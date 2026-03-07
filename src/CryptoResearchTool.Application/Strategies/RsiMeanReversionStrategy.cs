using CryptoResearchTool.Domain.Configuration;
using CryptoResearchTool.Domain.Models;
namespace CryptoResearchTool.Application.Strategies;

public class RsiMeanReversionStrategy : BaseStrategy
{
    private readonly int _rsiPeriod;
    private readonly decimal _oversold;
    private readonly decimal _overbought;
    private readonly bool _trendFilterEnabled;
    private readonly int _trendFilterPeriod;
    // ExitRsiLevel: sell when RSI recovers above this level (e.g. 50 or 55).
    // 0 = use overbought threshold only.
    private readonly decimal _exitRsiLevel;

    private bool _inPosition = false;

    public override string Name { get; }

    public RsiMeanReversionStrategy(StrategyConfiguration config, SimulationConfiguration simConfig)
        : base(config.Symbol, config.Timeframe, simConfig)
    {
        Name = config.Name;
        _rsiPeriod = config.GetParameter("RsiPeriod", 14);
        _oversold = config.GetParameter("Oversold", 30m);
        _overbought = config.GetParameter("Overbought", 70m);
        _trendFilterEnabled = config.GetParameter("TrendFilterEnabled", false);
        _trendFilterPeriod = config.GetParameter("TrendFilterPeriod", 50);
        _exitRsiLevel = config.GetParameter("ExitRsiLevel", 0m);
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

    private bool IsTrendBullish()
    {
        if (!_trendFilterEnabled || _trendFilterPeriod <= 0) return true;
        if (CandleHistory.Count < _trendFilterPeriod) return true; // not enough data – allow
        var closes = CandleHistory.Select(c => c.Close).ToList();
        var trendMa = closes.TakeLast(_trendFilterPeriod).Average();
        return closes.Last() >= trendMa;
    }

    public override StrategySignal? Evaluate()
    {
        if (CandleHistory.Count < _rsiPeriod + 1) return null;
        var rsi = CalculateRsi();
        StrategySignal? signal = null;

        if (!_inPosition)
        {
            // Entry: RSI oversold AND trend filter allows it
            if (rsi < _oversold && IsTrendBullish())
            {
                _inPosition = true;
                signal = CreateSignal(SignalType.Buy, LastPrice, $"RSI_Oversold({rsi:F1})");
            }
        }
        else
        {
            // Exit: RSI overbought OR RSI has recovered above the configurable exit level
            bool exitOnOverbought = rsi > _overbought;
            bool exitOnRecovery = _exitRsiLevel > 0 && rsi >= _exitRsiLevel;
            if (exitOnOverbought || exitOnRecovery)
            {
                _inPosition = false;
                var reason = exitOnOverbought
                    ? $"RSI_Overbought({rsi:F1})"
                    : $"RSI_Recovery({rsi:F1}>={_exitRsiLevel})";
                signal = CreateSignal(SignalType.Sell, LastPrice, reason);
            }
        }

        if (signal != null) _lastSignal = signal;
        return signal;
    }
}
