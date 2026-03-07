using CryptoResearchTool.Domain.Configuration;
using CryptoResearchTool.Domain.Models;

namespace CryptoResearchTool.Application.Services;

/// <summary>
/// Implements the candle-by-candle trade management state machine for an open position.
///
/// Each call to <see cref="ProcessBar"/> inspects the current position against the latest
/// closed candle and returns a list of <see cref="ManagementAction"/> objects that the
/// caller (<see cref="StrategyRunner"/>) must execute in order.
///
/// Supported features (all opt-in via <see cref="StrategyConfiguration"/>):
/// <list type="bullet">
///   <item>Staged partial take-profit exits at configurable profit levels.</item>
///   <item>Break-even stop: moves the protective stop to entry + offset once a profit trigger is reached.</item>
///   <item>Trailing stop: activates above a configurable profit level, then follows price upward.</item>
///   <item>Initial stop-loss monitoring (reads from <see cref="PortfolioPosition.StopLossPrice"/>).</item>
/// </list>
///
/// <para>
/// The engine is <em>active</em> (replaces the legacy fixed SL/TP logic in StrategyRunner) only
/// when at least one of the following is configured:
/// partial take-profits, break-even trigger, or trailing stop activation.
/// </para>
/// </summary>
public class TradeManagementEngine
{
    private readonly StrategyConfiguration _cfg;

    private readonly bool _hasPartialTargets;
    private readonly bool _hasBreakEven;
    private readonly bool _hasTrailingStop;

    /// <summary>
    /// True when at least one advanced management feature is configured.
    /// When false, the legacy SL/TP logic in StrategyRunner continues to run unchanged.
    /// </summary>
    public bool IsActive { get; }

    public TradeManagementEngine(StrategyConfiguration config)
    {
        _cfg = config;

        _hasPartialTargets = config.PartialTakeProfitLevelsPercent.Count > 0
                          && config.PartialTakeProfitExitPercent.Count == config.PartialTakeProfitLevelsPercent.Count;

        _hasBreakEven = config.BreakEvenTriggerPercent > 0;

        _hasTrailingStop = config.TrailingStopActivationPercent > 0
                        && config.TrailingStopDistancePercent > 0;

        IsActive = _hasPartialTargets || _hasBreakEven || _hasTrailingStop;
    }

    /// <summary>
    /// Initialises position-level management state immediately after entry.
    /// Must be called once, right after <see cref="IPortfolioSimulator.ExecuteBuy"/> returns.
    /// </summary>
    /// <param name="position">The newly opened position.</param>
    /// <param name="initialStopPrice">
    /// Protective stop derived from <see cref="StrategyConfiguration.StopLossPercent"/>
    /// (0 = no initial stop).
    /// </param>
    public void OnPositionOpened(PortfolioPosition position, decimal initialStopPrice)
    {
        position.StopLossPrice = initialStopPrice;
        position.HighestPriceSinceEntry = position.EntryPrice;
        position.LowestPriceSinceEntry = position.EntryPrice;
        position.PartialTargetsHit = 0;
        position.BreakEvenActivated = false;
        position.TrailingStopActive = false;
        position.TrailingStopPrice = 0m;
        position.RealizedPnL = 0m;
        position.AverageExitPrice = 0m;
        position.ManagementState = PositionManagementState.Entered;
    }

