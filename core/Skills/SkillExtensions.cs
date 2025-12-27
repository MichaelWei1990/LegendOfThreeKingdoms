using System;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Skills;

/// <summary>
/// Extension methods for skills to perform common card movement operations.
/// </summary>
public static class SkillExtensions
{
    /// <summary>
    /// Moves a card to the owner's hand zone.
    /// Searches for the card in all possible zones (discard pile, hand zones, equipment zones, judgement zones)
    /// and moves it to the target player's hand.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="owner">The player who will receive the card in their hand.</param>
    /// <param name="card">The card to move.</param>
    /// <param name="cardMoveService">The card move service to perform the move.</param>
    public static void MoveCardToHand(this Game game, Player owner, Card card, ICardMoveService cardMoveService)
    {
        if (game is null)
            throw new ArgumentNullException(nameof(game));
        if (owner is null)
            throw new ArgumentNullException(nameof(owner));
        if (card is null)
            throw new ArgumentNullException(nameof(card));
        if (cardMoveService is null)
            throw new ArgumentNullException(nameof(cardMoveService));

        // Find the source zone containing the card
        IZone? sourceZone = null;

        // Check discard pile first (most common case for skills like Jianxiong)
        if (game.DiscardPile.Cards.Contains(card))
        {
            sourceZone = game.DiscardPile;
        }
        else
        {
            // Search all zones for the card
            // This handles cases where the card might be in a resolution/in-play zone,
            // hand zone, equipment zone, or judgement zone
            foreach (var player in game.Players)
            {
                if (player.HandZone.Cards.Contains(card))
                {
                    sourceZone = player.HandZone;
                    break;
                }
                if (player.EquipmentZone.Cards.Contains(card))
                {
                    sourceZone = player.EquipmentZone;
                    break;
                }
                if (player.JudgementZone.Cards.Contains(card))
                {
                    sourceZone = player.JudgementZone;
                    break;
                }
            }
        }

        // If source zone not found, cannot move
        if (sourceZone is null)
            return;

        // Ensure target hand zone is valid
        if (owner.HandZone is not Zone targetHandZone)
            return;

        // Move the card to owner's hand
        var moveDescriptor = new CardMoveDescriptor(
            SourceZone: sourceZone,
            TargetZone: targetHandZone,
            Cards: new[] { card },
            Reason: CardMoveReason.Draw, // Using Draw reason for obtaining cards
            Ordering: CardMoveOrdering.ToTop,
            Game: game
        );

        cardMoveService.MoveMany(moveDescriptor);
    }
}
