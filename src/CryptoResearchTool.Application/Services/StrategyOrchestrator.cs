using CryptoResearchTool.Application.Interfaces;
using CryptoResearchTool.Domain.Configuration;
using CryptoResearchTool.Domain.Models;
using Microsoft.Extensions.Logging;
namespace CryptoResearchTool.Application.Services;

public class StrategyRunner
{
    public string StrategyRunId { get; }
    public ITradingStrategy Strategy { get; }
    public IPortfolioSimulator Portfolio { get; }
    public List<EquityPoint> EquityHistory { get; } = new();
    public StrategyMetrics CurrentMetrics { get; set; } = new();
    private readonly IMetricsCalculator _metricsCalculator;
    private readonly IPersistenceRepository _repository;
    private readonly SimulationConfiguration _simConfig;
    private readonly ILogger _logger;

    public StrategyRunner(string strategyRunId, ITradingStrategy strategy, IPortfolioSimulator portfolio,
        IMetricsCalculator metricsCalculator, IPersistenceRepository repository,
        SimulationConfiguration simConfig, ILogger logger)
    {
        StrategyRunId = strategyRunId;
        Strategy = strategy;
        Portfolio = portfolio;
        _metricsCalculator = metricsCalculator;
        _repository = repository;
        _simConfig = simConfig;
        _logger = logger;
    }

    public void OnTick(MarketTick tick)
    {
        if (tick.Symbol != Strategy.Symbol) return;
        Strategy.OnTick(tick);
        Portfolio.UpdateCurrentPrice(tick.Symbol, tick.Price);
    }

    public void OnCandle(Candle candle)
    {
        if (candle.Symbol != Strategy.Symbol || candle.Timeframe != Strategy.Timeframe) return;
        Strategy.OnCandle(candle);
        if (!candle.IsClosed) return;
        var signal = Strategy.Evaluate();
        if (signal == null || signal.Type == SignalType.None) return;
        CurrentMetrics.SignalsGenerated++;
        _ = _repository.SaveSignalAsync(StrategyRunId, signal);
        ProcessSignal(signal, candle.Close);
    }

    private void ProcessSignal(StrategySignal signal, decimal price)
    {
        if (signal.Type == SignalType.Buy && (Portfolio.OpenPosition == null || !Portfolio.OpenPosition.IsOpen))
        {
            var order = Portfolio.ExecuteBuy(Strategy.Symbol, price, _simConfig.DefaultPositionSizePercent, signal.Reason);
            if (order != null) CurrentMetrics.SignalsExecuted++;
        }
        else if (signal.Type == SignalType.Sell && Portfolio.OpenPosition != null && Portfolio.OpenPosition.IsOpen)
        {
            var order = Portfolio.ExecuteSell(Strategy.Symbol, price, signal.Reason);
            if (order != null)
            {
                CurrentMetrics.SignalsExecuted++;
                var trade = Portfolio.CompletedTrades.LastOrDefault();
                if (trade != null) _ = _repository.SaveTradeAsync(trade);
            }
        }
    }

    public async Task UpdateMetricsAsync()
    {
        var ep = Portfolio.GetEquityPoint();
        EquityHistory.Add(ep);
        await _repository.SaveEquityPointAsync(StrategyRunId, ep);
        CurrentMetrics = _metricsCalculator.Calculate(StrategyRunId, Strategy.Name, Portfolio, EquityHistory);
        await _repository.SaveMetricsSnapshotAsync(CurrentMetrics);
    }
}
