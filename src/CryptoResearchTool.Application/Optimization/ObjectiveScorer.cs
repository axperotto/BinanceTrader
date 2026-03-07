using CryptoResearchTool.Domain.Optimization;

namespace CryptoResearchTool.Application.Optimization;

/// <summary>
/// Computes a scalar score for a set of backtest metrics given the selected objective.
/// </summary>
public static class ObjectiveScorer
{
    /// <summary>
    /// Computes the per-run score.
    ///
    /// RobustScore formula (default, recommended):
    ///   score  =  returnPct
    ///           - maxDrawdownPct × 1.2          (drawdown penalty)
    ///           + sharpe         × 8            (reward quality of returns)
    ///           + min(profitFactor, 5) × 10     (reward edge; capped to reduce outlier bias)
    ///           - penalty for fewer than 5 trades (−5 per missing trade)
    ///           - penalty for more than 500 trades (−0.05 per excess trade)
    /// </summary>
    public static decimal ComputeScore(OptimizationMetrics metrics, OptimizationObjective objective) =>
        objective switch
        {
            OptimizationObjective.ReturnPercent => metrics.ReturnPct,
            OptimizationObjective.Sharpe        => metrics.SharpeRatio,
            OptimizationObjective.ProfitFactor  => metrics.ProfitFactor,
            OptimizationObjective.RobustScore   => ComputeRobustScore(metrics),
            _                                   => ComputeRobustScore(metrics),
        };

    /// <summary>
    /// Computes the overall ranking score when both training and validation results are present.
    ///
    /// Blending: 40 % training + 60 % validation.
    /// Overfit penalty: when the training score exceeds the validation score by more than 20
    /// points, the excess gap is penalised at 50 % to discourage over-tuned parameters.
    /// When validation is absent the training score is returned unchanged.
    /// </summary>
    public static decimal ComputeOverallScore(
        OptimizationMetrics train,
        OptimizationMetrics? validation,
        OptimizationObjective objective)
    {
        if (validation == null)
            return train.Score;

        decimal blended = train.Score * 0.4m + validation.Score * 0.6m;

        // Penalise significant overfit
        decimal gap = train.Score - validation.Score;
        decimal overfitPenalty = gap > 20m ? (gap - 20m) * 0.5m : 0m;

        return blended - overfitPenalty;
    }

    private static decimal ComputeRobustScore(OptimizationMetrics m)
    {
        var pf = Math.Min(m.ProfitFactor, 5m); // cap to avoid outliers dominating the ranking

        decimal score = m.ReturnPct
                      - m.MaxDrawdownPct * 1.2m
                      + m.SharpeRatio    * 8m
                      + pf               * 10m;

        if (m.Trades < 5)
            score -= (5 - m.Trades) * 5m;       // heavy penalty for almost no trades
        else if (m.Trades > 500)
            score -= (m.Trades - 500) * 0.05m;  // mild churn penalty

        return score;
    }
}
