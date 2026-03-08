using CryptoResearchTool.Domain.Optimization;

namespace CryptoResearchTool.Application.Optimization;

/// <summary>
/// Provides per-strategy-type metadata describing which parameters can be optimized
/// and their recommended default search ranges.
/// </summary>
public static class StrategyParameterDescriptorRegistry
{
    private static readonly Dictionary<string, List<StrategyParameterDescriptor>> _registry = new()
    {
        ["MovingAverageCrossover"] = new()
        {
            new() { ParameterName = "FastPeriod",                   Type = ParameterType.Integer, DefaultMin = 5,     DefaultMax = 20,   DefaultStep = 1,     Description = "Fast MA period" },
            new() { ParameterName = "SlowPeriod",                   Type = ParameterType.Integer, DefaultMin = 20,    DefaultMax = 80,   DefaultStep = 5,     Description = "Slow MA period" },
            new() { ParameterName = "TrendFilterPeriod",            Type = ParameterType.Integer, DefaultMin = 50,    DefaultMax = 200,  DefaultStep = 10,    Description = "Trend filter MA period" },
            new() { ParameterName = "StopLossPercent",              Type = ParameterType.Decimal, DefaultMin = 1.0m,  DefaultMax = 3.0m, DefaultStep = 0.5m,  Description = "Stop loss %" },
            new() { ParameterName = "MinBarsBetweenTrades",         Type = ParameterType.Integer, DefaultMin = 4,     DefaultMax = 20,   DefaultStep = 2,     Description = "Minimum bars between trades" },
            new() { ParameterName = "BreakEvenTriggerPercent",      Type = ParameterType.Decimal, DefaultMin = 1.0m,  DefaultMax = 3.0m, DefaultStep = 0.5m,  Description = "Break-even trigger %" },
            new() { ParameterName = "TrailingStopDistancePercent",  Type = ParameterType.Decimal, DefaultMin = 1.0m,  DefaultMax = 3.0m, DefaultStep = 0.5m,  Description = "Trailing stop distance %" },
        },
        ["RsiMeanReversion"] = new()
        {
            new() { ParameterName = "RsiPeriod",                    Type = ParameterType.Integer, DefaultMin = 10,    DefaultMax = 21,   DefaultStep = 1,     Description = "RSI period" },
            new() { ParameterName = "Oversold",                     Type = ParameterType.Decimal, DefaultMin = 20,    DefaultMax = 35,   DefaultStep = 1,     Description = "Oversold threshold" },
            new() { ParameterName = "Overbought",                   Type = ParameterType.Decimal, DefaultMin = 65,    DefaultMax = 80,   DefaultStep = 1,     Description = "Overbought threshold" },
            new() { ParameterName = "ExitRsiLevel",                 Type = ParameterType.Decimal, DefaultMin = 45,    DefaultMax = 60,   DefaultStep = 5,     Description = "RSI exit level" },
            new() { ParameterName = "StopLossPercent",              Type = ParameterType.Decimal, DefaultMin = 1.0m,  DefaultMax = 3.0m, DefaultStep = 0.5m,  Description = "Stop loss %" },
            new() { ParameterName = "MinBarsBetweenTrades",         Type = ParameterType.Integer, DefaultMin = 4,     DefaultMax = 20,   DefaultStep = 2,     Description = "Minimum bars between trades" },
            new() { ParameterName = "BreakEvenTriggerPercent",      Type = ParameterType.Decimal, DefaultMin = 1.0m,  DefaultMax = 3.0m, DefaultStep = 0.5m,  Description = "Break-even trigger %" },
        },
        ["Breakout"] = new()
        {
            new() { ParameterName = "LookbackBars",                 Type = ParameterType.Integer, DefaultMin = 10,    DefaultMax = 40,   DefaultStep = 5,     Description = "Channel lookback bars" },
            new() { ParameterName = "BreakoutThresholdPercent",     Type = ParameterType.Decimal, DefaultMin = 0.2m,  DefaultMax = 1.0m, DefaultStep = 0.1m,  Description = "Breakout threshold %" },
            new() { ParameterName = "TrendFilterPeriod",            Type = ParameterType.Integer, DefaultMin = 50,    DefaultMax = 200,  DefaultStep = 10,    Description = "Trend filter MA period" },
            new() { ParameterName = "StopLossPercent",              Type = ParameterType.Decimal, DefaultMin = 1.0m,  DefaultMax = 3.5m, DefaultStep = 0.5m,  Description = "Stop loss %" },
            new() { ParameterName = "MinBarsBetweenTrades",         Type = ParameterType.Integer, DefaultMin = 4,     DefaultMax = 20,   DefaultStep = 2,     Description = "Minimum bars between trades" },
            new() { ParameterName = "BreakEvenTriggerPercent",      Type = ParameterType.Decimal, DefaultMin = 1.0m,  DefaultMax = 4.0m, DefaultStep = 0.5m,  Description = "Break-even trigger %" },
            new() { ParameterName = "TrailingStopDistancePercent",  Type = ParameterType.Decimal, DefaultMin = 1.0m,  DefaultMax = 3.5m, DefaultStep = 0.5m,  Description = "Trailing stop distance %" },
        },
        ["Momentum"] = new()
        {
            new() { ParameterName = "LookbackBars",                 Type = ParameterType.Integer, DefaultMin = 8,     DefaultMax = 30,   DefaultStep = 2,     Description = "Momentum lookback bars" },
            new() { ParameterName = "EntryThresholdPercent",        Type = ParameterType.Decimal, DefaultMin = 1.0m,  DefaultMax = 3.5m, DefaultStep = 0.5m,  Description = "Entry momentum threshold %" },
            new() { ParameterName = "ExitThresholdPercent",         Type = ParameterType.Decimal, DefaultMin = -2.0m, DefaultMax = -0.5m,DefaultStep = 0.5m,  Description = "Exit momentum threshold %" },
            new() { ParameterName = "TrendFilterPeriod",            Type = ParameterType.Integer, DefaultMin = 30,    DefaultMax = 150,  DefaultStep = 10,    Description = "Trend filter MA period" },
            new() { ParameterName = "StopLossPercent",              Type = ParameterType.Decimal, DefaultMin = 1.0m,  DefaultMax = 3.5m, DefaultStep = 0.5m,  Description = "Stop loss %" },
            new() { ParameterName = "MinBarsBetweenTrades",         Type = ParameterType.Integer, DefaultMin = 4,     DefaultMax = 20,   DefaultStep = 2,     Description = "Minimum bars between trades" },
            new() { ParameterName = "BreakEvenTriggerPercent",      Type = ParameterType.Decimal, DefaultMin = 1.0m,  DefaultMax = 4.0m, DefaultStep = 0.5m,  Description = "Break-even trigger %" },
            new() { ParameterName = "TrailingStopDistancePercent",  Type = ParameterType.Decimal, DefaultMin = 1.0m,  DefaultMax = 3.5m, DefaultStep = 0.5m,  Description = "Trailing stop distance %" },
        },
    };

    /// <summary>Returns descriptors for all optimizable parameters of the given strategy type.</summary>
    public static List<StrategyParameterDescriptor> GetDescriptors(string strategyType)
        => _registry.TryGetValue(strategyType, out var descriptors)
            ? descriptors
            : new List<StrategyParameterDescriptor>();

    /// <summary>Returns a dictionary of sensible default parameter values (midpoint of each range).</summary>
    public static Dictionary<string, decimal> GetDefaultValues(string strategyType)
    {
        var result = new Dictionary<string, decimal>();
        foreach (var d in GetDescriptors(strategyType))
        {
            decimal mid = d.Type == ParameterType.Integer
                ? Math.Round((d.DefaultMin + d.DefaultMax) / 2m, 0)
                : Math.Round((d.DefaultMin + d.DefaultMax) / 2m, 2);
            result[d.ParameterName] = mid;
        }
        return result;
    }

    /// <summary>All strategy types that have registered descriptors.</summary>
    public static IReadOnlyCollection<string> KnownStrategyTypes
        => _registry.Keys.ToList().AsReadOnly();
}
