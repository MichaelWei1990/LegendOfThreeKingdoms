using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Identity;

/// <summary>
/// Service for assigning roles to players in identity mode.
/// </summary>
public interface IRoleAssignmentService
{
    /// <summary>
    /// Assigns roles to all players in the game based on player count and configuration.
    /// </summary>
    /// <param name="game">The game instance containing players to assign roles to.</param>
    /// <param name="random">Random source for shuffling role assignments.</param>
    /// <param name="variantIndex">Optional variant index (0 = default, 1+ = variant configurations).</param>
    /// <returns>The updated game instance with roles assigned, or null if assignment failed.</returns>
    Game? AssignRoles(Game game, Abstractions.IRandomSource random, int variantIndex = 0);

    /// <summary>
    /// Reveals the Lord's role (sets RoleRevealed = true for the Lord player).
    /// </summary>
    /// <param name="game">The game instance.</param>
    void RevealLordRole(Game game);
}

