using CryptoResearchTool.Application.Interfaces;
using CryptoResearchTool.Application.Services;
using CryptoResearchTool.Application.Strategies;
using CryptoResearchTool.Domain.Configuration;
using CryptoResearchTool.Domain.Models;
using CryptoResearchTool.Domain.Optimization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CryptoResearchTool.Application.Optimization;

/// <summary>
/// Runs strategy parameter optimization over a historical candle series.
///
/// Workflow:
///   1. Downloads (or loads from cache) the required candle data once.
///   2. Optionally splits it into training and validation windows.
///   3. Generates parameter combinations (grid or random).
///   4. For each combination: clones the base strategy config, runs a lightweight
///      in-memory backtest (using <see cref="NullPersistenceRepository"/> to avoid DB I/O),
///      and computes metrics.
///   5. Ranks results by the selected objective score.
/// </summary>
public class OptimizationEngine : IOptimizationEngine
{
    private readonly IHistoricalDataService _dataService;
    private readonly IHistoricalDataCache   _cache;
    private readonly ILogger<OptimizationEngine> _logger;

    // Shared no-op repository and metrics calculator: both are thread-safe and stateless.
    private static readonly NullPersistenceRepository _nullRepo   = new();
    private static readonly IMetricsCalculator        _metricsCalc = new MetricsCalculator();

    public OptimizationEngine(
        IHistoricalDataService dataService,
        IHistoricalDataCache cache,
        ILogger<OptimizationEngine> logger)
    {
        _dataService = dataService;
        _cache       = cache;
        _logger      = logger;
    }

    // ── IOptimizationEngine ──────────────────────────────────────────────────

    public long EstimateCombinationCount(OptimizationRequest request) =>
        request.SearchMode == OptimizationSearchMode.RandomSearch
            ? request.RandomSampleCount
            : ParameterCombinationGenerator.EstimateGridSearchCount(request.ParameterRanges);

    public async Task<List<OptimizationResult>> RunAsync(
        OptimizationRequest request,
        IProgress<OptimizationProgress>? progress = null,
        CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // ── 1. Fetch candles ─────────────────────────────────────────────────
        Report(progress, "Downloading historical data…", 0, 1, sw.Elapsed);
        var allCandles = await GetCandlesAsync(request, progress, ct);

        if (allCandles.Count == 0)
            throw new InvalidOperationException(
                "No historical data available for the selected symbol / timeframe / date range.");

        if (ct.IsCancellationRequested) return new List<OptimizationResult>();

        // ── 2. Train / validation split ──────────────────────────────────────
        List<Candle> trainCandles;
        List<Candle>? valCandles = null;

        if (request.EnableValidationSplit)
        {
            int splitIdx = (int)(allCandles.Count * request.TrainPercent / 100m);
            splitIdx = Math.Max(1, Math.Min(splitIdx, allCandles.Count - 1));
            trainCandles = allCandles.Take(splitIdx).ToList();
            valCandles   = allCandles.Skip(splitIdx).ToList();
        }
        else
        {
            trainCandles = allCandles;
        }

        // ── 3. Generate combinations ─────────────────────────────────────────
        List<Dictionary<string, decimal>> combinations;
        if (request.SearchMode == OptimizationSearchMode.RandomSearch)
        {
            combinations = ParameterCombinationGenerator.GenerateRandom(
                request.ParameterRanges, request.RandomSampleCount, request.BaseStrategy.Type);
        }
        else
        {
            combinations = ParameterCombinationGenerator.GenerateGrid(
                request.ParameterRanges, request.MaxCombinations, request.BaseStrategy.Type);
        }

        if (combinations.Count == 0)
            throw new InvalidOperationException(
                "No valid parameter combinations were generated. " +
                "Check that parameter ranges are non-empty and constraints can be satisfied.");

        int total = combinations.Count;
        Report(progress, $"Running {total} combinations…", 0, total, sw.Elapsed);

        // ── 4. Run backtests ─────────────────────────────────────────────────
        var simConfig = new SimulationConfiguration
        {
            InitialCapital           = request.InitialCapital,
            FeePercent               = request.FeePercent,
            SlippagePercent          = request.SlippagePercent,
            DefaultPositionSizePercent = 100m,
        };

        var results  = new List<OptimizationResult>(total);
        int processed = 0;

        foreach (var paramSet in combinations)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var config = StrategyConfigurationCloner.CloneAndInject(
                    request.BaseStrategy, paramSet, request.Symbol, request.Timeframe);

                var trainMetrics = RunSingleBacktest(config, simConfig, trainCandles);
                trainMetrics.Score = ObjectiveScorer.ComputeScore(trainMetrics, request.Objective);

                OptimizationMetrics? valMetrics = null;
                if (valCandles != null && valCandles.Count > 0)
                {
                    valMetrics = RunSingleBacktest(config, simConfig, valCandles);
                    valMetrics.Score = ObjectiveScorer.ComputeScore(valMetrics, request.Objective);
                }

                var overall = ObjectiveScorer.ComputeOverallScore(trainMetrics, valMetrics, request.Objective);

                results.Add(new OptimizationResult
                {
                    StrategyName      = config.Name,
                    Symbol            = request.Symbol,
                    Timeframe         = request.Timeframe,
                    ParameterValues   = new Dictionary<string, decimal>(paramSet),
                    TrainMetrics      = trainMetrics,
                    ValidationMetrics = valMetrics,
                    OverallScore      = overall,
                });
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Skipping combination – backtest threw an exception.");
            }

