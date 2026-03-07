using CryptoResearchTool.Domain.Models;
namespace CryptoResearchTool.Application.Interfaces;
public interface IPortfolioSimulator
{
    string StrategyRunId { get; }
    decimal Cash { get; }
    decimal InitialCapital { get; }
    PortfolioPosition? OpenPosition { get; }
    List<SimulatedTrade> CompletedTrades { get; }
    SimulatedOrder? ExecuteBuy(string symbol, decimal price, decimal positionSizePercent, string reason);
    SimulatedOrder? ExecuteSell(string symbol, decimal price, string reason);
    void UpdateCurrentPrice(string symbol, decimal price);
    decimal GetEquity();
    EquityPoint GetEquityPoint();
}
