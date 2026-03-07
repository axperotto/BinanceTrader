using CryptoResearchTool.Domain.Models;
namespace CryptoResearchTool.Application.Interfaces;
public interface IPersistenceRepository
{
    Task InitializeAsync();
    Task<string> CreateRunSessionAsync(string runName, string configJson);
    Task<string> CreateStrategyRunAsync(string runSessionId, string strategyName, string strategyType, string symbol, string timeframe, string configJson);
    Task SaveSignalAsync(string strategyRunId, StrategySignal signal);
    Task SaveTradeAsync(SimulatedTrade trade);
    Task SaveEquityPointAsync(string strategyRunId, EquityPoint point);
    Task SaveMetricsSnapshotAsync(StrategyMetrics metrics);
    Task SaveApplicationLogAsync(string level, string message, string? exception = null);
    Task<List<SimulatedTrade>> GetTradesAsync(string strategyRunId);
    Task<List<EquityPoint>> GetEquityHistoryAsync(string strategyRunId);
}
