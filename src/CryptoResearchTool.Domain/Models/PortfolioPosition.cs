namespace CryptoResearchTool.Domain.Models;
public class PortfolioPosition
{
    public string Symbol { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public DateTime EntryTime { get; set; }
    public string EntryReason { get; set; } = "";
    /// <summary>Fee paid on entry, tracked for complete trade cost calculation.</summary>
    public decimal EntryFee { get; set; }
    /// <summary>Cumulative slippage impact tracked for reporting.</summary>
    public decimal SlippageImpact { get; set; }

    // ── Advanced trade management tracking ──────────────────────────────────

    /// <summary>
    /// Quantity purchased at entry. Remains fixed; use <see cref="Quantity"/> for the
    /// remaining (still-open) quantity.
    /// </summary>
    public decimal InitialQuantity { get; set; }

    /// <summary>Cumulative realized PnL (net of fees) from partial exits on this position.</summary>
    public decimal RealizedPnL { get; set; }

    /// <summary>
    /// Current protective stop price. Starts at the initial stop-loss level and is
    /// moved upward by break-even / trailing-stop logic. 0 = no stop active.
    /// </summary>
    public decimal StopLossPrice { get; set; }

    /// <summary>True when the protective stop has been moved to break-even or above.</summary>
    public bool BreakEvenActivated { get; set; }

    /// <summary>Current trailing stop price. 0 = trailing stop not yet active.</summary>
    public decimal TrailingStopPrice { get; set; }

    /// <summary>True when the trailing stop has been activated.</summary>
    public bool TrailingStopActive { get; set; }

    /// <summary>
    /// Highest intra-bar price (candle High) seen since entry.
    /// Used to compute the trailing stop level.
    /// </summary>
    public decimal HighestPriceSinceEntry { get; set; }

    /// <summary>Lowest intra-bar price (candle Low) seen since entry (extensibility).</summary>
    public decimal LowestPriceSinceEntry { get; set; }

    /// <summary>Number of partial take-profit targets already executed.</summary>
    public int PartialTargetsHit { get; set; }

    /// <summary>
    /// Weighted average exit price across all partial exits so far.
    /// 0 when no partial exits have been executed yet.
    /// </summary>
    public decimal AverageExitPrice { get; set; }

    /// <summary>Bar index at which the last management action was taken (-1 = none yet).</summary>
    public int LastManagementBarIndex { get; set; } = -1;

    /// <summary>Current trade management lifecycle state.</summary>
    public PositionManagementState ManagementState { get; set; } = PositionManagementState.Flat;

    // ── Computed properties ──────────────────────────────────────────────────

    public bool IsOpen => Quantity > 0;
    public decimal UnrealizedPnL => IsOpen ? (CurrentPrice - EntryPrice) * Quantity : 0;
    public decimal UnrealizedPnLPercent => IsOpen && EntryPrice > 0 ? ((CurrentPrice - EntryPrice) / EntryPrice) * 100m : 0;
    public decimal MarketValue => Quantity * CurrentPrice;
}
