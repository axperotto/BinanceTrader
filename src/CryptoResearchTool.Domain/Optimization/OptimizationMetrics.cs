namespace CryptoResearchTool.Domain.Optimization;

/// <summary>Metrics produced by a single backtest run inside the optimizer.</summary>
public class OptimizationMetrics
{
    public decimal ReturnPct { get; set; }
    public decimal NetPnL { get; set; }
    public decimal MaxDrawdownPct { get; set; }
    public decimal SharpeRatio { get; set; }
    public decimal ProfitFactor { get; set; }
    public int Trades { get; set; }
    public decimal WinRate { get; set; }

    /// <summary>Composite score computed by the selected objective function.</summary>
    public decimal Score { get; set; }
}
