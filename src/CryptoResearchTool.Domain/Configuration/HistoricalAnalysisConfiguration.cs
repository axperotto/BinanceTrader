namespace CryptoResearchTool.Domain.Configuration;

public class HistoricalAnalysisConfiguration
{
    public string Symbol { get; set; } = "BTCUSDT";
    public string Timeframe { get; set; } = "1h";
    public DateTime StartDate { get; set; } = DateTime.UtcNow.AddMonths(-3);
    public DateTime EndDate { get; set; } = DateTime.UtcNow;
    public decimal InitialCapital { get; set; } = 1000m;
    public decimal FeePercent { get; set; } = 0.1m;
    public decimal SlippagePercent { get; set; } = 0.05m;
    public bool UseLocalCache { get; set; } = true;
    public bool ForceRefresh { get; set; } = false;
    public string CacheDirectory { get; set; } = "data/historical_cache";
}
