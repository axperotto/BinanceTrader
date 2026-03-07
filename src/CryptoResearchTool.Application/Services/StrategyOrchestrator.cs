using CryptoResearchTool.Application.Interfaces;
using CryptoResearchTool.Domain.Configuration;
using CryptoResearchTool.Domain.Models;
using Microsoft.Extensions.Logging;
namespace CryptoResearchTool.Application.Services;

public class StrategyRunner
{
    public string StrategyRunId { get; }
    public ITradingStrategy Strategy { get; }
    public IPortfolioSimulator Portfolio { get; }
    public List<EquityPoint> EquityHistory { get; } = new();
    public StrategyMetrics CurrentMetrics { get; set; } = new();

    private readonly IMetricsCalculator _metricsCalculator;
    private readonly IPersistenceRepository _repository;
    private readonly SimulationConfiguration _simConfig;
    private readonly StrategyConfiguration _strategyConfig;
    private readonly ILogger _logger;

    // Stop loss / take profit prices for the current open position (0 = disabled)
    private decimal _stopLossPrice = 0m;
    private decimal _takeProfitPrice = 0m;

    // Cooldown tracking: bars since last trade closed; starts at MaxValue so first entry is never blocked
    private int _barsSinceLastTrade = int.MaxValue;

    // Total closed bars seen, used for exposure % calculation
    private int _totalBars = 0;
    private int _barsInPosition = 0;

    public StrategyRunner(
        string strategyRunId,
        ITradingStrategy strategy,
        IPortfolioSimulator portfolio,
        IMetricsCalculator metricsCalculator,
        IPersistenceRepository repository,
        SimulationConfiguration simConfig,
        StrategyConfiguration strategyConfig,
        ILogger logger)
    {
        StrategyRunId = strategyRunId;
        Strategy = strategy;
        Portfolio = portfolio;
        _metricsCalculator = metricsCalculator;
        _repository = repository;
        _simConfig = simConfig;
        _strategyConfig = strategyConfig;
        _logger = logger;
    }

    /// <summary>
    /// Processes a real-time tick (live mode only).
    /// </summary>
    public void OnTick(MarketTick tick)
    {
        if (tick.Symbol != Strategy.Symbol) return;
        Strategy.OnTick(tick);
        Portfolio.UpdateCurrentPrice(tick.Symbol, tick.Price);
    }

    /// <summary>
    /// Processes a closed candle: feeds data to strategy, checks SL/TP, evaluates signals.
    /// </summary>
    public void OnCandle(Candle candle)
    {
        if (candle.Symbol != Strategy.Symbol || candle.Timeframe != Strategy.Timeframe) return;

        // Feed candle to strategy (updates CandleHistory when IsClosed)
        Strategy.OnCandle(candle);

        // Mark-to-market: keep current price updated from every candle close
        Portfolio.UpdateCurrentPrice(candle.Symbol, candle.Close);

        if (!candle.IsClosed) return;

        _totalBars++;
        if (Portfolio.OpenPosition != null && Portfolio.OpenPosition.IsOpen)
            _barsInPosition++;

        // Record equity snapshot for this bar (used for drawdown / Sharpe computation)
        RecordEquityPoint(candle.OpenTime);

        // ── Stop Loss / Take Profit ──────────────────────────────────────────
        // Check SL/TP before evaluating the strategy signal so that a forced exit
        // cannot be overridden by a new signal on the same candle.
        if (Portfolio.OpenPosition != null && Portfolio.OpenPosition.IsOpen)
        {
            if (TryTriggerStopLossTakeProfit(candle))
                return; // position has been closed; skip signal evaluation this bar
        }

        // ── Cooldown ─────────────────────────────────────────────────────────
        int cooldown = _strategyConfig.MinBarsBetweenTrades;
        if (cooldown > 0 && _barsSinceLastTrade < cooldown)
        {
            _barsSinceLastTrade++;
            return;
        }

        // ── Strategy signal ───────────────────────────────────────────────────
        var signal = Strategy.Evaluate();
        if (signal == null || signal.Type == SignalType.None) return;

        CurrentMetrics.SignalsGenerated++;
        _ = _repository.SaveSignalAsync(StrategyRunId, signal);
        ProcessSignal(signal, candle.Close, candle.OpenTime);

        _barsSinceLastTrade++;
    }

