using CryptoResearchTool.Domain.Configuration;
using CryptoResearchTool.Domain.Models;
namespace CryptoResearchTool.Application.Strategies;

/// <summary>
/// Benchmark strategy: buys once on the first available candle and holds until end of test.
/// The backtest engine will force-close the position at the last candle's close price.
/// </summary>
public class BuyAndHoldStrategy : BaseStrategy
{
    private bool _bought = false;
    public override string Name { get; }

    public BuyAndHoldStrategy(StrategyConfiguration config, SimulationConfiguration simConfig)
        : base(config.Symbol, config.Timeframe, simConfig)
    {
        Name = config.Name;
    }

    public override StrategySignal? Evaluate()
    {
        if (!_bought && CandleHistory.Count > 0)
        {
            _bought = true;
            // Use the current candle close (LastPrice updated by BaseStrategy.OnCandle)
            // as the signal price so the benchmark entry reflects a real market price.
            var signal = CreateSignal(SignalType.Buy, LastPrice, "BuyAndHold_Initial");
            _lastSignal = signal;
            return signal;
        }
        return null;
    }
}
