namespace CryptoResearchTool.Domain.Models;
public class StrategyRuntimeState
{
    public string StrategyRunId { get; set; } = "";
    public string StrategyName { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string Timeframe { get; set; } = "";
    public bool IsRunning { get; set; }
    public decimal Cash { get; set; }
    public decimal InitialCapital { get; set; }
    public PortfolioPosition? OpenPosition { get; set; }
    public StrategyMetrics Metrics { get; set; } = new();
    public StrategySignal? LastSignal { get; set; }
    public decimal LastPrice { get; set; }
    public DateTime LastUpdate { get; set; }
    public string StatusMessage { get; set; } = "";
}
