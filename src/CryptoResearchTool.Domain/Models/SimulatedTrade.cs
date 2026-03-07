namespace CryptoResearchTool.Domain.Models;
public class SimulatedTrade
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string StrategyRunId { get; set; } = "";
    public string Symbol { get; set; } = "";
    public decimal EntryPrice { get; set; }
    public decimal ExitPrice { get; set; }
    public decimal Quantity { get; set; }
    public decimal PnL { get; set; }
    public decimal PnLPercent { get; set; }
    public decimal TotalFees { get; set; }
    public DateTime EntryTime { get; set; }
    public DateTime ExitTime { get; set; }
    public TimeSpan HoldingTime { get; set; }
    public string EntryReason { get; set; } = "";
    public string ExitReason { get; set; } = "";
    public bool IsWinner => PnL > 0;
}
