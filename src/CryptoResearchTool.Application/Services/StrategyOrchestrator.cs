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
    private readonly TradeManagementEngine _managementEngine;

    // Stop loss / take profit prices for the current open position (0 = disabled).
    // Used only when TradeManagementEngine.IsActive is false (legacy path).
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
        _managementEngine = new TradeManagementEngine(strategyConfig);
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
    /// Processes a closed candle: feeds data to strategy, runs trade management,
    /// checks SL/TP, evaluates signals.
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

        // ── Trade management / Stop Loss / Take Profit ────────────────────────
        if (Portfolio.OpenPosition != null && Portfolio.OpenPosition.IsOpen)
        {
            bool positionClosed;
            if (_managementEngine.IsActive)
                positionClosed = RunManagementEngine(candle);
            else
                positionClosed = TryTriggerStopLossTakeProfit(candle);

            if (positionClosed)
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
    /// Runs the advanced trade management engine for the current candle.
    /// Returns true when the position has been fully closed this bar.
    /// </summary>
    private bool RunManagementEngine(Candle candle)
    {
        var position = Portfolio.OpenPosition!;
        var actions = _managementEngine.ProcessBar(position, candle, _totalBars);
        if (actions.Count == 0) return false;

        bool positionClosed = false;
        foreach (var action in actions)
        {
            if (!Portfolio.OpenPosition?.IsOpen ?? true)
            {
                positionClosed = true;
                break;
            }

            if (action.Type == ManagementActionType.PartialExit)
            {
                var order = Portfolio.ExecutePartialSell(
                    Strategy.Symbol, action.Price, action.FractionToSell,
                    action.ReasonCategory, action.ManagementReason, action.PartialExitIndex,
                    candle.OpenTime);

                if (order != null)
                {
                    CurrentMetrics.SignalsExecuted++;
                    var trade = Portfolio.CompletedTrades.LastOrDefault();
                    if (trade != null)
                        _ = _repository.SaveTradeAsync(trade);

                    if (Portfolio.OpenPosition == null || !Portfolio.OpenPosition.IsOpen)
                    {
                        positionClosed = true;
                        OnPositionClosed();
                        break;
                    }
                }
            }
            else if (action.Type == ManagementActionType.FullExit)
            {
                ExecuteClose(action.Price, action.ReasonCategory, candle.OpenTime);
                positionClosed = true;
                break;
            }
        }

        return positionClosed;
    }

    /// <summary>
    /// Checks whether the current open position should be closed by a stop loss or take profit
    /// triggered by this candle's price action. Uses pessimistic execution for ties
    /// (stop loss wins, which is worse for the trader).
    /// Only used when TradeManagementEngine.IsActive is false.
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
                // Compute initial stop loss price from strategy config
                var initialStop = _strategyConfig.StopLossPercent > 0
                    ? order.ExecutedPrice * (1m - _strategyConfig.StopLossPercent / 100m)
                    : 0m;

                if (_managementEngine.IsActive)
                {
                    // Initialise position-level management state
                    _managementEngine.OnPositionOpened(Portfolio.OpenPosition!, initialStop);
                    // Also clear the legacy SL/TP fields
                    _stopLossPrice = 0m;
                    _takeProfitPrice = 0m;
                }
                else
                {
                    // Legacy path: set SL/TP on the runner
                    SetStopLossTakeProfitPrices(order.ExecutedPrice);
                }

                _barsSinceLastTrade = 0;
            }
        }
        else if (signal.Type == SignalType.Sell &&
                 Portfolio.OpenPosition != null && Portfolio.OpenPosition.IsOpen)
        {
            // When management engine is active, respect AllowFinalSignalExit / AllowTrendInvalidationExit
            bool allowExit = !_managementEngine.IsActive
                || _strategyConfig.AllowFinalSignalExit
                || _strategyConfig.AllowTrendInvalidationExit;

            if (allowExit)
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
