using CryptoResearchTool.Domain.Models;
namespace CryptoResearchTool.Application.Interfaces;
public interface IMetricsCalculator
{
    StrategyMetrics Calculate(string strategyRunId, string strategyName, IPortfolioSimulator portfolio, List<EquityPoint> equityHistory);
}
