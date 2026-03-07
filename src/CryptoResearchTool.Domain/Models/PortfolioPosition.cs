namespace CryptoResearchTool.Domain.Models;
public class PortfolioPosition
{
    public string Symbol { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public DateTime EntryTime { get; set; }
    public string EntryReason { get; set; } = "";
    public bool IsOpen => Quantity > 0;
    public decimal UnrealizedPnL => IsOpen ? (CurrentPrice - EntryPrice) * Quantity : 0;
    public decimal UnrealizedPnLPercent => IsOpen && EntryPrice > 0 ? ((CurrentPrice - EntryPrice) / EntryPrice) * 100m : 0;
    public decimal MarketValue => Quantity * CurrentPrice;
}
