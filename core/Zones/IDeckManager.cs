using System.Collections.Generic;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Zones;

/// <summary>
/// High-level service for managing draw pile and discard pile interactions.
/// Provides semantic APIs for drawing cards with automatic reshuffle support.
/// </summary>
public interface IDeckManager
{
    /// <summary>
    /// Gets the current count of cards in the draw pile.
    /// </summary>
    /// <param name="game">The game instance.</param>
    /// <returns>The number of cards in the draw pile.</returns>
    int GetDrawPileCount(Game game);

    /// <summary>
    /// Gets the current count of cards in the discard pile.
    /// </summary>
    /// <param name="game">The game instance.</param>
    /// <returns>The number of cards in the discard pile.</returns>
    int GetDiscardPileCount(Game game);

    /// <summary>
    /// Draws the specified number of cards from the draw pile.
    /// If the draw pile does not have enough cards, automatically reshuffles
    /// the discard pile and appends it to the draw pile to continue drawing.
    /// </summary>
    /// <param name="game">The game instance.</param>
    /// <param name="count">The number of cards to draw. Must be >= 0.</param>
    /// <returns>
    /// A list of drawn cards. The count may be less than requested if
    /// both draw pile and discard pile are exhausted.
    /// </returns>
    IReadOnlyList<Card> Draw(Game game, int count);
}
