namespace CryptoResearchTool.Domain.Optimization;

/// <summary>A single ranked result from a parameter optimization run.</summary>
public class OptimizationResult
{
    public int Rank { get; set; }
    public string StrategyName { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string Timeframe { get; set; } = "";

    /// <summary>The parameter values that produced this result.</summary>
    public Dictionary<string, decimal> ParameterValues { get; set; } = new();

    /// <summary>Metrics computed on the training portion of the date range.</summary>
    public OptimizationMetrics TrainMetrics { get; set; } = new();

    /// <summary>
    /// Metrics computed on the validation portion of the date range.
    /// Null when validation split is disabled.
    /// </summary>
    public OptimizationMetrics? ValidationMetrics { get; set; }

    /// <summary>
    /// Final ranking score. When validation is enabled this blends train and
    /// validation scores, favouring validation performance.
    /// </summary>
    public decimal OverallScore { get; set; }

    /// <summary>Human-readable summary of the parameter values.</summary>
    public string ParameterSummary =>
        string.Join(", ", ParameterValues.Select(kv => $"{kv.Key}={kv.Value}"));
}
