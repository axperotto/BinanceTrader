using CryptoResearchTool.Domain.Configuration;
using CryptoResearchTool.Domain.Models;
namespace CryptoResearchTool.Application.Strategies;

/// <summary>
/// Breakout strategy: buys when the close exceeds the N-bar highest high by a threshold percentage.
/// Exits when the close falls back below the previous channel high, invalidating the breakout.
///
/// Fix notes vs. original:
/// - Added exit signal: sell when close drops back below the channel high (breakout invalidation)
/// - Reduced default BreakoutThresholdPercent from 0.5 to 0.1 to generate more signals on typical data
/// - Warmup: requires LookbackBars+1 closed candles before any signal is evaluated
/// </summary>
public class BreakoutStrategy : BaseStrategy
{
    private readonly int _lookbackBars;
    private readonly decimal _breakoutThresholdPercent;
    private bool _inBreakout = false;
    private decimal _channelHighAtEntry = 0m;

    public override string Name { get; }

    public BreakoutStrategy(StrategyConfiguration config, SimulationConfiguration simConfig)
        : base(config.Symbol, config.Timeframe, simConfig)
    {
        Name = config.Name;
        _lookbackBars = config.GetParameter("LookbackBars", 20);
        // Default threshold reduced to 0.1% so standard price action can generate signals
        _breakoutThresholdPercent = config.GetParameter("BreakoutThresholdPercent", 0.1m);
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
            // Entry: close breaks above channel high + threshold
            if (lastClose > breakoutLevel)
            {
                _inBreakout = true;
                _channelHighAtEntry = channelHigh;
                signal = CreateSignal(SignalType.Buy, lastClose,
                    $"Breakout(close={lastClose:F2} > level={breakoutLevel:F2})");
            }
        }
        else
        {
            // Exit: close falls back below the channel high that was in place at entry,
            // indicating the breakout has failed / reversed
            if (lastClose < _channelHighAtEntry)
            {
                var channelAtEntry = _channelHighAtEntry; // capture before resetting
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
