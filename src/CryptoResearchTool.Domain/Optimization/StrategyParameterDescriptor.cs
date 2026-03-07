namespace CryptoResearchTool.Domain.Optimization;

public enum ParameterType { Integer, Decimal }

/// <summary>Metadata describing a single optimizable parameter for a strategy type.</summary>
public class StrategyParameterDescriptor
{
    public string ParameterName { get; set; } = "";
    public ParameterType Type { get; set; } = ParameterType.Integer;
    public decimal DefaultMin { get; set; }
    public decimal DefaultMax { get; set; }
    public decimal DefaultStep { get; set; } = 1m;
    public string Description { get; set; } = "";
}