    /// <summary>
    /// Checks whether the current open position should be closed by a stop loss or take profit
    /// triggered by this candle's price action. Uses pessimistic execution for ties
    /// (stop loss wins, which is worse for the trader).
    /// </summary>
    private bool TryTriggerStopLossTakeProfit(Candle candle)
    {
        bool slHit = _stopLossPrice > 0 && candle.Low <= _stopLossPrice;
        bool tpHit = _takeProfitPrice > 0 && candle.High >= _takeProfitPrice;

        if (!slHit && !tpHit) return false;

        // Pessimistic tie-break: if both levels are hit in the same candle, assume stop loss fired first
        if (slHit)
        {
            // Stop loss fill logic (pessimistic):
            // - Normal case: candle opens above stop → fill at the stop price (Math.Min(open, stop) = stop)
            // - Gap-down case: candle opens below stop → fill at candle open (worse than the stop)
            // This correctly models slippage through the stop level in fast-moving markets.
            var exitPrice = Math.Min(candle.Open, _stopLossPrice);
            ExecuteClose(exitPrice, ExitReasonCategory.StopLoss, candle.OpenTime);
        }
        else
        {
            // Take profit fill: use Max(open, takeProfit) so gap-up benefits are captured at open.
            var exitPrice = Math.Max(candle.Open, _takeProfitPrice);
            ExecuteClose(exitPrice, ExitReasonCategory.TakeProfit, candle.OpenTime);
        }

        return true;
    }

    private void ProcessSignal(StrategySignal signal, decimal price, DateTime? candleTime)
    {
        if (signal.Type == SignalType.Buy &&
            (Portfolio.OpenPosition == null || !Portfolio.OpenPosition.IsOpen))
        {
            var order = Portfolio.ExecuteBuy(
                Strategy.Symbol, price, _simConfig.DefaultPositionSizePercent, signal.Reason, candleTime);
            if (order != null)
            {
                CurrentMetrics.SignalsExecuted++;
                // Set SL/TP target prices from the executed (slippage-adjusted) entry price
                SetStopLossTakeProfitPrices(order.ExecutedPrice);
                _barsSinceLastTrade = 0;
            }
        }
        else if (signal.Type == SignalType.Sell &&
                 Portfolio.OpenPosition != null && Portfolio.OpenPosition.IsOpen)
        {
            ExecuteClose(price, ExitReasonCategory.StrategySignal, candleTime);
        }
    }

    /// <summary>
    /// Force-close the open position at the end of the backtest to ensure a clean final state.
    /// Called by the backtest engine after the last candle has been replayed.
    /// </summary>
    public void ForceClosePosition(decimal price, DateTime? candleTime = null)
    {
        if (Portfolio.OpenPosition == null || !Portfolio.OpenPosition.IsOpen) return;
        ExecuteClose(price, ExitReasonCategory.ForcedCloseEndOfTest, candleTime);
    }

    private void ExecuteClose(decimal price, string reasonCategory, DateTime? candleTime)
    {
        var order = Portfolio.ExecuteSell(
            Strategy.Symbol, price, reasonCategory, candleTime);
        if (order != null)
        {
            CurrentMetrics.SignalsExecuted++;
            var trade = Portfolio.CompletedTrades.LastOrDefault();
            if (trade != null)
            {
                trade.ExitReasonCategory = reasonCategory;
                _ = _repository.SaveTradeAsync(trade);
            }
            OnPositionClosed();
        }
    }

    private void SetStopLossTakeProfitPrices(decimal entryPrice)
    {
        _stopLossPrice = _strategyConfig.StopLossPercent > 0
            ? entryPrice * (1m - _strategyConfig.StopLossPercent / 100m)
            : 0m;
        _takeProfitPrice = _strategyConfig.TakeProfitPercent > 0
            ? entryPrice * (1m + _strategyConfig.TakeProfitPercent / 100m)
            : 0m;
    }

    private void OnPositionClosed()
    {
        _barsSinceLastTrade = 0;
        _stopLossPrice = 0m;
        _takeProfitPrice = 0m;
    }

    /// <summary>Records an in-memory equity snapshot for the given candle time.</summary>
    public void RecordEquityPoint(DateTime candleTime)
    {
        EquityHistory.Add(Portfolio.GetEquityPoint(candleTime));
    }

    /// <summary>
    /// Computes and saves final metrics. Should be called after all candles have been
    /// processed (and positions force-closed if applicable).
    /// </summary>
    public async Task UpdateMetricsAsync()
    {
        // Always record the final equity state (after any forced position close).
        // This ensures the equity curve reflects the actual end-of-test portfolio value.
        var finalEp = Portfolio.GetEquityPoint();
        EquityHistory.Add(finalEp);
        await _repository.SaveEquityPointAsync(StrategyRunId, finalEp);

        var exposurePercent = _totalBars > 0 ? (decimal)_barsInPosition / _totalBars * 100m : 0m;
        CurrentMetrics = _metricsCalculator.Calculate(
            StrategyRunId, Strategy.Name, Portfolio, EquityHistory, exposurePercent);
        await _repository.SaveMetricsSnapshotAsync(CurrentMetrics);
    }
}