            processed++;

            if (processed % 10 == 0 || processed == total)
                Report(progress, $"Tested {processed}/{total}", processed, total, sw.Elapsed);

            // Periodically yield so the calling thread stays responsive
            if (processed % 50 == 0)
                await Task.Yield();
        }

        // ── 5. Rank by overall score (descending) ────────────────────────────
        results = results
            .OrderByDescending(r => r.OverallScore)
            .Select((r, i) => { r.Rank = i + 1; return r; })
            .ToList();

        Report(progress,
            $"Completed – {results.Count} results ranked. Best score: {results.FirstOrDefault()?.OverallScore:F2}",
            total, total, sw.Elapsed);

        _logger.LogInformation(
            "Optimization complete. {Total} combinations, {Results} results, " +
            "best score {BestScore:F2}.",
            total, results.Count, results.FirstOrDefault()?.OverallScore ?? 0);

        return results;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs a single backtest on the provided candle slice and returns the resulting metrics.
    /// Uses an in-memory (null) repository to avoid any database I/O.
    /// </summary>
    private static OptimizationMetrics RunSingleBacktest(
        StrategyConfiguration config,
        SimulationConfiguration simConfig,
        List<Candle> candles)
    {
        var strategy  = StrategyFactory.Create(config, simConfig);
        var runId     = Guid.NewGuid().ToString("N");
        strategy.Initialize(runId);

        var portfolio = new PortfolioSimulator(
            simConfig,
            NullLogger<PortfolioSimulator>.Instance,
            runId,
            simConfig.InitialCapital);

        var runner = new StrategyRunner(
            runId, strategy, portfolio, _metricsCalc, _nullRepo,
            simConfig, config, NullLogger.Instance);

        foreach (var candle in candles)
            runner.OnCandle(candle);

        if (candles.Count > 0)
            runner.ForceClosePosition(candles[^1].Close, candles[^1].OpenTime);

        // UpdateMetricsAsync completes synchronously because NullPersistenceRepository tasks
        // are already completed (no async I/O is involved).
        runner.UpdateMetricsAsync().GetAwaiter().GetResult();

        var m = runner.CurrentMetrics;
        return new OptimizationMetrics
        {
            ReturnPct      = m.ReturnPercent,
            NetPnL         = m.NetProfit,
            MaxDrawdownPct = m.MaxDrawdownPercent,
            SharpeRatio    = m.SharpeRatio,
            ProfitFactor   = m.ProfitFactor,
            Trades         = m.TotalTrades,
            WinRate        = m.WinRate,
        };
    }

    private async Task<List<Candle>> GetCandlesAsync(
        OptimizationRequest request,
        IProgress<OptimizationProgress>? progress,
        CancellationToken ct)
    {
        if (request.UseLocalCache &&
            _cache.Exists(request.Symbol, request.Timeframe, request.StartDate, request.EndDate))
        {
            var cached = await _cache.GetAsync(
                request.Symbol, request.Timeframe, request.StartDate, request.EndDate);
            if (cached != null && cached.Count > 0)
                return cached;
        }

        var candles = await _dataService.GetCandlesAsync(
            request.Symbol, request.Timeframe, request.StartDate, request.EndDate,
            new Progress<(int downloaded, int total, string message)>(p =>
                Report(progress, $"Downloading: {p.message}", p.downloaded,
                       Math.Max(p.total, p.downloaded), TimeSpan.Zero)),
            ct);

        if (candles.Count > 0 && request.UseLocalCache)
            await _cache.SaveAsync(
                request.Symbol, request.Timeframe, request.StartDate, request.EndDate, candles);

        return candles;
    }

    private static void Report(
        IProgress<OptimizationProgress>? progress,
        string status, int current, int total, TimeSpan elapsed) =>
        progress?.Report(new OptimizationProgress
        {
            Status       = status,
            CurrentIndex = current,
            Total        = total,
            Elapsed      = elapsed,
        });
}
