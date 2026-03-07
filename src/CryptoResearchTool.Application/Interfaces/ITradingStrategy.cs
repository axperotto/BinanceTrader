using CryptoResearchTool.Domain.Models;
namespace CryptoResearchTool.Application.Interfaces;
public interface ITradingStrategy
{
    string Name { get; }
    string Symbol { get; }
    string Timeframe { get; }
    void OnTick(MarketTick tick);
    void OnCandle(Candle candle);
    StrategySignal? Evaluate();
    StrategyRuntimeState GetState();
    void Initialize(string strategyRunId);
}
