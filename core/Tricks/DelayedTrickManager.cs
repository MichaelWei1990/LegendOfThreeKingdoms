using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Tricks;

/// <summary>
/// Manager for tracking and querying delayed tricks in players' judgement zones.
/// Provides helper methods to query delayed tricks for a specific player.
/// </summary>
public sealed class DelayedTrickManager
{
    /// <summary>
    /// Gets all delayed trick cards in the specified player's judgement zone.
    /// </summary>
    /// <param name="player">The player whose delayed tricks are requested.</param>
    /// <returns>A list of cards that are delayed tricks in the player's judgement zone.</returns>
    public IReadOnlyList<Card> GetDelayedTricks(Player player)
    {
        if (player is null) throw new System.ArgumentNullException(nameof(player));

        // Query cards in judgement zone that are delayed tricks
        return player.JudgementZone.Cards
            .Where(card => IsDelayedTrick(card))
            .ToList();
    }

    /// <summary>
    /// Checks if a card is a delayed trick.
    /// A card is a delayed trick if it's a Trick card and its CardSubType indicates it's a delayed trick.
    /// </summary>
    /// <param name="card">The card to check.</param>
    /// <returns>True if the card is a delayed trick, false otherwise.</returns>
    public static bool IsDelayedTrick(Card card)
    {
        if (card is null) return false;

        if (card.CardType != CardType.Trick)
            return false;

        // Check if CardSubType is DelayedTrick (generic delayed trick)
        if (card.CardSubType == CardSubType.DelayedTrick)
            return true;

        // Check if CardSubType is a specific delayed trick card type
        // Note: This matches the logic in UseCardResolver where these types are routed to DelayedTrickResolver
        return IsSpecificDelayedTrickSubType(card.CardSubType);
    }

    /// <summary>
    /// Checks if a CardSubType represents a specific delayed trick card.
    /// </summary>
    /// <param name="cardSubType">The card subtype to check.</param>
    /// <returns>True if the subtype is a specific delayed trick, false otherwise.</returns>
    private static bool IsSpecificDelayedTrickSubType(CardSubType cardSubType)
    {
        return cardSubType switch
        {
            CardSubType.Lebusishu => true,
            CardSubType.Shandian => true,
            _ => false
        };
    }

    /// <summary>
    /// Checks if a card is an immediate trick.
    /// A card is an immediate trick if it's a Trick card and its CardSubType indicates it's an immediate trick.
    /// </summary>
    /// <param name="card">The card to check.</param>
    /// <returns>True if the card is an immediate trick, false otherwise.</returns>
    public static bool IsImmediateTrick(Card card)
    {
        if (card is null) return false;

        if (card.CardType != CardType.Trick)
            return false;

        // Check if CardSubType is ImmediateTrick (generic immediate trick)
        if (card.CardSubType == CardSubType.ImmediateTrick)
            return true;

        // Check if CardSubType is a specific immediate trick card type
        // Note: This matches the logic in UseCardResolver where these types are routed to ImmediateTrickResolver
        return IsSpecificImmediateTrickSubType(card.CardSubType);
    }

    /// <summary>
    /// Checks if a CardSubType represents a specific immediate trick card.
    /// </summary>
    /// <param name="cardSubType">The card subtype to check.</param>
    /// <returns>True if the subtype is a specific immediate trick, false otherwise.</returns>
    private static bool IsSpecificImmediateTrickSubType(CardSubType cardSubType)
    {
        return cardSubType switch
        {
            CardSubType.WuzhongShengyou => true,
            CardSubType.TaoyuanJieyi => true,
            CardSubType.ShunshouQianyang => true,
            CardSubType.GuoheChaiqiao => true,
            _ => false
        };
    }
}
