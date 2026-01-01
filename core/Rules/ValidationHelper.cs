using System;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Rules;

/// <summary>
/// Helper class for common validation operations in the Rules layer.
/// Provides unified validation patterns to reduce code duplication.
/// </summary>
public static class ValidationHelper
{
    /// <summary>
    /// Validates that a rule context is not null.
    /// </summary>
    /// <param name="context">The context to validate.</param>
    /// <param name="paramName">The parameter name for error messages.</param>
    /// <exception cref="ArgumentNullException">Thrown if context is null.</exception>
    public static void ValidateContext(RuleContext? context, string paramName = "context")
    {
        if (context is null)
            throw new ArgumentNullException(paramName);
    }

    /// <summary>
    /// Validates that a card usage context is not null.
    /// </summary>
    /// <param name="context">The context to validate.</param>
    /// <param name="paramName">The parameter name for error messages.</param>
    /// <exception cref="ArgumentNullException">Thrown if context is null.</exception>
    public static void ValidateContext(CardUsageContext? context, string paramName = "context")
    {
        if (context is null)
            throw new ArgumentNullException(paramName);
    }

    /// <summary>
    /// Validates that a response context is not null.
    /// </summary>
    /// <param name="context">The context to validate.</param>
    /// <param name="paramName">The parameter name for error messages.</param>
    /// <exception cref="ArgumentNullException">Thrown if context is null.</exception>
    public static void ValidateContext(ResponseContext? context, string paramName = "context")
    {
        if (context is null)
            throw new ArgumentNullException(paramName);
    }

    /// <summary>
    /// Validates that a player is not null.
    /// </summary>
    /// <param name="player">The player to validate.</param>
    /// <param name="paramName">The parameter name for error messages.</param>
    /// <exception cref="ArgumentNullException">Thrown if player is null.</exception>
    public static void ValidatePlayer(Player? player, string paramName = "player")
    {
        if (player is null)
            throw new ArgumentNullException(paramName);
    }

    /// <summary>
    /// Validates that a game is not null.
    /// </summary>
    /// <param name="game">The game to validate.</param>
    /// <param name="paramName">The parameter name for error messages.</param>
    /// <exception cref="ArgumentNullException">Thrown if game is null.</exception>
    public static void ValidateGame(Game? game, string paramName = "game")
    {
        if (game is null)
            throw new ArgumentNullException(paramName);
    }

    /// <summary>
    /// Validates that a card is not null.
    /// </summary>
    /// <param name="card">The card to validate.</param>
    /// <param name="paramName">The parameter name for error messages.</param>
    /// <exception cref="ArgumentNullException">Thrown if card is null.</exception>
    public static void ValidateCard(Card? card, string paramName = "card")
    {
        if (card is null)
            throw new ArgumentNullException(paramName);
    }

    /// <summary>
    /// Validates that both players are not null and are distinct.
    /// </summary>
    /// <param name="from">The source player.</param>
    /// <param name="to">The target player.</param>
    /// <exception cref="ArgumentNullException">Thrown if either player is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if both players are the same.</exception>
    public static void ValidateDistinctPlayers(Player? from, Player? to)
    {
        if (from is null)
            throw new ArgumentNullException(nameof(from));
        if (to is null)
            throw new ArgumentNullException(nameof(to));
        if (ReferenceEquals(from, to) || from.Seat == to.Seat)
        {
            throw new InvalidOperationException(
                "Source and target players must be distinct. " +
                "Seat distance and attack range are only defined between different players.");
        }
    }
}
