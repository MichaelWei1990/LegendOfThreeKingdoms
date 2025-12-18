using System;

using System.Collections.Generic;

using System.Linq;

using LegendOfThreeKingdoms.Core.Model;

using LegendOfThreeKingdoms.Core.Model.Zones;



namespace LegendOfThreeKingdoms.Core.Zones;



/// <summary>

/// Basic in-memory implementation of <see cref="ICardMoveService"/> that

/// operates directly on the core <see cref="Game"/> and <see cref="Zone"/>

/// model types. This service is intentionally rules-agnostic and only

/// enforces structural invariants around card ownership and ordering.

/// </summary>

public sealed class BasicCardMoveService : ICardMoveService

{

    private readonly Action<CardMoveEvent>? _onBeforeMove;

    private readonly Action<CardMoveEvent>? _onAfterMove;

    /// <summary>

    /// Creates a basic card move service without event callbacks.

    /// </summary>

    public BasicCardMoveService()

        : this(onBeforeMove: null, onAfterMove: null)

    {

    }

    /// <summary>

    /// Creates a basic card move service with optional event callbacks.

    /// These callbacks are invoked during move operations to support

    /// future event bus integration. When null, no callbacks are invoked.

    /// </summary>

    public BasicCardMoveService(

        Action<CardMoveEvent>? onBeforeMove,

        Action<CardMoveEvent>? onAfterMove)

    {

        _onBeforeMove = onBeforeMove;

        _onAfterMove = onAfterMove;

    }

    public CardMoveResult MoveSingle(CardMoveDescriptor descriptor)

    {

        if (descriptor is null) throw new ArgumentNullException(nameof(descriptor));



        // For the basic implementation we delegate to MoveMany even for

        // single-card moves to keep all invariants in one place.

        return MoveMany(descriptor);

    }



    public CardMoveResult MoveMany(CardMoveDescriptor descriptor)

    {

        if (descriptor is null) throw new ArgumentNullException(nameof(descriptor));

        if (descriptor.Cards is null) throw new ArgumentException("Descriptor.Cards must not be null.", nameof(descriptor));



        if (descriptor.Cards.Count == 0)

        {

            // No-op move; return a trivial result to keep call sites simple.

            // For empty moves we do not generate events.

            return new CardMoveResult(descriptor, Array.Empty<Card>(), null, null);

        }



        if (descriptor.SourceZone is not Zone sourceZone)

        {

            throw new InvalidOperationException(

                "CardMoveService requires a mutable Zone as SourceZone. " +

                "The model should construct concrete Zone instances for all card-holding areas.");

        }



        if (descriptor.TargetZone is not Zone targetZone)

        {

            throw new InvalidOperationException(

                "CardMoveService requires a mutable Zone as TargetZone. " +

                "The model should construct concrete Zone instances for all card-holding areas.");

        }



        var sourceCards = sourceZone.MutableCards;

        var targetCards = targetZone.MutableCards;



        // Validate that the descriptor does not contain duplicate card instances.

        var seen = new HashSet<Card>();

        foreach (var card in descriptor.Cards)

        {

            if (card is null)

            {

                throw new ArgumentException("Descriptor.Cards must not contain null entries.", nameof(descriptor));

            }



            if (!seen.Add(card))

            {

                throw new InvalidOperationException("Descriptor.Cards contains duplicate card instances. A card may only be moved once per operation.");

            }

        }



        // Validate that all cards currently reside in the source zone and

        // are not already present in the target zone.

        foreach (var card in descriptor.Cards)

        {

            if (!sourceCards.Contains(card))

            {

                throw new InvalidOperationException(

                    $"Card with Id={card.Id} does not belong to the specified source zone '{sourceZone.ZoneId}'.");

            }



            if (targetCards.Contains(card))

            {

                throw new InvalidOperationException(

                    $"Card with Id={card.Id} is already present in target zone '{targetZone.ZoneId}'. " +

                    "A card must not belong to multiple zones at the same time.");

            }

        }

        // Generate BeforeMove event snapshot (read-only, before any state changes).

        var cardIds = descriptor.Cards.Select(c => c.Id).ToArray();

        var beforeEvent = new CardMoveEvent(

            SourceZoneId: sourceZone.ZoneId,

            SourceOwnerSeat: sourceZone.OwnerSeat,

            TargetZoneId: targetZone.ZoneId,

            TargetOwnerSeat: targetZone.OwnerSeat,

            CardIds: cardIds,

            Reason: descriptor.Reason,

            Ordering: descriptor.Ordering,

            Timing: CardMoveEventTiming.Before);

        // Invoke BeforeMove callback if provided.

        _onBeforeMove?.Invoke(beforeEvent);

        // Remove cards from the source zone. We remove by instance reference

        // rather than index so we do not depend on any particular ordering

        // in the descriptor.

        foreach (var card in descriptor.Cards)

        {

            // List.Remove returns false only if the card is not found, which

            // should have been guarded by the validation above.

            if (!sourceCards.Remove(card))

            {

                throw new InvalidOperationException(

                    $"Failed to remove card with Id={card.Id} from source zone '{sourceZone.ZoneId}'.");

            }

        }



        // Insert into the target zone according to the requested ordering.

        var moved = new List<Card>(descriptor.Cards.Count);

        switch (descriptor.Ordering)

        {

            case CardMoveOrdering.ToTop:

                // Engine convention: index 0 is the logical "top" of a pile

                // (e.g. DrawPile[0] is drawn first). We therefore insert new

                // cards at the front while preserving their relative order.

                for (var i = descriptor.Cards.Count - 1; i >= 0; i--)

                {

                    var card = descriptor.Cards[i];

                    targetCards.Insert(0, card);

                    moved.Add(card);

                }

                break;



            case CardMoveOrdering.ToBottom:

                foreach (var card in descriptor.Cards)

                {

                    targetCards.Add(card);

                    moved.Add(card);

                }

                break;



            case CardMoveOrdering.PreserveRelativeOrder:

                // For the basic implementation we treat PreserveRelativeOrder

                // as appending to the bottom of the target zone while keeping

                // the order of the moved cards intact.

                foreach (var card in descriptor.Cards)

                {

                    targetCards.Add(card);

                    moved.Add(card);

                }

                break;



            default:

                throw new ArgumentOutOfRangeException(nameof(descriptor), "Unknown CardMoveOrdering value.");

        }

        // Generate AfterMove event snapshot (read-only, after state changes).

        var afterEvent = new CardMoveEvent(

            SourceZoneId: sourceZone.ZoneId,

            SourceOwnerSeat: sourceZone.OwnerSeat,

            TargetZoneId: targetZone.ZoneId,

            TargetOwnerSeat: targetZone.OwnerSeat,

            CardIds: cardIds,

            Reason: descriptor.Reason,

            Ordering: descriptor.Ordering,

            Timing: CardMoveEventTiming.After);

        // Invoke AfterMove callback if provided.

        _onAfterMove?.Invoke(afterEvent);

        return new CardMoveResult(descriptor, moved, beforeEvent, afterEvent);

    }



