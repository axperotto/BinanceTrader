using CryptoResearchTool.Application.Interfaces;
using CryptoResearchTool.Domain.Configuration;
using CryptoResearchTool.Domain.Models;
using Microsoft.Extensions.Logging;
namespace CryptoResearchTool.Application.Services;

public class PortfolioSimulator : IPortfolioSimulator
{
    private readonly SimulationConfiguration _config;
    private readonly ILogger<PortfolioSimulator> _logger;
    private readonly object _lock = new();

    public string StrategyRunId { get; private set; } = "";
    public decimal Cash { get; private set; }
    public decimal InitialCapital { get; private set; }
    public PortfolioPosition? OpenPosition { get; private set; }
    public List<SimulatedTrade> CompletedTrades { get; } = new();

    public PortfolioSimulator(SimulationConfiguration config, ILogger<PortfolioSimulator> logger, string strategyRunId, decimal initialCapital)
    {
        _config = config;
        _logger = logger;
        StrategyRunId = strategyRunId;
        InitialCapital = initialCapital;
        Cash = initialCapital;
    }

    public SimulatedOrder? ExecuteBuy(string symbol, decimal price, decimal positionSizePercent, string reason, DateTime? timestamp = null)
    {
        lock (_lock)
        {
            if (OpenPosition != null && OpenPosition.IsOpen)
            {
                _logger.LogDebug("Buy skipped: position already open for {Symbol}", symbol);
                return null;
            }
            var ts = timestamp ?? DateTime.UtcNow;
            var slippageMultiplier = 1m + (_config.SlippagePercent / 100m);
            var executedPrice = price * slippageMultiplier;
            var capitalToUse = Cash * (positionSizePercent / 100m);
            var fee = capitalToUse * (_config.FeePercent / 100m);
            var capitalAfterFee = capitalToUse - fee;
            var quantity = capitalAfterFee / executedPrice;
            if (quantity <= 0 || Cash < capitalToUse) return null;
            Cash -= capitalToUse;
            var slippageImpact = price * quantity * (_config.SlippagePercent / 100m);
            OpenPosition = new PortfolioPosition
            {
                Symbol = symbol,
                Quantity = quantity,
                EntryPrice = executedPrice,
                CurrentPrice = executedPrice,
                EntryTime = ts,
                EntryReason = reason,
                EntryFee = fee,
                SlippageImpact = slippageImpact
            };
            var order = new SimulatedOrder
            {
                StrategyRunId = StrategyRunId,
                Symbol = symbol,
                Side = OrderSide.Buy,
                RequestedPrice = price,
                ExecutedPrice = executedPrice,
                Quantity = quantity,
                FeeAmount = fee,
                Timestamp = ts,
                Reason = reason
            };
            _logger.LogInformation("BUY {Symbol} qty={Qty:F6} @ {Price:F2} reason={Reason}", symbol, quantity, executedPrice, reason);
            return order;
        }
    }

    public SimulatedOrder? ExecuteSell(string symbol, decimal price, string reason, DateTime? timestamp = null)
    {
        lock (_lock)
        {
            if (OpenPosition == null || !OpenPosition.IsOpen || OpenPosition.Symbol != symbol) return null;
            var ts = timestamp ?? DateTime.UtcNow;
            var slippageMultiplier = 1m - (_config.SlippagePercent / 100m);
            var executedPrice = price * slippageMultiplier;
            var grossProceeds = OpenPosition.Quantity * executedPrice;
            var exitFee = grossProceeds * (_config.FeePercent / 100m);
            var netProceeds = grossProceeds - exitFee;
            var entryValue = OpenPosition.Quantity * OpenPosition.EntryPrice;
            var grossPnl = (OpenPosition.Quantity * executedPrice) - entryValue;
            var totalFees = OpenPosition.EntryFee + exitFee;
            var slippageImpact = OpenPosition.SlippageImpact + (grossProceeds * (_config.SlippagePercent / 100m));
            var pnl = netProceeds - entryValue;
            var pnlPercent = entryValue > 0 ? (pnl / entryValue) * 100m : 0;
            var holdingTime = ts - OpenPosition.EntryTime;
            var trade = new SimulatedTrade
            {
                StrategyRunId = StrategyRunId,
                Symbol = symbol,
                EntryPrice = OpenPosition.EntryPrice,
                ExitPrice = executedPrice,
                Quantity = OpenPosition.Quantity,
                GrossPnL = grossPnl,
                PnL = pnl,
                PnLPercent = pnlPercent,
                TotalFees = totalFees,
                SlippageImpact = slippageImpact,
                EntryTime = OpenPosition.EntryTime,
                ExitTime = ts,
                HoldingTime = holdingTime,
                EntryReason = OpenPosition.EntryReason,
                ExitReason = reason
            };
            CompletedTrades.Add(trade);
            Cash += netProceeds;
            var order = new SimulatedOrder
            {
                StrategyRunId = StrategyRunId,
                Symbol = symbol,
                Side = OrderSide.Sell,
                RequestedPrice = price,
                ExecutedPrice = executedPrice,
                Quantity = trade.Quantity,
                FeeAmount = exitFee,
                Timestamp = ts,
                Reason = reason
            };
            OpenPosition = null;
            _logger.LogInformation("SELL {Symbol} @ {Price:F2} PnL={PnL:F2} reason={Reason}", symbol, executedPrice, pnl, reason);
            return order;
        }
    }

    public void UpdateCurrentPrice(string symbol, decimal price)
    {
        lock (_lock)
        {
            if (OpenPosition != null && OpenPosition.Symbol == symbol)
                OpenPosition.CurrentPrice = price;
        }
    }

    public decimal GetEquity()
    {
        lock (_lock)
        {
            var positionValue = OpenPosition?.MarketValue ?? 0m;
            return Cash + positionValue;
        }
    }

    public EquityPoint GetEquityPoint(DateTime? timestamp = null) => new()
    {
        Timestamp = timestamp ?? DateTime.UtcNow,
        Equity = GetEquity(),
        Cash = Cash,
        UnrealizedPnL = OpenPosition?.UnrealizedPnL ?? 0m
    };
}
