namespace CryptoResearchTool.Domain.Configuration;
public class StrategyConfiguration
{
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string Timeframe { get; set; } = "1m";
    public bool Enabled { get; set; } = true;
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>Stop loss threshold as a percentage below entry price. 0 = disabled.</summary>
    public decimal StopLossPercent { get; set; } = 0m;

    /// <summary>Take profit threshold as a percentage above entry price. 0 = disabled.</summary>
    public decimal TakeProfitPercent { get; set; } = 0m;

    /// <summary>
    /// Minimum number of closed bars to wait after a trade closes before allowing a new entry.
    /// 0 = no cooldown.
    /// </summary>
    public int MinBarsBetweenTrades { get; set; } = 0;

    // ── Advanced trade management ────────────────────────────────────────────

    /// <summary>
    /// Profit percentage above entry at which the protective stop is moved to break-even.
    /// 0 = disabled.
    /// </summary>
    public decimal BreakEvenTriggerPercent { get; set; } = 0m;

    /// <summary>
    /// Additional percentage offset above entry when setting the break-even stop
    /// (e.g. 0.2 = entry + 0.2%). Only used when BreakEvenTriggerPercent > 0.
    /// </summary>
    public decimal BreakEvenOffsetPercent { get; set; } = 0m;

    /// <summary>
    /// Profit percentage above entry at which the trailing stop activates. 0 = disabled.
    /// </summary>
    public decimal TrailingStopActivationPercent { get; set; } = 0m;

    /// <summary>
    /// Distance below the highest-since-entry price for the trailing stop (as a percentage).
    /// Requires TrailingStopActivationPercent to be set. 0 = disabled.
    /// </summary>
    public decimal TrailingStopDistancePercent { get; set; } = 0m;

    /// <summary>
    /// Profit percentage levels at which staged partial exits are executed
    /// (e.g. [2.0, 4.0, 7.0]). Must match the length of PartialTakeProfitExitPercent.
    /// </summary>
    public List<decimal> PartialTakeProfitLevelsPercent { get; set; } = new();

    /// <summary>
    /// Percentage of the ORIGINAL position to exit at each partial level
    /// (e.g. [25.0, 25.0, 25.0]). Must match the length of PartialTakeProfitLevelsPercent.
    /// The sum should not exceed 100; any remainder stays open for trailing / final exit.
    /// </summary>
    public List<decimal> PartialTakeProfitExitPercent { get; set; } = new();

    /// <summary>
    /// When true, a strategy sell signal can close the remaining open position even when
    /// partial exits are configured. Default: true.
    /// </summary>
    public bool AllowFinalSignalExit { get; set; } = true;

    /// <summary>
    /// When true, a trend-invalidation signal from the strategy closes the remaining position.
    /// Default: true.
    /// </summary>
    public bool AllowTrendInvalidationExit { get; set; } = true;

    public T GetParameter<T>(string key, T defaultValue)
    {
        if (Parameters.TryGetValue(key, out var val))
        {
            try { return (T)Convert.ChangeType(val, typeof(T)); }
            catch { }
        }
        return defaultValue;
    }

    /// <summary>
    /// Normalizes the configuration by promoting legacy parameter values stored inside
    /// <see cref="Parameters"/> to the corresponding top-level properties when the
    /// top-level property is still at its default (zero) value.
    /// This provides backward compatibility with strategies.json files that embed
    /// StopLossPercent / TakeProfitPercent / MinBarsBetweenTrades inside Parameters.
    /// </summary>
    public void Normalize()
    {
        if (StopLossPercent == 0 && Parameters.TryGetValue("StopLossPercent", out var sl))
        {
            // Best-effort conversion: mirrors the same pattern as GetParameter<T>.
            // Invalid values are silently skipped to avoid crashing on malformed config.
            try { StopLossPercent = Convert.ToDecimal(sl); } catch (Exception) { }
        }
        if (TakeProfitPercent == 0 && Parameters.TryGetValue("TakeProfitPercent", out var tp))
        {
            try { TakeProfitPercent = Convert.ToDecimal(tp); } catch (Exception) { }
        }
        if (MinBarsBetweenTrades == 0 && Parameters.TryGetValue("MinBarsBetweenTrades", out var mbt))
        {
            try { MinBarsBetweenTrades = Convert.ToInt32(mbt); } catch (Exception) { }
        }
        // Validate partial take profit arrays: both lists must have the same length
        // and the exit percentages must not exceed 100 % in total.
        if (PartialTakeProfitLevelsPercent.Count != PartialTakeProfitExitPercent.Count)
        {
            PartialTakeProfitLevelsPercent.Clear();
            PartialTakeProfitExitPercent.Clear();
        }
        if (PartialTakeProfitExitPercent.Count > 0 && PartialTakeProfitExitPercent.Sum() > 100m)
        {
            // Exit percentages cannot exceed 100% of the original position.
            // A sum of exactly 100% is valid (all profit taken via partial exits; nothing left for trailing).
            PartialTakeProfitLevelsPercent.Clear();
            PartialTakeProfitExitPercent.Clear();
        }
        // Sort levels in ascending order (required for stage-by-stage evaluation).
        if (PartialTakeProfitLevelsPercent.Count > 1)
        {
            var pairs = PartialTakeProfitLevelsPercent
                .Zip(PartialTakeProfitExitPercent, (l, e) => (Level: l, Exit: e))
                .OrderBy(p => p.Level)
                .ToList();
            PartialTakeProfitLevelsPercent = pairs.Select(p => p.Level).ToList();
            PartialTakeProfitExitPercent = pairs.Select(p => p.Exit).ToList();
        }
    }
}
