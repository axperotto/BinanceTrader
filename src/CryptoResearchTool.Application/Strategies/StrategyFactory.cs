using CryptoResearchTool.Application.Interfaces;
using CryptoResearchTool.Domain.Configuration;
namespace CryptoResearchTool.Application.Strategies;

public static class StrategyFactory
{
    public static ITradingStrategy Create(StrategyConfiguration config, SimulationConfiguration simConfig)
    {
        return config.Type switch
        {
            "MovingAverageCrossover" => new MovingAverageCrossoverStrategy(config, simConfig),
            "RsiMeanReversion" => new RsiMeanReversionStrategy(config, simConfig),
            "Breakout" => new BreakoutStrategy(config, simConfig),
            "Momentum" => new MomentumStrategy(config, simConfig),
            "BuyAndHold" => new BuyAndHoldStrategy(config, simConfig),
            _ => throw new ArgumentException($"Unknown strategy type: {config.Type}")
        };
    }
}
