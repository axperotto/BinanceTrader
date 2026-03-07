using CryptoResearchTool.Domain.Configuration;
using CryptoResearchTool.Domain.Models;
namespace CryptoResearchTool.Application.Strategies;

public class BreakoutStrategy : BaseStrategy
{
    private readonly int _lookbackBars;
    private readonly decimal _breakoutThresholdPercent;
    public override string Name { get; }

    public BreakoutStrategy(StrategyConfiguration config, SimulationConfiguration simConfig)
        : base(config.Symbol, config.Timeframe, simConfig)
    {
        Name = config.Name;
        _lookbackBars = config.GetParameter("LookbackBars", 20);
        _breakoutThresholdPercent = config.GetParameter("BreakoutThresholdPercent", 0.5m);
    }

    public override StrategySignal? Evaluate()
    {
        if (CandleHistory.Count < _lookbackBars + 1) return null;
        var lookback = CandleHistory.TakeLast(_lookbackBars + 1).ToList();
        var prevBars = lookback.Take(_lookbackBars).ToList();
        var highestHigh = prevBars.Max(c => c.High);
        var lastClose = lookback.Last().Close;
        var threshold = highestHigh * (1m + _breakoutThresholdPercent / 100m);
        StrategySignal? signal = null;
        if (lastClose > threshold)
            signal = CreateSignal(SignalType.Buy, lastClose, $"Breakout({lastClose:F2}>{threshold:F2})");
        if (signal != null) _lastSignal = signal;
        return signal;
    }
}
