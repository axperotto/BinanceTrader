namespace CryptoResearchTool.Domain.Models;

/// <summary>Represents the lifecycle state of an open position's trade management.</summary>
public enum PositionManagementState
{
    /// <summary>No position open.</summary>
    Flat,
    /// <summary>Position entered; no management targets reached yet.</summary>
    Entered,
    /// <summary>Stop has been moved to break-even or above entry.</summary>
    BreakEvenProtected,
    /// <summary>Trailing stop is active, following price upward.</summary>
    TrailingActive,
    /// <summary>Position has been fully closed.</summary>
    FullyClosed
}
