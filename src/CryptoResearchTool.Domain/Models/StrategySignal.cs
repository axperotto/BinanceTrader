namespace CryptoResearchTool.Domain.Models;
public class StrategySignal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string StrategyName { get; set; } = "";
    public string Symbol { get; set; } = "";
    public SignalType Type { get; set; }
    public decimal Price { get; set; }
    public DateTime Timestamp { get; set; }
    public string Reason { get; set; } = "";
}
