using CryptoResearchTool.Application.Interfaces;
using CryptoResearchTool.Domain.Configuration;
using CryptoResearchTool.Domain.Models;
namespace CryptoResearchTool.Application.Strategies;

public abstract class BaseStrategy : ITradingStrategy
{
    public abstract string Name { get; }
    public string Symbol { get; }
    public string Timeframe { get; }
    protected string StrategyRunId { get; private set; } = "";
    protected decimal LastPrice { get; private set; }
    protected List<Candle> CandleHistory { get; } = new();
    protected int MaxHistory { get; } = 200;
    protected StrategyRuntimeState _state;
    protected StrategyMetrics _metrics = new();
    protected StrategySignal? _lastSignal;
    protected readonly SimulationConfiguration _simConfig;

    protected BaseStrategy(string symbol, string timeframe, SimulationConfiguration simConfig)
    {
        Symbol = symbol;
        Timeframe = timeframe;
        _simConfig = simConfig;
        _state = new StrategyRuntimeState { Symbol = symbol, Timeframe = timeframe };
    }

    public void Initialize(string strategyRunId)
    {
        StrategyRunId = strategyRunId;
        _state.StrategyRunId = strategyRunId;
        _state.StrategyName = Name;
        _state.IsRunning = true;
        _state.InitialCapital = _simConfig.InitialCapital;
        _state.Cash = _simConfig.InitialCapital;
    }

    public virtual void OnTick(MarketTick tick)
    {
        if (tick.Symbol != Symbol) return;
        LastPrice = tick.Price;
        _state.LastPrice = tick.Price;
        _state.LastUpdate = tick.Timestamp;
    }

    public virtual void OnCandle(Candle candle)
    {
        if (candle.Symbol != Symbol || candle.Timeframe != Timeframe) return;
        if (candle.IsClosed)
        {
            CandleHistory.Add(candle);
            if (CandleHistory.Count > MaxHistory) CandleHistory.RemoveAt(0);
        }
    }

    public abstract StrategySignal? Evaluate();

    public StrategyRuntimeState GetState()
    {
        _state.LastSignal = _lastSignal;
        return _state;
    }

    protected StrategySignal CreateSignal(SignalType type, decimal price, string reason) => new()
    {
        StrategyName = Name,
        Symbol = Symbol,
        Type = type,
        Price = price,
        Timestamp = DateTime.UtcNow,
        Reason = reason
    };
}
