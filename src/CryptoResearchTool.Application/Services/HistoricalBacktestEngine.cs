using CryptoResearchTool.Application.Interfaces;
using CryptoResearchTool.Application.Strategies;
using CryptoResearchTool.Domain.Configuration;
using CryptoResearchTool.Domain.Models;
using Microsoft.Extensions.Logging;

namespace CryptoResearchTool.Application.Services;

public class HistoricalBacktestEngine
{
    private readonly IHistoricalDataService _dataService;
    private readonly IHistoricalDataCache _cache;
    private readonly IMetricsCalculator _metricsCalculator;
    private readonly IPersistenceRepository _repository;
    private readonly ILogger<HistoricalBacktestEngine> _logger;

    public HistoricalBacktestEngine(
        IHistoricalDataService dataService,
        IHistoricalDataCache cache,
        IMetricsCalculator metricsCalculator,
        IPersistenceRepository repository,
        ILogger<HistoricalBacktestEngine> logger)
    {
        _dataService = dataService;
        _cache = cache;
        _metricsCalculator = metricsCalculator;
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Runs a full historical backtest.
    /// In Global mode: all strategies share one symbol/timeframe from <paramref name="config"/>.
    /// In PerStrategy mode: each strategy uses its own configured symbol and timeframe.
    /// Returns the populated StrategyRunner list after the run.
    /// </summary>
    public async Task<List<StrategyRunner>> RunAsync(
        HistoricalAnalysisConfiguration config,
        List<StrategyConfiguration> strategyConfigs,
        string runSessionId,
        IProgress<(string status, int candlesProcessed, int totalCandles)>? progress = null,
        CancellationToken ct = default)
    {
        ValidateConfig(config);

        var simConfig = new SimulationConfiguration
        {
            InitialCapital = config.InitialCapital,
            FeePercent = config.FeePercent,
            SlippagePercent = config.SlippagePercent,
            DefaultPositionSizePercent = 100.0m
        };

        var enabled = strategyConfigs.Where(s => s.Enabled).ToList();
        if (enabled.Count == 0)
        {
            _logger.LogWarning("No enabled strategies to run.");
            progress?.Report(("No enabled strategies.", 0, 0));
            return new List<StrategyRunner>();
        }

        return config.TestMode == HistoricalTestMode.PerStrategy
            ? await RunPerStrategyAsync(config, enabled, simConfig, runSessionId, progress, ct)
            : await RunGlobalAsync(config, enabled, simConfig, runSessionId, progress, ct);
    }

    // ── Global mode ─────────────────────────────────────────────────────────

    private async Task<List<StrategyRunner>> RunGlobalAsync(
        HistoricalAnalysisConfiguration config,
        List<StrategyConfiguration> enabledStrategies,
        SimulationConfiguration simConfig,
        string runSessionId,
        IProgress<(string, int, int)>? progress,
        CancellationToken ct)
    {
        var candles = await GetCandlesAsync(
            config.Symbol, config.Timeframe, config, progress, ct);
        if (candles.Count == 0)
        {
            _logger.LogWarning("No candles returned for {Symbol} {Timeframe}", config.Symbol, config.Timeframe);
            progress?.Report(("No data returned.", 0, 0));
            return new List<StrategyRunner>();
        }

        var runners = await InitializeRunnersAsync(
            enabledStrategies, config.Symbol, config.Timeframe, config, simConfig, runSessionId);

        await ReplayCandlesAsync(candles, runners, progress, ct);
        FinalizeRunners(runners, candles);
        await UpdateMetricsAsync(runners);

        progress?.Report(("Completed", candles.Count, candles.Count));
        _logger.LogInformation(
            "[Global] Backtest complete. {Runners} strategies, {Candles} candles, {Symbol} {Timeframe}.",
            runners.Count, candles.Count, config.Symbol, config.Timeframe);
        return runners;
    }

    // ── Per-strategy mode ───────────────────────────────────────────────────

    private async Task<List<StrategyRunner>> RunPerStrategyAsync(
        HistoricalAnalysisConfiguration config,
        List<StrategyConfiguration> enabledStrategies,
        SimulationConfiguration simConfig,
        string runSessionId,
        IProgress<(string, int, int)>? progress,
        CancellationToken ct)
    {
        var allRunners = new List<StrategyRunner>();

        // Group strategies by their own symbol + timeframe; fall back to global config if blank
        var groups = enabledStrategies
            .GroupBy(s => (
                Symbol: string.IsNullOrWhiteSpace(s.Symbol) ? config.Symbol : s.Symbol.Trim().ToUpperInvariant(),
                Timeframe: string.IsNullOrWhiteSpace(s.Timeframe) ? config.Timeframe : s.Timeframe))
            .ToList();

        int totalGroups = groups.Count;
        int groupIdx = 0;

        foreach (var group in groups)
        {
            if (ct.IsCancellationRequested) break;

            groupIdx++;
            var symbol = group.Key.Symbol;
            var timeframe = group.Key.Timeframe;
            var statusPrefix = $"[{groupIdx}/{totalGroups}] {symbol} {timeframe}";

            progress?.Report(($"{statusPrefix}: downloading...", 0, 0));

            var candles = await GetCandlesAsync(symbol, timeframe, config, progress, ct);
            if (candles.Count == 0)
            {
                _logger.LogWarning("No candles for {Symbol} {Timeframe} – skipping group.", symbol, timeframe);
                progress?.Report(($"{statusPrefix}: no data.", 0, 0));
                continue;
            }

            var runners = await InitializeRunnersAsync(
                group.ToList(), symbol, timeframe, config, simConfig, runSessionId);

            await ReplayCandlesAsync(candles, runners,
                new Progress<(string s, int p, int t)>(r => progress?.Report(($"{statusPrefix}: {r.s}", r.p, r.t))),
                ct);

            FinalizeRunners(runners, candles);
            await UpdateMetricsAsync(runners);
            allRunners.AddRange(runners);

            _logger.LogInformation(
                "[PerStrategy] Group {Symbol} {Timeframe}: {Runners} strategies, {Candles} candles.",
                symbol, timeframe, runners.Count, candles.Count);
        }

        if (!ct.IsCancellationRequested)
            progress?.Report(("Completed", 1, 1));
        return allRunners;
    }

    // ── Shared helpers ──────────────────────────────────────────────────────

    private async Task<List<Candle>> GetCandlesAsync(
        string symbol,
        string timeframe,
        HistoricalAnalysisConfiguration config,
        IProgress<(string, int, int)>? progress,
        CancellationToken ct)
    {
        bool tryCache = config.UseLocalCache && !config.ForceRefresh;

        if (tryCache && _cache.Exists(symbol, timeframe, config.StartDate, config.EndDate))
        {
            progress?.Report(($"Using local cache for {symbol} {timeframe}...", 0, 0));
            var cached = await _cache.GetAsync(symbol, timeframe, config.StartDate, config.EndDate);
            if (cached != null && cached.Count > 0)
            {
                progress?.Report(($"Loaded {cached.Count:N0} candles from cache.", cached.Count, cached.Count));
                return cached;
            }
        }

        progress?.Report(($"Downloading {symbol} {timeframe} from Binance...", 0, 0));

        var downloadProgress = new Progress<(int downloaded, int total, string message)>(p =>
            progress?.Report(($"Downloading {symbol}: {p.message}", p.downloaded, Math.Max(p.total, p.downloaded))));

        var candles = await _dataService.GetCandlesAsync(
            symbol, timeframe, config.StartDate, config.EndDate, downloadProgress, ct);

        if (candles.Count > 0 && config.UseLocalCache)
            await _cache.SaveAsync(symbol, timeframe, config.StartDate, config.EndDate, candles);

        return candles;
    }

    private async Task<List<StrategyRunner>> InitializeRunnersAsync(
        List<StrategyConfiguration> strategies,
        string symbol,
        string timeframe,
        HistoricalAnalysisConfiguration historicalConfig,
        SimulationConfiguration simConfig,
        string runSessionId)
    {
        var runners = new List<StrategyRunner>();
        foreach (var stratConfig in strategies)
        {
            // Build an effective config that uses the resolved symbol/timeframe for this run
            var effective = new StrategyConfiguration
            {
                Type = stratConfig.Type,
                Name = stratConfig.Name,
                Symbol = symbol,
                Timeframe = timeframe,
                Enabled = true,
                Parameters = stratConfig.Parameters,
                StopLossPercent = stratConfig.StopLossPercent,
                TakeProfitPercent = stratConfig.TakeProfitPercent,
                MinBarsBetweenTrades = stratConfig.MinBarsBetweenTrades
            };

            var strategy = StrategyFactory.Create(effective, simConfig);
            var runId = await _repository.CreateStrategyRunAsync(
                runSessionId, effective.Name, effective.Type, symbol, timeframe, "{}");
            strategy.Initialize(runId);

            var portfolio = new PortfolioSimulator(
                simConfig,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<PortfolioSimulator>.Instance,
                runId,
                historicalConfig.InitialCapital);

            var runner = new StrategyRunner(
                runId, strategy, portfolio, _metricsCalculator, _repository,
                simConfig, effective,
                Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

            runners.Add(runner);
        }
        return runners;
    }

    private static async Task ReplayCandlesAsync(
        List<Candle> candles,
        List<StrategyRunner> runners,
        IProgress<(string, int, int)>? progress,
        CancellationToken ct)
    {
        int total = candles.Count;
        int processed = 0;
        progress?.Report(("Running analysis...", 0, total));

        foreach (var candle in candles)
        {
            if (ct.IsCancellationRequested) break;

            foreach (var runner in runners)
                runner.OnCandle(candle);

            processed++;
            if (processed % 500 == 0 || processed == total)
                progress?.Report(("Running analysis...", processed, total));

            if (processed % 1000 == 0)
                await Task.Yield();
        }
    }

    /// <summary>
    /// Force-closes any remaining open positions at the final candle close price,
    /// then finalizes equity history.
    /// </summary>
    private static void FinalizeRunners(List<StrategyRunner> runners, List<Candle> candles)
    {
        if (candles.Count == 0) return;
        var lastCandle = candles.Last();
        foreach (var runner in runners)
            runner.ForceClosePosition(lastCandle.Close, lastCandle.OpenTime);
    }

    private static async Task UpdateMetricsAsync(List<StrategyRunner> runners)
    {
        foreach (var runner in runners)
            await runner.UpdateMetricsAsync();
    }

    private static void ValidateConfig(HistoricalAnalysisConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(config.Symbol))
            throw new ArgumentException("Symbol cannot be empty.");
        if (string.IsNullOrWhiteSpace(config.Timeframe))
            throw new ArgumentException("Timeframe cannot be empty.");
        if (config.EndDate <= config.StartDate)
            throw new ArgumentException("End date must be after start date.");
        if (config.InitialCapital <= 0)
            throw new ArgumentException("Initial capital must be greater than 0.");
    }
}
