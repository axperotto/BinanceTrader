using CryptoResearchTool.Domain.Configuration;
using CryptoResearchTool.Domain.Models;
namespace CryptoResearchTool.Application.Strategies;

public class MovingAverageCrossoverStrategy : BaseStrategy
{
    private readonly int _fastPeriod;
    private readonly int _slowPeriod;
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
    }

    public override StrategySignal? Evaluate()
    {
        if (CandleHistory.Count < _slowPeriod) return null;
        var closes = CandleHistory.Select(c => c.Close).ToList();
        var fastMa = closes.TakeLast(_fastPeriod).Average();
        var slowMa = closes.TakeLast(_slowPeriod).Average();
        StrategySignal? signal = null;
        if (_initialized)
        {
            if (_prevFastMa <= _prevSlowMa && fastMa > slowMa)
                signal = CreateSignal(SignalType.Buy, LastPrice, "MA_CrossUp");
            else if (_prevFastMa >= _prevSlowMa && fastMa < slowMa)
                signal = CreateSignal(SignalType.Sell, LastPrice, "MA_CrossDown");
        }
        _prevFastMa = fastMa;
        _prevSlowMa = slowMa;
        _initialized = true;
        if (signal != null) _lastSignal = signal;
        return signal;
    }
}
