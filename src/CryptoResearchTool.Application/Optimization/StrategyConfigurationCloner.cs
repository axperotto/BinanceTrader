using CryptoResearchTool.Domain.Configuration;

namespace CryptoResearchTool.Application.Optimization;

/// <summary>
/// Creates a deep clone of a <see cref="StrategyConfiguration"/> and injects
/// optimizer-supplied parameter values without mutating the original.
/// </summary>
public static class StrategyConfigurationCloner
{
    // Parameters that live as first-class top-level properties on StrategyConfiguration
    // (not in the Parameters dictionary). These must be applied to the appropriate property.
    private static readonly HashSet<string> _topLevelParams = new(StringComparer.OrdinalIgnoreCase)
    {
        "StopLossPercent",
        "TakeProfitPercent",
        "MinBarsBetweenTrades",
        "BreakEvenTriggerPercent",
        "BreakEvenOffsetPercent",
        "TrailingStopActivationPercent",
        "TrailingStopDistancePercent",
    };

    /// <summary>
    /// Returns a new <see cref="StrategyConfiguration"/> that is a deep copy of
    /// <paramref name="source"/> with <paramref name="parameterValues"/> applied.
    /// The clone uses the specified <paramref name="symbol"/> and <paramref name="timeframe"/>.
    /// </summary>
    public static StrategyConfiguration CloneAndInject(
        StrategyConfiguration source,
        Dictionary<string, decimal> parameterValues,
        string symbol,
        string timeframe)
    {
        var clone = new StrategyConfiguration
        {
            Type                          = source.Type,
            Name                          = source.Name,
            Symbol                        = symbol,
            Timeframe                     = timeframe,
            Enabled                       = true,
            Parameters                    = new Dictionary<string, object>(source.Parameters),
            StopLossPercent               = source.StopLossPercent,
            TakeProfitPercent             = source.TakeProfitPercent,
            MinBarsBetweenTrades          = source.MinBarsBetweenTrades,
            BreakEvenTriggerPercent       = source.BreakEvenTriggerPercent,
            BreakEvenOffsetPercent        = source.BreakEvenOffsetPercent,
            TrailingStopActivationPercent = source.TrailingStopActivationPercent,
            TrailingStopDistancePercent   = source.TrailingStopDistancePercent,
            PartialTakeProfitLevelsPercent = new List<decimal>(source.PartialTakeProfitLevelsPercent),
            PartialTakeProfitExitPercent  = new List<decimal>(source.PartialTakeProfitExitPercent),
            AllowFinalSignalExit          = source.AllowFinalSignalExit,
            AllowTrendInvalidationExit    = source.AllowTrendInvalidationExit,
        };

        // Apply optimizer-supplied values: route to top-level property or Parameters dict
        foreach (var (name, value) in parameterValues)
        {
            if (_topLevelParams.Contains(name))
                ApplyTopLevel(clone, name, value);
            else
                clone.Parameters[name] = value; // stored as decimal; GetParameter<T> handles conversion
        }

        clone.Normalize();
        return clone;
    }

    /// <summary>
    /// Copies all optimizer-selected parameter values back onto an existing strategy
    /// configuration in-place (used when the user clicks "Apply Selected Result").
    /// </summary>
    public static void ApplyParameters(
        StrategyConfiguration target,
        Dictionary<string, decimal> parameterValues)
    {
        foreach (var (name, value) in parameterValues)
        {
            if (_topLevelParams.Contains(name))
                ApplyTopLevel(target, name, value);
            else
                target.Parameters[name] = value;
        }
        target.Normalize();
    }

    private static void ApplyTopLevel(StrategyConfiguration cfg, string name, decimal value)
    {
        switch (name)
        {
            case "StopLossPercent":               cfg.StopLossPercent               = value; break;
            case "TakeProfitPercent":             cfg.TakeProfitPercent             = value; break;
            case "MinBarsBetweenTrades":          cfg.MinBarsBetweenTrades          = (int)value; break;
            case "BreakEvenTriggerPercent":       cfg.BreakEvenTriggerPercent       = value; break;
            case "BreakEvenOffsetPercent":        cfg.BreakEvenOffsetPercent        = value; break;
            case "TrailingStopActivationPercent": cfg.TrailingStopActivationPercent = value; break;
            case "TrailingStopDistancePercent":   cfg.TrailingStopDistancePercent   = value; break;
        }
    }
}
