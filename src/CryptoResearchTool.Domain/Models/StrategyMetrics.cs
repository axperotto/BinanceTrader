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
    /// <summary>Average PnL of winning trades.</summary>
    public decimal AverageWin { get; set; }
    /// <summary>Average absolute PnL of losing trades.</summary>
    public decimal AverageLoss { get; set; }
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
    /// <summary>Return of the Buy&amp;Hold benchmark over the same period.</summary>
    public decimal BenchmarkReturn { get; set; }
    /// <summary>Percentage of bars where a position was open.</summary>
    public decimal ExposurePercent { get; set; }
    /// <summary>Longest consecutive winning trade streak.</summary>
    public int LongestWinStreak { get; set; }
    /// <summary>Longest consecutive losing trade streak.</summary>
    public int LongestLoseStreak { get; set; }
    /// <summary>Breakdown of trade count by exit reason.</summary>
    public Dictionary<string, int> ExitReasonCounts { get; set; } = new();
}
