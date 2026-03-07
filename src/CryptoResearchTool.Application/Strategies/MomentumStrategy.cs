using CryptoResearchTool.Domain.Configuration;
using CryptoResearchTool.Domain.Models;
namespace CryptoResearchTool.Application.Strategies;

public class MomentumStrategy : BaseStrategy
{
    private readonly int _lookbackBars;
    private readonly decimal _entryThresholdPercent;
    private readonly decimal _exitThresholdPercent;
    // TrendFilterPeriod: when > 0, buy signals are only generated when the latest close
    // is above the trend MA. 0 = disabled.
    private readonly int _trendFilterPeriod;
    // ConfirmationBars: number of consecutive bars that must show positive momentum
    // above the entry threshold before a buy is triggered. 1 = no confirmation needed.
    private readonly int _confirmationBars;

    private int _consecutiveBullishBars = 0;
    private bool _inPosition = false;

    public override string Name { get; }

    public MomentumStrategy(StrategyConfiguration config, SimulationConfiguration simConfig)
        : base(config.Symbol, config.Timeframe, simConfig)
    {
        Name = config.Name;
        _lookbackBars = config.GetParameter("LookbackBars", 10);
        _entryThresholdPercent = config.GetParameter("EntryThresholdPercent", 1.0m);
        _exitThresholdPercent = config.GetParameter("ExitThresholdPercent", 0.0m);
        _trendFilterPeriod = config.GetParameter("TrendFilterPeriod", 0);
        _confirmationBars = Math.Max(1, config.GetParameter("ConfirmationBars", 1));
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
        if (CandleHistory.Count < _lookbackBars + 1) return null;

        var bars = CandleHistory.TakeLast(_lookbackBars + 1).ToList();
        var firstClose = bars.First().Close;
        var lastClose = bars.Last().Close;
        if (firstClose <= 0) return null;

        var momentum = ((lastClose - firstClose) / firstClose) * 100m;
        StrategySignal? signal = null;

        if (!_inPosition)
        {
            if (momentum > _entryThresholdPercent)
            {
                _consecutiveBullishBars++;
                if (_consecutiveBullishBars >= _confirmationBars && IsTrendBullish())
                {
                    _inPosition = true;
                    _consecutiveBullishBars = 0;
                    signal = CreateSignal(SignalType.Buy, lastClose, $"Momentum({momentum:F2}%)");
                }
            }
            else
            {
                _consecutiveBullishBars = 0;
            }
        }
        else
        {
            // Exit when momentum falls below the exit threshold (negative or configurable)
            if (momentum < _exitThresholdPercent)
            {
                _inPosition = false;
                signal = CreateSignal(SignalType.Sell, lastClose, $"MomentumExit({momentum:F2}%)");
            }
        }

        if (signal != null) _lastSignal = signal;
        return signal;
    }
}
