using CryptoResearchTool.Domain.Configuration;
using CryptoResearchTool.Domain.Models;
namespace CryptoResearchTool.Application.Strategies;

public class MomentumStrategy : BaseStrategy
{
    private readonly int _lookbackBars;
    private readonly decimal _entryThresholdPercent;
    private readonly decimal _exitThresholdPercent;
    public override string Name { get; }

    public MomentumStrategy(StrategyConfiguration config, SimulationConfiguration simConfig)
        : base(config.Symbol, config.Timeframe, simConfig)
    {
        Name = config.Name;
        _lookbackBars = config.GetParameter("LookbackBars", 10);
        _entryThresholdPercent = config.GetParameter("EntryThresholdPercent", 1.0m);
        _exitThresholdPercent = config.GetParameter("ExitThresholdPercent", 0.0m);
    }

    public override StrategySignal? Evaluate()
    {
        if (CandleHistory.Count < _lookbackBars + 1) return null;
        var bars = CandleHistory.TakeLast(_lookbackBars + 1).ToList();
        var firstClose = bars.First().Close;
        var lastClose = bars.Last().Close;
        if (firstClose <= 0) return null;
        var momentum = ((lastClose - firstClose) / firstClose) * 100m;
        StrategySignal? signal = null;
        if (momentum > _entryThresholdPercent)
            signal = CreateSignal(SignalType.Buy, lastClose, $"Momentum({momentum:F2}%)");
        else if (momentum < _exitThresholdPercent)
            signal = CreateSignal(SignalType.Sell, lastClose, $"MomentumExit({momentum:F2}%)");
        if (signal != null) _lastSignal = signal;
        return signal;
    }
}
