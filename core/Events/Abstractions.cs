using System;
using LegendOfThreeKingdoms.Core.Judgement;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Events;

/// <summary>
/// Base interface for all game events.
/// All events must implement this interface to be publishable through the event bus.
/// </summary>
public interface IGameEvent
{
    /// <summary>
    /// The game instance this event belongs to.
    /// </summary>
    Game Game { get; }

    /// <summary>
    /// Timestamp when the event occurred (UTC).
    /// </summary>
    DateTime Timestamp { get; }
}

/// <summary>
/// Event bus interface for publishing and subscribing to game events.
/// Supports synchronous, single-threaded event distribution.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publishes an event to all subscribers of the event type.
    /// </summary>
    /// <typeparam name="T">The type of event to publish.</typeparam>
    /// <param name="gameEvent">The event instance to publish.</param>
    void Publish<T>(T gameEvent) where T : IGameEvent;

    /// <summary>
    /// Subscribes a handler to events of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of event to subscribe to.</typeparam>
    /// <param name="handler">The handler to invoke when events of type T are published.</param>
    void Subscribe<T>(Action<T> handler) where T : IGameEvent;

    /// <summary>
    /// Unsubscribes a handler from events of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of event to unsubscribe from.</typeparam>
    /// <param name="handler">The handler to remove from subscriptions.</param>
    void Unsubscribe<T>(Action<T> handler) where T : IGameEvent;
}

/// <summary>
/// Event published when a player's turn starts.
/// </summary>
public sealed record TurnStartEvent(
    Game Game,
    int PlayerSeat,
    int TurnNumber,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}

/// <summary>
/// Event published when a player's turn ends.
/// </summary>
public sealed record TurnEndEvent(
    Game Game,
    int PlayerSeat,
    int TurnNumber,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}

/// <summary>
/// Event published when a phase starts.
/// </summary>
public sealed record PhaseStartEvent(
    Game Game,
    int PlayerSeat,
    Phase Phase,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}

/// <summary>
/// Event published when a phase ends.
/// </summary>
public sealed record PhaseEndEvent(
    Game Game,
    int PlayerSeat,
    Phase Phase,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}

/// <summary>
/// Event published when damage is created (before it is applied).
/// </summary>
public sealed record DamageCreatedEvent(
    Game Game,
    DamageDescriptor Damage,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}

/// <summary>
/// Event published when damage is applied to a player.
/// </summary>
public sealed record DamageAppliedEvent(
    Game Game,
    DamageDescriptor Damage,
    int PreviousHealth,
    int CurrentHealth,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}

/// <summary>
/// Event published when damage is resolved (after all damage-related effects are processed).
/// This event is published after DamageAppliedEvent and is used by skills that need to react
/// to completed damage, such as Jianxiong (奸雄).
/// </summary>
public sealed record DamageResolvedEvent(
    Game Game,
    DamageDescriptor Damage,
    int PreviousHealth,
    int CurrentHealth,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}

/// <summary>
/// Event published when a player enters the dying state.
/// </summary>
public sealed record DyingStartEvent(
    Game Game,
    int DyingPlayerSeat,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}

/// <summary>
/// Event published when a player dies.
/// </summary>
public sealed record PlayerDiedEvent(
    Game Game,
    int DeadPlayerSeat,
    int? KillerSeat,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}

/// <summary>
/// Event published when cards are moved between zones.
/// This event wraps the existing CardMoveEvent structure to implement IGameEvent.
/// </summary>
public sealed record CardMovedEvent(
    Game Game,
    CardMoveEvent CardMoveEvent,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}

/// <summary>
/// Event published when a judgement starts.
/// </summary>
public sealed record JudgementStartedEvent(
    Game Game,
    Guid JudgementId,
    int JudgeOwnerSeat,
    JudgementReason Reason,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}

/// <summary>
/// Event published when a judgement completes.
/// </summary>
public sealed record JudgementCompletedEvent(
    Game Game,
    Guid JudgementId,
    JudgementResult Result,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}

/// <summary>
/// Event published when a delayed trick is placed in a player's judgement zone.
/// </summary>
public sealed record DelayedTrickPlacedEvent(
    Game Game,
    int SourcePlayerSeat,
    int TargetPlayerSeat,
    int CardId,
    CardSubType CardSubType,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}

/// <summary>
/// Event published when a card is used (主动使用) by a player.
/// </summary>
public sealed record CardUsedEvent(
    Game Game,
    int SourcePlayerSeat,
    int CardId,
    CardSubType CardSubType,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}

/// <summary>
/// Event published when a card is played (打出) in response by a player.
/// </summary>
public sealed record CardPlayedEvent(
    Game Game,
    int ResponderSeat,
    int CardId,
    CardSubType CardSubType,
    ResponseType ResponseType,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}
