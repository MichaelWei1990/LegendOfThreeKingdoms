using LegendOfThreeKingdoms.Core.Configuration;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Abstractions;

/// <summary>
/// Abstraction of a complete game mode (e.g. standard, guozhan).
/// A game mode defines turn structure, faction rules and win conditions.
/// </summary>
public interface IGameMode
{
    /// <summary>
    /// Stable identifier of the mode (e.g. "standard", "guozhan").
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Human-readable name for display purposes.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Selects the seat index of the player who should take
    /// the very first turn in a new game.
    /// <para>
    /// For identity mode this is typically the Emperor (Lord).
    /// Implementations should return a valid seat for a player
    /// that is expected to be alive at the beginning of the game.
    /// </para>
    /// </summary>
    /// <param name="game">The game state after configuration has been mapped.</param>
    /// <returns>The seat index of the first player.</returns>
    int SelectFirstPlayerSeat(Game game);

    /// <summary>
    /// Gets the role assignment service for this game mode, if applicable.
    /// Identity mode should return a service instance; other modes may return null.
    /// </summary>
    /// <returns>Role assignment service, or null if not applicable.</returns>
    Identity.IRoleAssignmentService? GetRoleAssignmentService() => null;

    /// <summary>
    /// Gets the win condition service for this game mode, if applicable.
    /// Identity mode should return a service instance; other modes may return null.
    /// </summary>
    /// <returns>Win condition service, or null if not applicable.</returns>
    Identity.IWinConditionService? GetWinConditionService() => null;

    // Future phases will extend this interface with additional methods to
    // build initial game state, drive phase progression and evaluate
    // victory conditions based on the current state.
}
