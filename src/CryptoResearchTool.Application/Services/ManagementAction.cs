namespace CryptoResearchTool.Application.Services;

/// <summary>Type of management action the engine requests on a given bar.</summary>
public enum ManagementActionType
{
    /// <summary>Sell a configured fraction of the initial position; position stays open.</summary>
    PartialExit,
    /// <summary>Close the entire remaining position immediately.</summary>
    FullExit
}

/// <summary>
/// An action returned by <see cref="TradeManagementEngine.ProcessBar"/> for the
/// <see cref="StrategyRunner"/> to execute against the portfolio simulator.
/// </summary>
public class ManagementAction
{
    public ManagementActionType Type { get; set; }

    /// <summary>Market price at which to execute the order (management engine determines trigger price).</summary>
    public decimal Price { get; set; }

    /// <summary>Structured exit reason category (one of the <see cref="Domain.Models.ExitReasonCategory"/> constants).</summary>
    public string ReasonCategory { get; set; } = "";

    /// <summary>Human-readable description of why this action was taken (e.g. "PartialTP_1", "TrailingStop").</summary>
    public string ManagementReason { get; set; } = "";

    /// <summary>For PartialExit: fraction of the INITIAL position to sell (0–1).</summary>
    public decimal FractionToSell { get; set; }

    /// <summary>For PartialExit: zero-based index of the partial target that triggered this action.</summary>
    public int PartialExitIndex { get; set; }
}
