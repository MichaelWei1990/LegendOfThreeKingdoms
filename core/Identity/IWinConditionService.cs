using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Identity;

/// <summary>
/// Service for checking win conditions in identity mode.
/// </summary>
public interface IWinConditionService
{
    /// <summary>
    /// Checks if any win condition has been met in the current game state.
    /// This should be called after a player's death is fully resolved.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <returns>Win condition result indicating if the game is over and who won.</returns>
    WinConditionResult CheckWinConditions(Game game);
}

