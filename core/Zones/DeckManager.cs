using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;

namespace LegendOfThreeKingdoms.Core.Zones;

/// <summary>
/// Implementation of IDeckManager that provides draw pile and discard pile management
/// with automatic reshuffle support when the draw pile is exhausted.
/// </summary>
public sealed class DeckManager : IDeckManager
{
    private readonly ICardMoveService _cardMoveService;
    private readonly IRandomSource _random;
    private readonly IEventBus? _eventBus;

    /// <summary>
    /// Creates a new DeckManager with required dependencies.
    /// </summary>
    /// <param name="cardMoveService">Service for moving cards between zones.</param>
    /// <param name="random">Random source for shuffling the discard pile.</param>
    /// <param name="eventBus">Optional event bus for publishing events.</param>
    public DeckManager(
        ICardMoveService cardMoveService,
        IRandomSource random,
        IEventBus? eventBus = null)
    {
        _cardMoveService = cardMoveService ?? throw new ArgumentNullException(nameof(cardMoveService));
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _eventBus = eventBus;
    }

    /// <inheritdoc />
    public int GetDrawPileCount(Game game)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        return game.DrawPile.Cards.Count;
    }

    /// <inheritdoc />
    public int GetDiscardPileCount(Game game)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        return game.DiscardPile.Cards.Count;
    }

    /// <inheritdoc />
    public IReadOnlyList<Card> Draw(Game game, int count)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), "Count must be non-negative.");

        if (count == 0)
        {
            return Array.Empty<Card>();
        }

        var drawn = new List<Card>();
        int remaining = count;

        while (remaining > 0)
        {
            var drawPile = game.DrawPile as Zone;
            if (drawPile is null)
            {
                throw new InvalidOperationException("Game.DrawPile must be a mutable Zone instance.");
            }

            var available = drawPile.MutableCards.Count;

            if (available >= remaining)
            {
                // Draw directly from draw pile
                DrawFromPile(drawPile, remaining, drawn);
                break;
            }
            else
            {
                // Draw all remaining cards from draw pile first
                if (available > 0)
                {
                    DrawFromPile(drawPile, available, drawn);
                    remaining -= available;
                }

                // Try to reshuffle discard pile into draw pile
                var discardPile = game.DiscardPile as Zone;
                if (discardPile is not null && discardPile.MutableCards.Count > 0)
                {
                    ReshuffleDiscardIntoDraw(game);
                }
                else
                {
                    // Discard pile is empty, cannot replenish
                    // Return whatever we've drawn so far
                    break;
                }
            }
        }

        return drawn;
    }

    /// <summary>
    /// Draws cards from the draw pile (from index 0, which is the top).
    /// </summary>
    private static void DrawFromPile(Zone drawPile, int count, List<Card> drawn)
    {
        var cards = drawPile.MutableCards;
        for (int i = 0; i < count; i++)
        {
            var card = cards[0];
            cards.RemoveAt(0);
            drawn.Add(card);
        }
    }

    /// <summary>
    /// Reshuffles the discard pile and appends it to the bottom of the draw pile.
    /// Uses Fisher-Yates shuffle algorithm for randomization.
    /// </summary>
    private void ReshuffleDiscardIntoDraw(Game game)
    {
        var discardZone = game.DiscardPile as Zone;
        var drawZone = game.DrawPile as Zone;

        if (discardZone is null || drawZone is null)
        {
            return;
        }

        var discardCards = discardZone.MutableCards;
        int count = discardCards.Count;

        if (count == 0)
        {
            return;
        }

        // Create a copy of the discard cards list to avoid modifying during iteration
        var cardsToMove = discardCards.ToList();

        // Fisher-Yates shuffle
        for (int i = count - 1; i > 0; i--)
        {
            int j = _random.NextInt(0, i + 1);
            (cardsToMove[i], cardsToMove[j]) = (cardsToMove[j], cardsToMove[i]);
        }

        // Move all cards from discard pile to draw pile (to bottom)
        var descriptor = new CardMoveDescriptor(
            SourceZone: discardZone,
            TargetZone: drawZone,
            Cards: cardsToMove,
            Reason: CardMoveReason.ReturnToDeckBottom,
            Ordering: CardMoveOrdering.ToBottom,
            Game: game
        );

        _cardMoveService.MoveMany(descriptor);
    }
}