    /// <summary>
    /// Processes one closed candle for the open position and returns any management actions
    /// that should be executed immediately.
    /// Actions are ordered: partial exits first, then a full exit (stop hit) if applicable.
    /// </summary>
    /// <param name="position">The currently open position (modified in-place for watermarks / stop updates).</param>
    /// <param name="candle">The newly closed candle.</param>
    /// <param name="barIndex">Monotonically increasing bar index used for audit purposes.</param>
    /// <returns>Ordered list of actions to execute; empty when nothing needs to be done this bar.</returns>
    public List<ManagementAction> ProcessBar(PortfolioPosition position, Candle candle, int barIndex)
    {
        var actions = new List<ManagementAction>();
        if (position == null || !position.IsOpen) return actions;

        // ── 1. Update price watermarks ────────────────────────────────────────
        if (candle.High > position.HighestPriceSinceEntry)
            position.HighestPriceSinceEntry = candle.High;
        if (candle.Low < position.LowestPriceSinceEntry || position.LowestPriceSinceEntry == 0m)
            position.LowestPriceSinceEntry = candle.Low;

        // ── 2. Staged partial take-profit exits ───────────────────────────────
        if (_hasPartialTargets)
            CheckPartialTakeProfits(position, candle, actions);

        // ── 3. Break-even stop update ─────────────────────────────────────────
        if (_hasBreakEven)
            UpdateBreakEvenStop(position);

        // ── 4. Trailing stop activation / update ──────────────────────────────
        if (_hasTrailingStop)
            UpdateTrailingStop(position);

        // ── 5. Sync management state for UI visibility ────────────────────────
        if (position.TrailingStopActive)
            position.ManagementState = PositionManagementState.TrailingActive;
        else if (position.BreakEvenActivated)
            position.ManagementState = PositionManagementState.BreakEvenProtected;

        // ── 6. Check whether any protective stop is hit ───────────────────────
        // Only do this if no partial exits already closed the position this bar.
        if (position.IsOpen)
            CheckProtectiveStop(position, candle, barIndex, actions);

        if (actions.Count > 0)
            position.LastManagementBarIndex = barIndex;

        return actions;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void CheckPartialTakeProfits(
        PortfolioPosition position, Candle candle, List<ManagementAction> actions)
    {
        var targets = _cfg.PartialTakeProfitLevelsPercent;
        var exits = _cfg.PartialTakeProfitExitPercent;

        // NOTE: targets must be in ascending order — enforced by StrategyConfiguration.Normalize().
        // The break below relies on this ordering to avoid unnecessary checks.
        for (int i = position.PartialTargetsHit; i < targets.Count; i++)
        {
            var targetPrice = position.EntryPrice * (1m + targets[i] / 100m);
            if (candle.High < targetPrice) break; // targets are ascending; no point checking further

            // Target i is reached within this candle's range.
            // Execute at the configured target price (optimistic fill at that exact level).
            // Slippage is applied inside ExecutePartialSell.
            var fraction = exits[i] / 100m;
            actions.Add(new ManagementAction
            {
                Type = ManagementActionType.PartialExit,
                Price = targetPrice,
                ReasonCategory = ExitReasonCategory.PartialTakeProfit,
                ManagementReason = $"PartialTP_{i + 1}({targets[i]:F1}%)",
                FractionToSell = fraction,
                PartialExitIndex = i
            });

            position.PartialTargetsHit = i + 1;
        }
    }

    private void UpdateBreakEvenStop(PortfolioPosition position)
    {
        if (position.BreakEvenActivated) return;
        if (position.EntryPrice <= 0) return;

        var profitPct = (position.HighestPriceSinceEntry - position.EntryPrice)
                        / position.EntryPrice * 100m;

        if (profitPct < _cfg.BreakEvenTriggerPercent) return;

        var breakEvenStop = position.EntryPrice * (1m + _cfg.BreakEvenOffsetPercent / 100m);

        // Stop only ever moves upward
        if (breakEvenStop > position.StopLossPrice)
        {
            position.StopLossPrice = breakEvenStop;
            position.BreakEvenActivated = true;
        }
    }

    private void UpdateTrailingStop(PortfolioPosition position)
    {
        if (position.EntryPrice <= 0) return;

        var profitPct = (position.HighestPriceSinceEntry - position.EntryPrice)
                        / position.EntryPrice * 100m;

        if (!position.TrailingStopActive)
        {
            // Activate when price has moved at least the activation threshold
            if (profitPct < _cfg.TrailingStopActivationPercent) return;
            position.TrailingStopActive = true;
        }

        // Recalculate trailing stop from the current best price
        var newTrailingStop = position.HighestPriceSinceEntry
                              * (1m - _cfg.TrailingStopDistancePercent / 100m);

        // Trailing stop only moves upward; also ensure it never drops below the break-even stop
        if (newTrailingStop > position.TrailingStopPrice)
            position.TrailingStopPrice = newTrailingStop;

        if (position.TrailingStopPrice > position.StopLossPrice)
            position.StopLossPrice = position.TrailingStopPrice;
    }

    private static void CheckProtectiveStop(
        PortfolioPosition position, Candle candle, int barIndex, List<ManagementAction> actions)
    {
        if (position.StopLossPrice <= 0) return;
        if (candle.Low > position.StopLossPrice) return;

        // Determine exit reason: trailing stop, break-even stop, or initial stop-loss
        string reasonCategory;
        string managementReason;
        if (position.TrailingStopActive && position.TrailingStopPrice > 0
            && position.TrailingStopPrice >= position.StopLossPrice)
        {
            reasonCategory = ExitReasonCategory.TrailingStop;
            managementReason = $"TrailingStop@{position.TrailingStopPrice:F2}";
        }
        else if (position.BreakEvenActivated)
        {
            reasonCategory = ExitReasonCategory.BreakEvenStop;
            managementReason = $"BreakEvenStop@{position.StopLossPrice:F2}";
        }
        else
        {
            reasonCategory = ExitReasonCategory.StopLoss;
            managementReason = $"StopLoss@{position.StopLossPrice:F2}";
        }

        // Pessimistic fill logic:
        // - Normal case (open > stop): fill at stop price → Math.Min(open, stop) = stop ✓
        // - Gap-down (open ≤ stop): candle opened below stop; fill at open (worse for trader) ✓
        // candle.Low ≤ candle.Open ≤ candle.High by definition, so Math.Min(open, stop)
        // is equivalent to and simpler than Math.Max(low, Math.Min(open, stop)).
        var exitPrice = Math.Min(candle.Open, position.StopLossPrice);

        actions.Add(new ManagementAction
        {
            Type = ManagementActionType.FullExit,
            Price = exitPrice,
            ReasonCategory = reasonCategory,
            ManagementReason = managementReason
        });
    }
}
