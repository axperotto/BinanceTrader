namespace CryptoResearchTool.Domain.Models;

/// <summary>Structured category for the reason a position was closed.</summary>
public static class ExitReasonCategory
{
    public const string StrategySignal = "StrategySignal";
    public const string StopLoss = "StopLoss";
    public const string TakeProfit = "TakeProfit";
    public const string ForcedCloseEndOfTest = "ForcedCloseEndOfTest";
}

public class SimulatedTrade
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string StrategyRunId { get; set; } = "";
    public string Symbol { get; set; } = "";
    public decimal EntryPrice { get; set; }
    public decimal ExitPrice { get; set; }
    public decimal Quantity { get; set; }
    /// <summary>Gross PnL before fees.</summary>
    public decimal GrossPnL { get; set; }
    /// <summary>Net PnL after all fees and slippage.</summary>
    public decimal PnL { get; set; }
    public decimal PnLPercent { get; set; }
    public decimal TotalFees { get; set; }
    public decimal SlippageImpact { get; set; }
    public DateTime EntryTime { get; set; }
    public DateTime ExitTime { get; set; }
    public TimeSpan HoldingTime { get; set; }
    public string EntryReason { get; set; } = "";
    public string ExitReason { get; set; } = "";
    /// <summary>Structured exit category: StrategySignal, StopLoss, TakeProfit, ForcedCloseEndOfTest.</summary>
    public string ExitReasonCategory { get; set; } = Models.ExitReasonCategory.StrategySignal;
    public bool IsWinner => PnL > 0;
}
