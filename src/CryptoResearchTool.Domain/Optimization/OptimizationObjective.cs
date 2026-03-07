namespace CryptoResearchTool.Domain.Optimization;

public enum OptimizationObjective
{
    ReturnPercent,
    Sharpe,
    ProfitFactor,
    /// <summary>
    /// Composite score that balances return, drawdown, Sharpe, and profit factor.
    /// This is the default and recommended objective as it resists overfitting.
    /// </summary>
    RobustScore
}

public enum OptimizationSearchMode
{
    GridSearch,
    RandomSearch
}
