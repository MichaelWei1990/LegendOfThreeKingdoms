using System.Collections.Generic;

using LegendOfThreeKingdoms.Core.Model;

using LegendOfThreeKingdoms.Core.Model.Zones;



namespace LegendOfThreeKingdoms.Core.Zones;



/// <summary>

/// High-level reason for moving cards between zones.

/// This enum is intentionally coarse-grained and focuses on

/// "why" a move happens instead of low-level rule details.

/// </summary>

public enum CardMoveReason

{

    Unknown = 0,



    /// <summary>

    /// Drawing cards from a pile into a hand or other private zone.

    /// </summary>

    Draw,



    /// <summary>

    /// Discarding cards, typically from a hand or judgement/equipment zone

    /// into the global discard pile.

    /// </summary>

    Discard,



    /// <summary>

    /// Playing a card as part of using it (e.g. Slash, Peach) and

    /// sending it to the discard pile afterwards.

    /// </summary>

    Play,



    /// <summary>

    /// Moving a card into or out of a judgement zone as part of

    /// resolving a delayed trick or similar effect.

    /// </summary>

    Judgement,



    /// <summary>

    /// Returning a card to the top of a pile.

    /// </summary>

    ReturnToDeckTop,



    /// <summary>

    /// Returning a card to the bottom of a pile.

    /// </summary>

    ReturnToDeckBottom,

    /// <summary>

    /// Equipping a card (moving from hand to equipment zone).

    /// </summary>

    Equip

}



/// <summary>

/// Describes how moved cards should be inserted into the target zone.

/// </summary>

public enum CardMoveOrdering

{

    /// <summary>

    /// Append cards to the logical "top" of the target pile/zone.

    /// For draw piles this typically means they will be drawn last.

    /// </summary>

    ToTop = 0,



    /// <summary>

    /// Insert cards at the logical "bottom" of the target pile/zone.

    /// For draw piles this typically means they will be drawn first.

    /// </summary>

    ToBottom = 1,



    /// <summary>

    /// Preserve the relative order of the moved cards as they

    /// appeared in the source zone while inserting them consecutively

    /// into the target zone.

    /// </summary>

    PreserveRelativeOrder = 2

}

/// <summary>

/// Timing marker for card move events, indicating whether the event

/// is raised before or after the actual move operation.

/// </summary>

public enum CardMoveEventTiming

{

    /// <summary>

    /// Event raised before cards are moved (pre-move snapshot).

    /// </summary>

    Before = 0,

    /// <summary>

    /// Event raised after cards have been moved (post-move snapshot).

    /// </summary>

    After = 1

}

/// <summary>

/// Immutable event payload that describes a card move operation at a specific

/// point in time (before or after the move). This structure is designed to be

/// serializable and suitable for logging, replay, and event bus integration.

/// </summary>

public sealed record CardMoveEvent(

    string SourceZoneId,

    int? SourceOwnerSeat,

    string TargetZoneId,

    int? TargetOwnerSeat,

    IReadOnlyList<int> CardIds,

    CardMoveReason Reason,

    CardMoveOrdering Ordering,

    CardMoveEventTiming Timing

);



/// <summary>

/// Immutable description of a requested card move between two zones.

/// This is a pure data object; the service implementation is

/// responsible for validating and applying it to a <see cref="Game"/>.

/// </summary>

public sealed record CardMoveDescriptor(

    IZone SourceZone,

    IZone TargetZone,

    IReadOnlyList<Card> Cards,

    CardMoveReason Reason,

    CardMoveOrdering Ordering,

    Game? Game = null

);



/// <summary>

/// Result of executing a card move. Contains the descriptor, moved cards,

/// and optional event snapshots for Before/After move states. The event

/// fields are designed to support future event bus integration without

/// requiring changes to call sites.

/// </summary>

public sealed record CardMoveResult(

    CardMoveDescriptor Descriptor,

    IReadOnlyList<Card> MovedCards,

    CardMoveEvent? BeforeMoveEvent = null,

    CardMoveEvent? AfterMoveEvent = null

);



/// <summary>

/// Central service responsible for moving cards between zones in a

/// rules-agnostic way. It enforces basic consistency invariants such as

/// "a card belongs to at most one zone" but does not decide whether a

/// move is allowed from a gameplay perspective.

/// </summary>

public interface ICardMoveService

{

    /// <summary>

    /// Moves a single card from one zone to another according to the

    /// supplied descriptor.

    /// Implementations should validate that the card currently resides

    /// in the source zone and update both zones atomically.

    /// </summary>

    CardMoveResult MoveSingle(CardMoveDescriptor descriptor);



    /// <summary>

    /// Moves multiple cards described by a single descriptor in one

    /// atomic operation. Either all cards are moved or none are.

    /// </summary>

    CardMoveResult MoveMany(CardMoveDescriptor descriptor);



    /// <summary>

    /// Draws a number of cards from the game's global draw pile into

    /// the specified player's hand zone.

    /// Implementations are expected to honour the engine's convention

    /// of which end of the draw pile represents the top.

    /// </summary>

    IReadOnlyList<Card> DrawCards(Game game, Player player, int count);



    /// <summary>

    /// Discards the specified cards from the player's hand into the

    /// global discard pile. This is a convenience method that typically

    /// delegates to <see cref="MoveMany"/>.

    /// </summary>

    CardMoveResult DiscardFromHand(Game game, Player player, IReadOnlyList<Card> cards);

}

