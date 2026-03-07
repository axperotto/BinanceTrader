namespace CryptoResearchTool.Domain.Optimization;

/// <summary>Defines the search range for a single strategy parameter during optimization.</summary>
public class StrategyParameterRange
{
    public string ParameterName { get; set; } = "";
    public decimal MinValue { get; set; }
    public decimal MaxValue { get; set; }
    public decimal Step { get; set; } = 1m;
    public bool IsEnabled { get; set; } = true;

    /// <summary>When true, parameter values are rounded to integers during combination generation.</summary>
    public bool IsInteger { get; set; } = false;
}
