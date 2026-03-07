namespace CryptoResearchTool.Domain.Models;

/// <summary>Structured category for the reason a position was closed.</summary>
public static class ExitReasonCategory
{
    public const string StrategySignal = "StrategySignal";
    public const string StopLoss = "StopLoss";
    public const string TakeProfit = "TakeProfit";
    public const string ForcedCloseEndOfTest = "ForcedCloseEndOfTest";
    /// <summary>A staged partial exit triggered when price reached a configured profit level.</summary>
    public const string PartialTakeProfit = "PartialTakeProfit";
    /// <summary>Remaining position closed after the trailing stop was hit.</summary>
    public const string TrailingStop = "TrailingStop";
    /// <summary>Remaining position closed after the break-even stop was hit.</summary>
    public const string BreakEvenStop = "BreakEvenStop";
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
    /// <summary>Structured exit category: StrategySignal, StopLoss, TakeProfit, ForcedCloseEndOfTest,
    /// PartialTakeProfit, TrailingStop, BreakEvenStop.</summary>
    public string ExitReasonCategory { get; set; } = Models.ExitReasonCategory.StrategySignal;

    // ── Partial exit tracking ────────────────────────────────────────────────

    /// <summary>True when this record is a partial exit (position still open with remaining quantity).</summary>
    public bool IsPartialExit { get; set; }

    /// <summary>Zero-based index of which partial take-profit level triggered this exit.</summary>
    public int PartialExitIndex { get; set; }

    /// <summary>Remaining open quantity after this exit event.</summary>
    public decimal RemainingQuantityAfter { get; set; }

    /// <summary>
    /// Human-readable management reason for this exit event
    /// (e.g. "PartialTP_1", "TrailingStop", "BreakEven").
    /// </summary>
    public string ManagementReason { get; set; } = "";

    public bool IsWinner => PnL > 0;
}
