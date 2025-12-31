using System.Collections.Generic;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Identity;

/// <summary>
/// Result of win condition checking.
/// </summary>
public sealed class WinConditionResult
{
    /// <summary>
    /// Whether a win condition has been met.
    /// </summary>
    public bool IsGameOver { get; init; }

    /// <summary>
    /// Type of winning side (if game is over).
    /// </summary>
    public WinType? WinType { get; init; }

    /// <summary>
    /// List of winning players (if game is over).
    /// </summary>
    public IReadOnlyList<Player>? WinningPlayers { get; init; }

    /// <summary>
    /// Description of the win condition that was met.
    /// </summary>
    public string? EndReason { get; init; }

    /// <summary>
    /// Creates a result indicating the game is not over.
    /// </summary>
    public static WinConditionResult NotOver()
    {
        return new WinConditionResult
        {
            IsGameOver = false
        };
    }

    /// <summary>
    /// Creates a result indicating the game is over with a winner.
    /// </summary>
    public static WinConditionResult GameOver(
        WinType winType,
        IReadOnlyList<Player> winningPlayers,
        string endReason)
    {
        return new WinConditionResult
        {
            IsGameOver = true,
            WinType = winType,
            WinningPlayers = winningPlayers,
            EndReason = endReason
        };
    }
}

/// <summary>
/// Types of winning sides in identity mode.
/// </summary>
public enum WinType
{
    /// <summary>
    /// Lord and Loyalists win together.
    /// </summary>
    LordAndLoyalists,

    /// <summary>
    /// Rebels win.
    /// </summary>
    Rebels,

    /// <summary>
    /// Renegade wins (sole survivor).
    /// </summary>
    Renegade
}

