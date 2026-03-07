using CryptoResearchTool.Domain.Configuration;
using CryptoResearchTool.Domain.Models;
namespace CryptoResearchTool.Application.Strategies;

public class RsiMeanReversionStrategy : BaseStrategy
{
    private readonly int _rsiPeriod;
    private readonly decimal _oversold;
    private readonly decimal _overbought;
    public override string Name { get; }

    public RsiMeanReversionStrategy(StrategyConfiguration config, SimulationConfiguration simConfig)
        : base(config.Symbol, config.Timeframe, simConfig)
    {
        Name = config.Name;
        _rsiPeriod = config.GetParameter("RsiPeriod", 14);
        _oversold = config.GetParameter("Oversold", 30m);
        _overbought = config.GetParameter("Overbought", 70m);
    }

    private decimal CalculateRsi()
    {
        if (CandleHistory.Count < _rsiPeriod + 1) return 50m;
        var closes = CandleHistory.TakeLast(_rsiPeriod + 1).Select(c => c.Close).ToList();
        var gains = new List<decimal>();
        var losses = new List<decimal>();
        for (int i = 1; i < closes.Count; i++)
        {
            var change = closes[i] - closes[i - 1];
            if (change > 0) { gains.Add(change); losses.Add(0); }
            else { gains.Add(0); losses.Add(-change); }
        }
        var avgGain = gains.Average();
        var avgLoss = losses.Average();
        if (avgLoss == 0) return 100m;
        var rs = avgGain / avgLoss;
        return 100m - (100m / (1m + rs));
    }

    public override StrategySignal? Evaluate()
    {
        if (CandleHistory.Count < _rsiPeriod + 1) return null;
        var rsi = CalculateRsi();
        StrategySignal? signal = null;
        if (rsi < _oversold)
            signal = CreateSignal(SignalType.Buy, LastPrice, $"RSI_Oversold({rsi:F1})");
        else if (rsi > _overbought)
            signal = CreateSignal(SignalType.Sell, LastPrice, $"RSI_Overbought({rsi:F1})");
        if (signal != null) _lastSignal = signal;
        return signal;
    }
}
