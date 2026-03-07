namespace CryptoResearchTool.Domain.Models;
public class EquityPoint
{
    public DateTime Timestamp { get; set; }
    public decimal Equity { get; set; }
    public decimal Cash { get; set; }
    public decimal UnrealizedPnL { get; set; }
}
