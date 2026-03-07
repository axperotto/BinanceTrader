using CryptoResearchTool.Domain.Optimization;

namespace CryptoResearchTool.Application.Interfaces;

/// <summary>Progress snapshot reported during optimization.</summary>
public class OptimizationProgress
{
    public int CurrentIndex { get; set; }
    public int Total { get; set; }
    public string Status { get; set; } = "";
    public TimeSpan Elapsed { get; set; }
}

/// <summary>Contract for the strategy parameter optimization engine.</summary>
public interface IOptimizationEngine
{
    /// <summary>
    /// Estimates the total number of parameter combinations that will be evaluated for
    /// the given request without running any backtests.
    /// </summary>
    long EstimateCombinationCount(OptimizationRequest request);

    /// <summary>
    /// Runs the full optimization and returns ranked results.
    /// Progress is reported via <paramref name="progress"/>; the run can be cancelled
    /// through <paramref name="ct"/>.
    /// </summary>
    Task<List<OptimizationResult>> RunAsync(
        OptimizationRequest request,
        IProgress<OptimizationProgress>? progress = null,
        CancellationToken ct = default);
}
