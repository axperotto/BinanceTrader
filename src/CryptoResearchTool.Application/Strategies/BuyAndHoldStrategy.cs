using CryptoResearchTool.Domain.Configuration;
using CryptoResearchTool.Domain.Models;
namespace CryptoResearchTool.Application.Strategies;

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
            var signal = CreateSignal(SignalType.Buy, LastPrice, "BuyAndHold_Initial");
            _lastSignal = signal;
            return signal;
        }
        return null;
    }
}
