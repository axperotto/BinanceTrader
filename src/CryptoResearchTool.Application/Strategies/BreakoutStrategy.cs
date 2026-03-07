using CryptoResearchTool.Domain.Configuration;
using CryptoResearchTool.Domain.Models;
namespace CryptoResearchTool.Application.Strategies;

/// <summary>
/// Breakout strategy: buys when the close exceeds the N-bar highest high by a threshold
/// percentage, optionally requiring multiple confirmation bars and a trend filter.
/// Exits when the close falls back below the channel high (configurable).
///
/// Parameters:
/// - LookbackBars (default 20): number of bars used to define the price channel.
/// - BreakoutThresholdPercent (default 0.1): how far above the channel high the close must be.
/// - TrendFilterPeriod (default 0): when > 0, buy only when close is above this MA. 0 = disabled.
/// - ConfirmationBars (default 1): consecutive bars above the breakout level required before entry.
/// - ExitOnCloseBelowChannel (default true): sell when close drops back below channel high at entry.
/// </summary>
public class BreakoutStrategy : BaseStrategy
{
    private readonly int _lookbackBars;
    private readonly decimal _breakoutThresholdPercent;
    private readonly int _trendFilterPeriod;
    private readonly int _confirmationBars;
    private readonly bool _exitOnCloseBelowChannel;

    private bool _inBreakout = false;
    private decimal _channelHighAtEntry = 0m;
    private int _confirmationCount = 0;

    public override string Name { get; }

    public BreakoutStrategy(StrategyConfiguration config, SimulationConfiguration simConfig)
        : base(config.Symbol, config.Timeframe, simConfig)
    {
        Name = config.Name;
        _lookbackBars = config.GetParameter("LookbackBars", 20);
        _breakoutThresholdPercent = config.GetParameter("BreakoutThresholdPercent", 0.1m);
        _trendFilterPeriod = config.GetParameter("TrendFilterPeriod", 0);
        _confirmationBars = Math.Max(1, config.GetParameter("ConfirmationBars", 1));
        _exitOnCloseBelowChannel = config.GetParameter("ExitOnCloseBelowChannel", true);
    }

    private bool IsTrendBullish()
    {
        if (_trendFilterPeriod <= 0 || CandleHistory.Count < _trendFilterPeriod) return true;
        var closes = CandleHistory.Select(c => c.Close).ToList();
        var trendMa = closes.TakeLast(_trendFilterPeriod).Average();
        return closes.Last() >= trendMa;
    }

    public override StrategySignal? Evaluate()
    {
        // Warmup: need lookback bars + 1 (the current bar) to evaluate
        if (CandleHistory.Count < _lookbackBars + 1) return null;

        var lookback = CandleHistory.TakeLast(_lookbackBars + 1).ToList();
        // Previous N bars (not including the current/last one)
        var prevBars = lookback.Take(_lookbackBars).ToList();
        var channelHigh = prevBars.Max(c => c.High);
        var lastClose = lookback.Last().Close;
        var breakoutLevel = channelHigh * (1m + _breakoutThresholdPercent / 100m);

        StrategySignal? signal = null;

        if (!_inBreakout)
        {
            if (lastClose > breakoutLevel && IsTrendBullish())
            {
                _confirmationCount++;
                if (_confirmationCount >= _confirmationBars)
                {
                    _inBreakout = true;
                    _channelHighAtEntry = channelHigh;
                    _confirmationCount = 0;
                    signal = CreateSignal(SignalType.Buy, lastClose,
                        $"Breakout(close={lastClose:F2} > level={breakoutLevel:F2})");
                }
            }
            else
            {
                _confirmationCount = 0;
            }
        }
        else
        {
            // Exit: close falls back below the channel high recorded at entry
            if (_exitOnCloseBelowChannel && lastClose < _channelHighAtEntry)
            {
                var channelAtEntry = _channelHighAtEntry;
                _inBreakout = false;
                _channelHighAtEntry = 0m;
                signal = CreateSignal(SignalType.Sell, lastClose,
                    $"BreakoutFailed(close={lastClose:F2} < channel={channelAtEntry:F2})");
            }
        }

        if (signal != null) _lastSignal = signal;
        return signal;
    }
}
