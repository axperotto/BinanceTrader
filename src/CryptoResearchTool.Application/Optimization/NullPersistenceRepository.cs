using CryptoResearchTool.Application.Interfaces;
using CryptoResearchTool.Domain.Models;

namespace CryptoResearchTool.Application.Optimization;

/// <summary>
/// A no-op implementation of <see cref="IPersistenceRepository"/> used during optimization
/// to avoid writing thousands of ephemeral backtest runs to the database.
/// All methods complete immediately and return empty/default values.
/// </summary>
internal sealed class NullPersistenceRepository : IPersistenceRepository
{
    public Task InitializeAsync() => Task.CompletedTask;

    public Task<string> CreateRunSessionAsync(string runName, string configJson)
        => Task.FromResult(Guid.NewGuid().ToString("N"));

    public Task<string> CreateStrategyRunAsync(
        string runSessionId, string strategyName, string strategyType,
        string symbol, string timeframe, string configJson)
        => Task.FromResult(Guid.NewGuid().ToString("N"));

    public Task SaveSignalAsync(string strategyRunId, StrategySignal signal) => Task.CompletedTask;
    public Task SaveTradeAsync(SimulatedTrade trade) => Task.CompletedTask;
    public Task SaveEquityPointAsync(string strategyRunId, EquityPoint point) => Task.CompletedTask;
    public Task SaveMetricsSnapshotAsync(StrategyMetrics metrics) => Task.CompletedTask;
    public Task SaveApplicationLogAsync(string level, string message, string? exception = null) => Task.CompletedTask;

    public Task<List<SimulatedTrade>> GetTradesAsync(string strategyRunId)
        => Task.FromResult(new List<SimulatedTrade>());

    public Task<List<EquityPoint>> GetEquityHistoryAsync(string strategyRunId)
        => Task.FromResult(new List<EquityPoint>());
}
