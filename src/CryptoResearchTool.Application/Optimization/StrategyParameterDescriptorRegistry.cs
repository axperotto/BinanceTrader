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
            new() { ParameterName = "FastPeriod",                   Type = ParameterType.Integer, DefaultMin = 2,     DefaultMax = 20,   DefaultStep = 1,     Description = "Fast MA period" },
            new() { ParameterName = "SlowPeriod",                   Type = ParameterType.Integer, DefaultMin = 10,    DefaultMax = 100,  DefaultStep = 5,     Description = "Slow MA period" },
            new() { ParameterName = "TrendFilterPeriod",            Type = ParameterType.Integer, DefaultMin = 0,     DefaultMax = 200,  DefaultStep = 10,    Description = "Trend filter MA period (0=disabled)" },
            new() { ParameterName = "MinBarsBetweenTrades",         Type = ParameterType.Integer, DefaultMin = 0,     DefaultMax = 20,   DefaultStep = 2,     Description = "Minimum bars between trades" },
            new() { ParameterName = "StopLossPercent",              Type = ParameterType.Decimal, DefaultMin = 0,     DefaultMax = 5,    DefaultStep = 0.5m,  Description = "Stop loss %" },
            new() { ParameterName = "BreakEvenTriggerPercent",      Type = ParameterType.Decimal, DefaultMin = 0,     DefaultMax = 5,    DefaultStep = 0.5m,  Description = "Break-even trigger %" },
            new() { ParameterName = "TrailingStopActivationPercent",Type = ParameterType.Decimal, DefaultMin = 0,     DefaultMax = 5,    DefaultStep = 0.5m,  Description = "Trailing stop activation %" },
            new() { ParameterName = "TrailingStopDistancePercent",  Type = ParameterType.Decimal, DefaultMin = 0,     DefaultMax = 5,    DefaultStep = 0.5m,  Description = "Trailing stop distance %" },
        },
        ["Breakout"] = new()
        {
            new() { ParameterName = "LookbackBars",                 Type = ParameterType.Integer, DefaultMin = 5,     DefaultMax = 60,   DefaultStep = 5,     Description = "Channel lookback bars" },
            new() { ParameterName = "BreakoutThresholdPercent",     Type = ParameterType.Decimal, DefaultMin = 0.05m, DefaultMax = 2m,   DefaultStep = 0.05m, Description = "Breakout threshold %" },
            new() { ParameterName = "TrendFilterPeriod",            Type = ParameterType.Integer, DefaultMin = 0,     DefaultMax = 200,  DefaultStep = 10,    Description = "Trend filter MA period (0=disabled)" },
            new() { ParameterName = "ConfirmationBars",             Type = ParameterType.Integer, DefaultMin = 1,     DefaultMax = 5,    DefaultStep = 1,     Description = "Confirmation bars required" },
            new() { ParameterName = "MinBarsBetweenTrades",         Type = ParameterType.Integer, DefaultMin = 0,     DefaultMax = 20,   DefaultStep = 2,     Description = "Minimum bars between trades" },
            new() { ParameterName = "StopLossPercent",              Type = ParameterType.Decimal, DefaultMin = 0,     DefaultMax = 5,    DefaultStep = 0.5m,  Description = "Stop loss %" },
            new() { ParameterName = "BreakEvenTriggerPercent",      Type = ParameterType.Decimal, DefaultMin = 0,     DefaultMax = 5,    DefaultStep = 0.5m,  Description = "Break-even trigger %" },
            new() { ParameterName = "TrailingStopActivationPercent",Type = ParameterType.Decimal, DefaultMin = 0,     DefaultMax = 5,    DefaultStep = 0.5m,  Description = "Trailing stop activation %" },
            new() { ParameterName = "TrailingStopDistancePercent",  Type = ParameterType.Decimal, DefaultMin = 0,     DefaultMax = 5,    DefaultStep = 0.5m,  Description = "Trailing stop distance %" },
        },
        ["Momentum"] = new()
        {
            new() { ParameterName = "LookbackBars",                 Type = ParameterType.Integer, DefaultMin = 3,     DefaultMax = 50,   DefaultStep = 3,     Description = "Momentum lookback bars" },
            new() { ParameterName = "EntryThresholdPercent",        Type = ParameterType.Decimal, DefaultMin = 0.1m,  DefaultMax = 5m,   DefaultStep = 0.2m,  Description = "Entry momentum threshold %" },
            new() { ParameterName = "ExitThresholdPercent",         Type = ParameterType.Decimal, DefaultMin = -5m,   DefaultMax = 1m,   DefaultStep = 0.2m,  Description = "Exit momentum threshold %" },
            new() { ParameterName = "TrendFilterPeriod",            Type = ParameterType.Integer, DefaultMin = 0,     DefaultMax = 200,  DefaultStep = 10,    Description = "Trend filter MA period (0=disabled)" },
            new() { ParameterName = "ConfirmationBars",             Type = ParameterType.Integer, DefaultMin = 1,     DefaultMax = 5,    DefaultStep = 1,     Description = "Confirmation bars required" },
            new() { ParameterName = "MinBarsBetweenTrades",         Type = ParameterType.Integer, DefaultMin = 0,     DefaultMax = 20,   DefaultStep = 2,     Description = "Minimum bars between trades" },
            new() { ParameterName = "StopLossPercent",              Type = ParameterType.Decimal, DefaultMin = 0,     DefaultMax = 5,    DefaultStep = 0.5m,  Description = "Stop loss %" },
            new() { ParameterName = "BreakEvenTriggerPercent",      Type = ParameterType.Decimal, DefaultMin = 0,     DefaultMax = 5,    DefaultStep = 0.5m,  Description = "Break-even trigger %" },
            new() { ParameterName = "TrailingStopActivationPercent",Type = ParameterType.Decimal, DefaultMin = 0,     DefaultMax = 5,    DefaultStep = 0.5m,  Description = "Trailing stop activation %" },
            new() { ParameterName = "TrailingStopDistancePercent",  Type = ParameterType.Decimal, DefaultMin = 0,     DefaultMax = 5,    DefaultStep = 0.5m,  Description = "Trailing stop distance %" },
        },
        ["RsiMeanReversion"] = new()
        {
            new() { ParameterName = "RsiPeriod",                    Type = ParameterType.Integer, DefaultMin = 5,     DefaultMax = 30,   DefaultStep = 1,     Description = "RSI period" },
            new() { ParameterName = "Oversold",                     Type = ParameterType.Decimal, DefaultMin = 15,    DefaultMax = 40,   DefaultStep = 1,     Description = "Oversold threshold" },
            new() { ParameterName = "Overbought",                   Type = ParameterType.Decimal, DefaultMin = 60,    DefaultMax = 85,   DefaultStep = 1,     Description = "Overbought threshold" },
            new() { ParameterName = "ExitRsiLevel",                 Type = ParameterType.Decimal, DefaultMin = 0,     DefaultMax = 60,   DefaultStep = 5,     Description = "RSI exit level (0=use overbought)" },
            new() { ParameterName = "TrendFilterPeriod",            Type = ParameterType.Integer, DefaultMin = 0,     DefaultMax = 200,  DefaultStep = 10,    Description = "Trend filter period (0=disabled)" },
            new() { ParameterName = "MinBarsBetweenTrades",         Type = ParameterType.Integer, DefaultMin = 0,     DefaultMax = 20,   DefaultStep = 2,     Description = "Minimum bars between trades" },
            new() { ParameterName = "StopLossPercent",              Type = ParameterType.Decimal, DefaultMin = 0,     DefaultMax = 5,    DefaultStep = 0.5m,  Description = "Stop loss %" },
            new() { ParameterName = "BreakEvenTriggerPercent",      Type = ParameterType.Decimal, DefaultMin = 0,     DefaultMax = 5,    DefaultStep = 0.5m,  Description = "Break-even trigger %" },
        },
    };

    /// <summary>Returns descriptors for all optimizable parameters of the given strategy type.</summary>
    public static List<StrategyParameterDescriptor> GetDescriptors(string strategyType)
        => _registry.TryGetValue(strategyType, out var descriptors)
            ? descriptors
            : new List<StrategyParameterDescriptor>();

    /// <summary>All strategy types that have registered descriptors.</summary>
    public static IReadOnlyCollection<string> KnownStrategyTypes
        => _registry.Keys.ToList().AsReadOnly();
}
