using CryptoResearchTool.Application.Interfaces;
using CryptoResearchTool.Domain.Models;
namespace CryptoResearchTool.Application.Services;

public class MetricsCalculator : IMetricsCalculator
{
    public StrategyMetrics Calculate(
        string strategyRunId,
        string strategyName,
        IPortfolioSimulator portfolio,
        List<EquityPoint> equityHistory,
        decimal exposurePercent = 0m)
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
            LastUpdated = DateTime.UtcNow,
            ExposurePercent = exposurePercent
        };

        metrics.RealizedPnL = trades.Sum(t => t.PnL);
        metrics.NetProfit = metrics.RealizedPnL + metrics.UnrealizedPnL;
        metrics.ReturnPercent = portfolio.InitialCapital > 0
            ? (metrics.NetProfit / portfolio.InitialCapital) * 100m
            : 0;

        if (trades.Count > 0)
        {
            metrics.AverageTradePnL = trades.Average(t => t.PnL);

            var winners = trades.Where(t => t.IsWinner).ToList();
            var losers = trades.Where(t => !t.IsWinner).ToList();
            var grossProfit = winners.Sum(t => t.PnL);
            var grossLoss = Math.Abs(losers.Sum(t => t.PnL));

            metrics.AverageWin = winners.Count > 0 ? winners.Average(t => t.PnL) : 0m;
            metrics.AverageLoss = losers.Count > 0 ? Math.Abs(losers.Average(t => t.PnL)) : 0m;
            metrics.ProfitFactor = grossLoss > 0
                ? grossProfit / grossLoss
                : grossProfit > 0 ? 999m : 0m;

            var winRate = (decimal)metrics.WinningTrades / metrics.TotalTrades;
            metrics.Expectancy = (winRate * metrics.AverageWin) - ((1m - winRate) * metrics.AverageLoss);

            var totalSeconds = trades.Sum(t => t.HoldingTime.TotalSeconds);
            metrics.AverageHoldingTime = TimeSpan.FromSeconds(totalSeconds / trades.Count);

            // Win/lose streak calculation
            (metrics.LongestWinStreak, metrics.LongestLoseStreak) = CalculateStreaks(trades);

            // Exit reason breakdown
            metrics.ExitReasonCounts = trades
                .GroupBy(t => t.ExitReasonCategory)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        // Drawdown from equity curve
        if (equityHistory.Count > 1)
        {
            var peak = equityHistory[0].Equity;
            metrics.PeakEquity = peak;
            var maxDd = 0m;
            foreach (var point in equityHistory)
            {
                if (point.Equity > peak) { peak = point.Equity; metrics.PeakEquity = peak; }
                var dd = peak > 0 ? (peak - point.Equity) / peak * 100m : 0m;
                if (dd > maxDd) maxDd = dd;
            }
            metrics.MaxDrawdownPercent = maxDd;
            metrics.MaxDrawdown = metrics.PeakEquity * maxDd / 100m;
        }

        // Sharpe ratio from equity curve returns
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
                var variance = returns.Select(r => (r - avgReturn) * (r - avgReturn)).Average();
                var stdDev = (decimal)Math.Sqrt((double)variance);
                metrics.SharpeRatio = stdDev > 0 ? avgReturn / stdDev : 0m;
            }
        }

        return metrics;
    }

    private static (int winStreak, int loseStreak) CalculateStreaks(List<SimulatedTrade> trades)
    {
        int maxWin = 0, maxLose = 0, curWin = 0, curLose = 0;
        foreach (var t in trades)
        {
            if (t.IsWinner)
            {
                curWin++;
                curLose = 0;
                if (curWin > maxWin) maxWin = curWin;
            }
            else
            {
                curLose++;
                curWin = 0;
                if (curLose > maxLose) maxLose = curLose;
            }
        }
        return (maxWin, maxLose);
    }
}
