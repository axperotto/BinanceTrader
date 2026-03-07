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
    }
}
