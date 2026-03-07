namespace CryptoResearchTool.Domain.Models;
public class StrategyMetrics
{
    public string StrategyRunId { get; set; } = "";
    public string StrategyName { get; set; } = "";
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public decimal WinRate => TotalTrades > 0 ? (decimal)WinningTrades / TotalTrades * 100m : 0;
    public decimal NetProfit { get; set; }
    public decimal ReturnPercent { get; set; }
    public decimal ProfitFactor { get; set; }
    public decimal AverageTradePnL { get; set; }
    public decimal Expectancy { get; set; }
    public decimal MaxDrawdown { get; set; }
    public decimal MaxDrawdownPercent { get; set; }
    public decimal CurrentEquity { get; set; }
    public decimal PeakEquity { get; set; }
    public decimal UnrealizedPnL { get; set; }
    public decimal RealizedPnL { get; set; }
    public TimeSpan AverageHoldingTime { get; set; }
    public decimal SharpeRatio { get; set; }
    public int SignalsGenerated { get; set; }
    public int SignalsExecuted { get; set; }
    public DateTime LastUpdated { get; set; }
    public decimal InitialCapital { get; set; }
    public decimal BenchmarkReturn { get; set; }
}
