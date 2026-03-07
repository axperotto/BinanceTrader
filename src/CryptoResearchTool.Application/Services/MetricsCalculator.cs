using CryptoResearchTool.Application.Interfaces;
using CryptoResearchTool.Domain.Models;
namespace CryptoResearchTool.Application.Services;

public class MetricsCalculator : IMetricsCalculator
{
    public StrategyMetrics Calculate(string strategyRunId, string strategyName, IPortfolioSimulator portfolio, List<EquityPoint> equityHistory)
    {
        var trades = portfolio.CompletedTrades;
        var metrics = new StrategyMetrics
        {
            StrategyRunId = strategyRunId,
            StrategyName = strategyName,
            InitialCapital = portfolio.InitialCapital,
            CurrentEquity = portfolio.GetEquity(),
            UnrealizedPnL = portfolio.OpenPosition?.UnrealizedPnL ?? 0m,
            TotalTrades = trades.Count,
            WinningTrades = trades.Count(t => t.IsWinner),
            LosingTrades = trades.Count(t => !t.IsWinner),
            LastUpdated = DateTime.UtcNow
        };

        metrics.RealizedPnL = trades.Sum(t => t.PnL);
        metrics.NetProfit = metrics.RealizedPnL + metrics.UnrealizedPnL;
        metrics.ReturnPercent = portfolio.InitialCapital > 0 ? (metrics.NetProfit / portfolio.InitialCapital) * 100m : 0;

        if (trades.Count > 0)
        {
            metrics.AverageTradePnL = trades.Average(t => t.PnL);
            var grossProfit = trades.Where(t => t.IsWinner).Sum(t => t.PnL);
            var grossLoss = Math.Abs(trades.Where(t => !t.IsWinner).Sum(t => t.PnL));
            metrics.ProfitFactor = grossLoss > 0 ? grossProfit / grossLoss : grossProfit > 0 ? 999m : 0;
            var avgWin = metrics.WinningTrades > 0 ? trades.Where(t => t.IsWinner).Average(t => t.PnL) : 0;
            var avgLoss = metrics.LosingTrades > 0 ? Math.Abs(trades.Where(t => !t.IsWinner).Average(t => t.PnL)) : 0;
            var winRate = metrics.TotalTrades > 0 ? (decimal)metrics.WinningTrades / metrics.TotalTrades : 0;
            metrics.Expectancy = (winRate * avgWin) - ((1 - winRate) * avgLoss);
            var totalSeconds = trades.Sum(t => t.HoldingTime.TotalSeconds);
            metrics.AverageHoldingTime = TimeSpan.FromSeconds(totalSeconds / trades.Count);
        }

        if (equityHistory.Count > 1)
        {
            var peak = equityHistory[0].Equity;
            metrics.PeakEquity = peak;
            var maxDd = 0m;
            foreach (var point in equityHistory)
            {
                if (point.Equity > peak) { peak = point.Equity; metrics.PeakEquity = peak; }
                var dd = peak > 0 ? (peak - point.Equity) / peak * 100m : 0;
                if (dd > maxDd) maxDd = dd;
            }
            metrics.MaxDrawdownPercent = maxDd;
            metrics.MaxDrawdown = metrics.PeakEquity * maxDd / 100m;
        }

        if (equityHistory.Count > 2)
        {
            var returns = new List<decimal>();
            for (int i = 1; i < equityHistory.Count; i++)
            {
                if (equityHistory[i - 1].Equity > 0)
                    returns.Add((equityHistory[i].Equity - equityHistory[i - 1].Equity) / equityHistory[i - 1].Equity);
            }
            if (returns.Count > 1)
            {
                var avgReturn = returns.Average();
                var stdDev = (decimal)Math.Sqrt((double)returns.Select(r => (r - avgReturn) * (r - avgReturn)).Average());
                metrics.SharpeRatio = stdDev > 0 ? avgReturn / stdDev : 0;
            }
        }

        return metrics;
    }
}
