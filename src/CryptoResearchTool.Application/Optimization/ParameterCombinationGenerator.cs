using CryptoResearchTool.Domain.Optimization;

namespace CryptoResearchTool.Application.Optimization;

/// <summary>
/// Generates parameter combinations for grid search and random search optimization modes.
/// </summary>
public static class ParameterCombinationGenerator
{
    /// <summary>
    /// Estimates how many combinations grid search would produce (before validation filters).
    /// Returns <see cref="long.MaxValue"/> on integer overflow.
    /// </summary>
    public static long EstimateGridSearchCount(IEnumerable<StrategyParameterRange> ranges)
    {
        long count = 1;
        foreach (var r in ranges.Where(r => r.IsEnabled && r.Step > 0))
        {
            long steps = (long)Math.Floor((r.MaxValue - r.MinValue) / r.Step) + 1;
            if (steps <= 0) steps = 1;
            try { checked { count *= steps; } }
            catch (OverflowException) { return long.MaxValue; }
        }
        return count;
    }

    /// <summary>
    /// Generates all grid-search combinations from the enabled ranges.
    /// Stops once <paramref name="maxCombinations"/> valid combinations have been collected.
    /// Invalid combinations (strategy-specific constraint violations) are skipped silently.
    /// </summary>
    public static List<Dictionary<string, decimal>> GenerateGrid(
        IList<StrategyParameterRange> ranges,
        int maxCombinations,
        string strategyType = "")
    {
        var enabled = ranges.Where(r => r.IsEnabled && r.Step > 0).ToList();
        var result = new List<Dictionary<string, decimal>>();
        GenerateGridRecursive(enabled, 0, new Dictionary<string, decimal>(), result, maxCombinations, strategyType);
        return result;
    }

    private static void GenerateGridRecursive(
        IList<StrategyParameterRange> ranges,
        int index,
        Dictionary<string, decimal> current,
        List<Dictionary<string, decimal>> result,
        int maxCombinations,
        string strategyType)
    {
        if (result.Count >= maxCombinations) return;

        if (index == ranges.Count)
        {
            if (IsValidCombination(current, strategyType))
                result.Add(new Dictionary<string, decimal>(current));
            return;
        }

        var range = ranges[index];
        var value = range.MinValue;
        // Add a small epsilon to MaxValue to include the boundary reliably with floating-point step arithmetic
        while (value <= range.MaxValue + range.Step * 0.001m)
        {
            if (result.Count >= maxCombinations) break;
            var clamped = Math.Min(value, range.MaxValue);
            current[range.ParameterName] = range.IsInteger ? Math.Round(clamped, 0) : Math.Round(clamped, 4);
            GenerateGridRecursive(ranges, index + 1, current, result, maxCombinations, strategyType);
            value += range.Step;
        }
        current.Remove(range.ParameterName);
    }

    /// <summary>
    /// Generates random combinations sampled uniformly within the enabled parameter ranges.
    /// Duplicate combinations are de-duplicated on a best-effort basis.
    /// </summary>
    public static List<Dictionary<string, decimal>> GenerateRandom(
        IList<StrategyParameterRange> ranges,
        int sampleCount,
        string strategyType = "")
    {
        var enabled = ranges.Where(r => r.IsEnabled && r.Step > 0).ToList();
        var result = new List<Dictionary<string, decimal>>();
        var seen = new HashSet<string>();
        var rng = new Random();
        int maxAttempts = sampleCount * 20;
        int attempts = 0;

        while (result.Count < sampleCount && attempts < maxAttempts)
        {
            attempts++;
            var combo = new Dictionary<string, decimal>();
            foreach (var range in enabled)
            {
                long steps = (long)Math.Floor((range.MaxValue - range.MinValue) / range.Step);
                if (steps < 0) steps = 0;
                long pick = (long)(rng.NextDouble() * (steps + 1));
                var value = range.MinValue + pick * range.Step;
                value = Math.Min(value, range.MaxValue);
                combo[range.ParameterName] = range.IsInteger ? Math.Round(value, 0) : Math.Round(value, 4);
            }

            if (!IsValidCombination(combo, strategyType)) continue;

            var key = string.Join("|", combo.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));
            if (seen.Add(key))
                result.Add(combo);
        }

        return result;
    }

    /// <summary>
    /// Validates that a parameter combination is logically consistent.
    /// Returns false for nonsensical configurations that should be skipped.
    /// </summary>
    public static bool IsValidCombination(Dictionary<string, decimal> p, string strategyType)
    {
        // FastPeriod must be strictly less than SlowPeriod
        if (p.TryGetValue("FastPeriod", out var fast) && p.TryGetValue("SlowPeriod", out var slow))
            if (fast >= slow) return false;

        // TrendFilterPeriod (when > 0) must be greater than SlowPeriod to form a meaningful hierarchy
        if (p.TryGetValue("TrendFilterPeriod", out var trend) && trend > 0)
            if (p.TryGetValue("SlowPeriod", out var sp) && trend <= sp) return false;

        // RSI: Oversold must be strictly less than Overbought
        if (p.TryGetValue("Oversold", out var os) && p.TryGetValue("Overbought", out var ob))
            if (os >= ob) return false;

        // Trailing stop: distance requires activation to be set
        if (p.TryGetValue("TrailingStopDistancePercent", out var tsDist) && tsDist > 0)
            if (p.TryGetValue("TrailingStopActivationPercent", out var tsAct) && tsAct <= 0)
                return false;

        // BreakEven trigger must be above the stop loss level (both > 0)
        if (p.TryGetValue("BreakEvenTriggerPercent", out var be) && be > 0)
            if (p.TryGetValue("StopLossPercent", out var sl) && sl > 0 && be <= sl)
                return false;

        return true;
    }
}
