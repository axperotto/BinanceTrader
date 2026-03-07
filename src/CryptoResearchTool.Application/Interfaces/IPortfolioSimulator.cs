using CryptoResearchTool.Domain.Models;
namespace CryptoResearchTool.Application.Interfaces;
public interface IPortfolioSimulator
{
    string StrategyRunId { get; }
    decimal Cash { get; }
    decimal InitialCapital { get; }
    PortfolioPosition? OpenPosition { get; }
    List<SimulatedTrade> CompletedTrades { get; }
    SimulatedOrder? ExecuteBuy(string symbol, decimal price, decimal positionSizePercent, string reason, DateTime? timestamp = null);
    SimulatedOrder? ExecuteSell(string symbol, decimal price, string reason, DateTime? timestamp = null);

    /// <summary>
    /// Executes a partial exit on the current open position.
    /// Reduces the remaining quantity; closes the position entirely when Quantity reaches zero.
    /// </summary>
    /// <param name="symbol">Symbol to sell.</param>
    /// <param name="price">Current market price (slippage is applied internally).</param>
    /// <param name="fractionOfInitial">
    /// Fraction of the INITIAL position quantity to sell (0–1).
    /// Capped at the remaining quantity to avoid overshooting.
    /// </param>
    /// <param name="reasonCategory">Structured exit category (use <see cref="ExitReasonCategory"/> constants).</param>
    /// <param name="managementReason">Human-readable description of the management action.</param>
    /// <param name="partialExitIndex">Zero-based index of this partial exit level.</param>
    /// <param name="timestamp">Optional timestamp override.</param>
    /// <returns>The executed order, or null if no open position exists for this symbol.</returns>
    SimulatedOrder? ExecutePartialSell(string symbol, decimal price, decimal fractionOfInitial,
        string reasonCategory, string managementReason, int partialExitIndex,
        DateTime? timestamp = null);

    void UpdateCurrentPrice(string symbol, decimal price);
    decimal GetEquity();
    EquityPoint GetEquityPoint(DateTime? timestamp = null);
}
