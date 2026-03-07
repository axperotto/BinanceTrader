using CryptoResearchTool.Domain.Configuration;
using CryptoResearchTool.Domain.Models;
namespace CryptoResearchTool.Application.Strategies;

public class MovingAverageCrossoverStrategy : BaseStrategy
{
    private readonly int _fastPeriod;
    private readonly int _slowPeriod;
    private readonly int _trendFilterPeriod;
    private decimal _prevFastMa = 0;
    private decimal _prevSlowMa = 0;
    private bool _initialized = false;

    public override string Name { get; }

    public MovingAverageCrossoverStrategy(StrategyConfiguration config, SimulationConfiguration simConfig)
        : base(config.Symbol, config.Timeframe, simConfig)
    {
        Name = config.Name;
        _fastPeriod = config.GetParameter("FastPeriod", 5);
        _slowPeriod = config.GetParameter("SlowPeriod", 20);
        // TrendFilterPeriod: when > 0, a buy signal is only allowed when the latest close
        // is above the trend MA (a longer-term moving average used as a trend filter).
        // Set to 0 (default) to disable the filter.
        _trendFilterPeriod = config.GetParameter("TrendFilterPeriod", 0);
    }

    public override StrategySignal? Evaluate()
    {
        int warmup = Math.Max(_slowPeriod, _trendFilterPeriod > 0 ? _trendFilterPeriod : 0);
        if (CandleHistory.Count < warmup) return null;

        var closes = CandleHistory.Select(c => c.Close).ToList();
        var fastMa = closes.TakeLast(_fastPeriod).Average();
        var slowMa = closes.TakeLast(_slowPeriod).Average();

        StrategySignal? signal = null;
        if (_initialized)
        {
            bool crossedUp = _prevFastMa <= _prevSlowMa && fastMa > slowMa;
            bool crossedDown = _prevFastMa >= _prevSlowMa && fastMa < slowMa;

            if (crossedUp)
            {
                // Apply trend filter when configured: only buy when close is above the trend MA
                bool trendAllows = _trendFilterPeriod <= 0
                    || CandleHistory.Count < _trendFilterPeriod
                    || closes.Last() >= closes.TakeLast(_trendFilterPeriod).Average();

                if (trendAllows)
                    signal = CreateSignal(SignalType.Buy, LastPrice, "MA_CrossUp");
            }
            else if (crossedDown)
            {
                signal = CreateSignal(SignalType.Sell, LastPrice, "MA_CrossDown");
            }
        }

        _prevFastMa = fastMa;
        _prevSlowMa = slowMa;
        _initialized = true;
        if (signal != null) _lastSignal = signal;
        return signal;
    }
}
