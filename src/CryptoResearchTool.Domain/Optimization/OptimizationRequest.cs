using CryptoResearchTool.Domain.Configuration;

namespace CryptoResearchTool.Domain.Optimization;

/// <summary>Everything needed to kick off a strategy parameter optimization run.</summary>
public class OptimizationRequest
{
    /// <summary>The base strategy configuration whose parameters will be varied.</summary>
    public StrategyConfiguration BaseStrategy { get; set; } = new();

    public string Symbol { get; set; } = "BTCUSDT";
    public string Timeframe { get; set; } = "1h";
    public DateTime StartDate { get; set; } = DateTime.UtcNow.AddMonths(-6);
    public DateTime EndDate { get; set; } = DateTime.UtcNow;

    public decimal InitialCapital { get; set; } = 1000m;
    public decimal FeePercent { get; set; } = 0.1m;
    public decimal SlippagePercent { get; set; } = 0.05m;

    public OptimizationSearchMode SearchMode { get; set; } = OptimizationSearchMode.GridSearch;
    public OptimizationObjective Objective { get; set; } = OptimizationObjective.RobustScore;

    /// <summary>Parameter ranges to sweep. Only enabled ranges are included.</summary>
    public List<StrategyParameterRange> ParameterRanges { get; set; } = new();

    /// <summary>Maximum grid-search combinations to evaluate.</summary>
    public int MaxCombinations { get; set; } = 500;

    /// <summary>Number of random samples to draw (random-search mode only).</summary>
    public int RandomSampleCount { get; set; } = 200;

    /// <summary>When true, the date range is split into a training and a validation window.</summary>
    public bool EnableValidationSplit { get; set; } = true;

    /// <summary>Percentage of the date range used for training (remainder used for validation).</summary>
    public decimal TrainPercent { get; set; } = 70m;

    public bool UseLocalCache { get; set; } = true;
}
