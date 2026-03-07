namespace CryptoResearchTool.Domain.Configuration;

/// <summary>
/// Controls which symbol/timeframe is used when running a historical backtest.
/// </summary>
public enum HistoricalTestMode
{
    /// <summary>
    /// All enabled strategies are tested on the same symbol and timeframe chosen in the UI.
    /// Useful for direct, apples-to-apples comparison between strategies.
    /// </summary>
    Global,

    /// <summary>
    /// Each strategy is tested on its own configured symbol and timeframe.
    /// This is the realistic mode: each strategy runs on the market it was designed for.
    /// </summary>
    PerStrategy
}