    public IReadOnlyList<Card> DrawCards(Game game, Player player, int count)

    {

        if (game is null) throw new ArgumentNullException(nameof(game));

        if (player is null) throw new ArgumentNullException(nameof(player));

        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));



        if (count == 0)

        {

            return Array.Empty<Card>();

        }



        if (game.DrawPile is not Zone drawZone)

        {

            throw new InvalidOperationException("Game.DrawPile must be a mutable Zone instance.");

        }



        if (player.HandZone is not Zone handZone)

        {

            throw new InvalidOperationException("Player.HandZone must be a mutable Zone instance.");

        }



        var drawCards = drawZone.MutableCards;

        if (drawCards.Count < count)

        {

            throw new InvalidOperationException(

                $"Draw pile does not contain enough cards to draw {count} card(s). Available={drawCards.Count}.");

        }



        var drawn = new List<Card>(count);



        // Engine convention: index 0 is the top of the draw pile and is drawn first.

        for (var i = 0; i < count; i++)

        {

            var card = drawCards[0];

            drawCards.RemoveAt(0);

            handZone.MutableCards.Add(card);

            drawn.Add(card);

        }



        return drawn;

    }



    public CardMoveResult DiscardFromHand(Game game, Player player, IReadOnlyList<Card> cards)

    {

        if (game is null) throw new ArgumentNullException(nameof(game));

        if (player is null) throw new ArgumentNullException(nameof(player));

        if (cards is null) throw new ArgumentNullException(nameof(cards));



        if (cards.Count == 0)

        {

            var emptyDescriptor = new CardMoveDescriptor(

                SourceZone: player.HandZone,

                TargetZone: game.DiscardPile,

                Cards: Array.Empty<Card>(),

                Reason: CardMoveReason.Discard,

                Ordering: CardMoveOrdering.ToTop);



            return new CardMoveResult(emptyDescriptor, Array.Empty<Card>(), null, null);

        }



        if (player.HandZone is not Zone handZone)

        {

            throw new InvalidOperationException("Player.HandZone must be a mutable Zone instance.");

        }



        if (game.DiscardPile is not Zone discardZone)

        {

            throw new InvalidOperationException("Game.DiscardPile must be a mutable Zone instance.");

        }



        var descriptor = new CardMoveDescriptor(

            SourceZone: handZone,

            TargetZone: discardZone,

            Cards: cards,

            Reason: CardMoveReason.Discard,

            Ordering: CardMoveOrdering.ToTop);



        return MoveMany(descriptor);

    }

}

