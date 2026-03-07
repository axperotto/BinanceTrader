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

    public SimulatedOrder? ExecuteBuy(string symbol, decimal price, decimal positionSizePercent, string reason)
    {
        lock (_lock)
        {
            if (OpenPosition != null && OpenPosition.IsOpen)
            {
                _logger.LogDebug("Buy skipped: position already open for {Symbol}", symbol);
                return null;
            }
            var slippageMultiplier = 1m + (_config.SlippagePercent / 100m);
            var executedPrice = price * slippageMultiplier;
            var capitalToUse = Cash * (positionSizePercent / 100m);
            var fee = capitalToUse * (_config.FeePercent / 100m);
            var capitalAfterFee = capitalToUse - fee;
            var quantity = capitalAfterFee / executedPrice;
            if (quantity <= 0 || Cash < capitalToUse) return null;
            Cash -= capitalToUse;
            OpenPosition = new PortfolioPosition
            {
                Symbol = symbol,
                Quantity = quantity,
                EntryPrice = executedPrice,
                CurrentPrice = executedPrice,
                EntryTime = DateTime.UtcNow,
                EntryReason = reason
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
                Timestamp = DateTime.UtcNow,
                Reason = reason
            };
            _logger.LogInformation("BUY {Symbol} qty={Qty:F6} @ {Price:F2} reason={Reason}", symbol, quantity, executedPrice, reason);
            return order;
        }
    }

    public SimulatedOrder? ExecuteSell(string symbol, decimal price, string reason)
    {
        lock (_lock)
        {
            if (OpenPosition == null || !OpenPosition.IsOpen || OpenPosition.Symbol != symbol) return null;
            var slippageMultiplier = 1m - (_config.SlippagePercent / 100m);
            var executedPrice = price * slippageMultiplier;
            var grossProceeds = OpenPosition.Quantity * executedPrice;
            var fee = grossProceeds * (_config.FeePercent / 100m);
            var netProceeds = grossProceeds - fee;
            var entryValue = OpenPosition.Quantity * OpenPosition.EntryPrice;
            var pnl = netProceeds - entryValue;
            var pnlPercent = entryValue > 0 ? (pnl / entryValue) * 100m : 0;
            var trade = new SimulatedTrade
            {
                StrategyRunId = StrategyRunId,
                Symbol = symbol,
                EntryPrice = OpenPosition.EntryPrice,
                ExitPrice = executedPrice,
                Quantity = OpenPosition.Quantity,
                PnL = pnl,
                PnLPercent = pnlPercent,
                TotalFees = fee,
                EntryTime = OpenPosition.EntryTime,
                ExitTime = DateTime.UtcNow,
                HoldingTime = DateTime.UtcNow - OpenPosition.EntryTime,
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
                FeeAmount = fee,
                Timestamp = DateTime.UtcNow,
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

    public EquityPoint GetEquityPoint() => new()
    {
        Timestamp = DateTime.UtcNow,
        Equity = GetEquity(),
        Cash = Cash,
        UnrealizedPnL = OpenPosition?.UnrealizedPnL ?? 0m
    };
}
