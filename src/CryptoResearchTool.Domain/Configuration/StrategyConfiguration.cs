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
}
