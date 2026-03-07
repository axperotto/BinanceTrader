namespace CryptoResearchTool.Domain.Models;
public class SimulatedOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string StrategyRunId { get; set; } = "";
    public string Symbol { get; set; } = "";
    public OrderSide Side { get; set; }
    public decimal RequestedPrice { get; set; }
    public decimal ExecutedPrice { get; set; }
    public decimal Quantity { get; set; }
    public decimal FeeAmount { get; set; }
    public DateTime Timestamp { get; set; }
    public string Reason { get; set; } = "";
}
public enum OrderSide { Buy, Sell }
