namespace LegendOfThreeKingdoms.Core.Model;

/// <summary>
/// Extension methods for card-related types.
/// </summary>
public static class CardExtensions
{
    /// <summary>
    /// Determines whether a suit is black (Spade or Club).
    /// </summary>
    /// <param name="suit">The suit to check.</param>
    /// <returns>True if the suit is black, false otherwise.</returns>
    public static bool IsBlack(this Suit suit) => suit == Suit.Spade || suit == Suit.Club;

    /// <summary>
    /// Determines whether a suit is red (Heart or Diamond).
    /// </summary>
    /// <param name="suit">The suit to check.</param>
    /// <returns>True if the suit is red, false otherwise.</returns>
    public static bool IsRed(this Suit suit) => suit == Suit.Heart || suit == Suit.Diamond;
}
