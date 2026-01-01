using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Turns;

/// <summary>
/// Interface for executing a player's turn.
/// In the minimal identity mode flow, this provides an extension point
/// for turn execution logic without requiring complex card/skill resolution.
/// </summary>
public interface ITurnExecutor
{
    /// <summary>
    /// Executes a turn for the given player.
    /// In minimal flow, this can be a no-op or a placeholder for future expansion.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="player">The player whose turn is being executed.</param>
    void ExecuteTurn(Game game, Player player);
}
