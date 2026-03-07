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
    /// Runs a full historical backtest: downloads (or loads cached) candles,
    /// replays them through all enabled strategies, and returns the resulting runners.
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

        var candles = await GetCandlesAsync(config, progress, ct);
        if (candles.Count == 0)
        {
            _logger.LogWarning("No candles returned for {Symbol} {Timeframe}", config.Symbol, config.Timeframe);
            progress?.Report(("No data returned.", 0, 0));
            return new List<StrategyRunner>();
        }

        var runners = await InitializeRunnersAsync(strategyConfigs, config, simConfig, runSessionId);
        if (runners.Count == 0)
        {
            _logger.LogWarning("No enabled strategies to run.");
            return runners;
        }

        await ReplayCandlesAsync(candles, runners, progress, ct);

        foreach (var runner in runners)
            await runner.UpdateMetricsAsync();

        progress?.Report(("Completed", candles.Count, candles.Count));
        _logger.LogInformation("Historical backtest complete. {Runners} strategies, {Candles} candles.", runners.Count, candles.Count);
        return runners;
    }

    private async Task<List<Candle>> GetCandlesAsync(
        HistoricalAnalysisConfiguration config,
        IProgress<(string status, int candlesProcessed, int totalCandles)>? progress,
        CancellationToken ct)
    {
        bool tryCache = config.UseLocalCache && !config.ForceRefresh;

        if (tryCache && _cache.Exists(config.Symbol, config.Timeframe, config.StartDate, config.EndDate))
        {
            progress?.Report(("Using local cache...", 0, 0));
            var cached = await _cache.GetAsync(config.Symbol, config.Timeframe, config.StartDate, config.EndDate);
            if (cached != null && cached.Count > 0)
            {
                progress?.Report(($"Loaded {cached.Count:N0} candles from cache.", cached.Count, cached.Count));
                return cached;
            }
            progress?.Report(("Cache empty, downloading from Binance...", 0, 0));
        }
        else
        {
            progress?.Report(("Downloading data from Binance...", 0, 0));
        }

        var downloadProgress = new Progress<(int downloaded, int total, string message)>(p =>
            progress?.Report(($"Downloading: {p.message}", p.downloaded, Math.Max(p.total, p.downloaded))));

        var candles = await _dataService.GetCandlesAsync(
            config.Symbol, config.Timeframe, config.StartDate, config.EndDate, downloadProgress, ct);

        if (candles.Count > 0 && config.UseLocalCache)
            await _cache.SaveAsync(config.Symbol, config.Timeframe, config.StartDate, config.EndDate, candles);

        return candles;
    }

    private async Task<List<StrategyRunner>> InitializeRunnersAsync(
        List<StrategyConfiguration> strategyConfigs,
        HistoricalAnalysisConfiguration historicalConfig,
        SimulationConfiguration simConfig,
        string runSessionId)
    {
        var runners = new List<StrategyRunner>();
        foreach (var stratConfig in strategyConfigs.Where(s => s.Enabled))
        {
            // Override symbol/timeframe to match the historical analysis configuration
            var overridden = new StrategyConfiguration
            {
                Type = stratConfig.Type,
                Name = stratConfig.Name,
                Symbol = historicalConfig.Symbol,
                Timeframe = historicalConfig.Timeframe,
                Enabled = true,
                Parameters = stratConfig.Parameters
            };

            var strategy = StrategyFactory.Create(overridden, simConfig);
            var runId = await _repository.CreateStrategyRunAsync(
                runSessionId, overridden.Name, overridden.Type,
                historicalConfig.Symbol, historicalConfig.Timeframe, "{}");
            strategy.Initialize(runId);

            var portfolio = new PortfolioSimulator(
                simConfig,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<PortfolioSimulator>.Instance,
                runId,
                historicalConfig.InitialCapital);

            var runner = new StrategyRunner(
                runId, strategy, portfolio, _metricsCalculator, _repository, simConfig,
                Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

            runners.Add(runner);
        }
        return runners;
    }

    private static async Task ReplayCandlesAsync(
        List<Candle> candles,
        List<StrategyRunner> runners,
        IProgress<(string status, int candlesProcessed, int totalCandles)>? progress,
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

            // Yield to keep the UI responsive without slowing down too much
            if (processed % 1000 == 0)
                await Task.Yield();
        }
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
