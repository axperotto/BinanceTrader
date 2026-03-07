namespace CryptoResearchTool.Domain.Configuration;
public class StrategyConfiguration
{
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string Timeframe { get; set; } = "1m";
    public bool Enabled { get; set; } = true;
    public Dictionary<string, object> Parameters { get; set; } = new();
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
