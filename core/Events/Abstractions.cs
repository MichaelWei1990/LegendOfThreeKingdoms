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
/// Event published when the draw phase is replaced by a skill (e.g., Tuxi).
/// This event is published when a player chooses to use a skill to replace normal card drawing.
/// </summary>
public sealed record DrawPhaseReplacedEvent(
    Game Game,
    int PlayerSeat,
    string ReplacementReason,
    IReadOnlyList<int> TargetSeats,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}

/// <summary>
/// Event published right before damage is applied (before reducing HP / triggering dying).
/// This event allows skills to prevent or modify damage before it is applied.
/// Used by skills like Ice Sword (寒冰剑) that can prevent damage and replace it with other effects.
/// </summary>
public sealed record BeforeDamageEvent(
    Game Game,
    DamageDescriptor Damage,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;

    /// <summary>
    /// Whether this damage has been prevented by a skill.
    /// If set to true, the damage will not be applied (amount becomes 0).
    /// </summary>
    public bool IsPrevented { get; set; }

    /// <summary>
    /// Damage amount modification to apply.
    /// This value will be added to the original damage amount.
    /// Can be positive (increase damage) or negative (decrease damage).
    /// Multiple skills can modify damage, and their modifications are cumulative.
    /// </summary>
    public int DamageModification { get; set; }
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
/// Event published after damage is fully resolved, including dying process if applicable.
/// This event is published after:
/// - DamageResolver completes (if no dying)
/// - DyingRescueHandlerResolver completes successfully (if dying was triggered and player was saved)
/// This event is used by skills that need to react after damage and dying are fully resolved,
/// such as Feedback (反馈).
/// </summary>
public sealed record AfterDamageEvent(
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
/// Event published when a player loses HP (not from damage).
/// This is distinct from damage events and should not trigger skills that respond to damage.
/// Used by skills like Kurou (苦肉) that lose HP as a cost.
/// </summary>
public sealed record HpLostEvent(
    Game Game,
    int TargetSeat,
    int Amount,
    int PreviousHealth,
    int CurrentHealth,
    object? Source,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}

/// <summary>
/// Event published after HP loss is fully resolved, including dying process if applicable.
/// This event is published after:
/// - LoseHpResolver completes (if no dying)
/// - DyingRescueHandlerResolver completes successfully (if dying was triggered and player was saved)
/// This event is used by skills that need to react after HP loss and dying are fully resolved.
/// </summary>
public sealed record AfterHpLostEvent(
    Game Game,
    int TargetSeat,
    int Amount,
    int PreviousHealth,
    int CurrentHealth,
    object? Source,
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
/// Event published when a judgement card is revealed (after being drawn and placed in JudgementZone).
/// This event is published before the judgement result is calculated, allowing skills to modify the judgement card.
/// </summary>
public sealed record JudgementCardRevealedEvent(
    Game Game,
    Guid JudgementId,
    int JudgeOwnerSeat,
    Card JudgementCard,
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

/// <summary>
/// Event published after card targets are finalized (locked), before targets start responding.
/// This event is published when a card is used and its targets have been determined,
/// but before response windows are opened. Used by skills like Twin Swords (雌雄双股剑)
/// that need to interact with targets before they respond.
/// </summary>
public sealed record AfterCardTargetsDeclaredEvent(
    Game Game,
    int SourcePlayerSeat,
    Card Card,
    IReadOnlyList<int> TargetSeats,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}

/// <summary>
/// Event published when a player becomes a target of a Slash.
/// This event is published after targets are declared but before the response window,
/// allowing skills like Liuli (流离) to redirect the Slash to another target.
/// </summary>
public sealed record SlashTargetedEvent(
    Game Game,
    int AttackerSeat,
    int TargetSeat,
    Card SlashCard,
    DamageDescriptor PendingDamage,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}

/// <summary>
/// Event published when a Slash is dodged by a Dodge (闪) response.
/// This event is published after the Dodge successfully cancels the Slash,
/// before the damage would have been applied. Used by skills like Stone Axe (贯石斧)
/// that can force damage even after a successful Dodge.
/// </summary>
public sealed record AfterSlashDodgedEvent(
    Game Game,
    int AttackerSeat,
    int TargetSeat,
    Card SlashCard,
    DamageDescriptor OriginalDamage,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}

/// <summary>
/// Event published when a Slash is negated by a Jink (闪) response.
/// This event is published when a Slash is successfully dodged by a Jink,
/// allowing skills like Qinglong Yanyue Dao (青龙偃月刀) to trigger a chase effect.
/// </summary>
public sealed record SlashNegatedByJinkEvent(
    Game Game,
    Player Source,
    Player Target,
    Card SlashCard,
    bool DistanceWasChecked = false,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}

/// <summary>
/// Event published right before health recovery is applied.
/// This event allows skills to modify the recovery amount before it is applied.
/// Used by skills like Rescue (救援) that can increase recovery amount.
/// </summary>
public sealed record BeforeRecoverEvent(
    Game Game,
    Player Source,
    Player Target,
    int BaseAmount,
    Card EffectCard,
    object? Reason,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;

    /// <summary>
    /// Recovery amount modification to apply.
    /// This value will be added to the base recovery amount.
    /// Can be positive (increase recovery) or negative (decrease recovery).
    /// Multiple skills can modify recovery, and their modifications are cumulative.
    /// </summary>
    public int RecoveryModification { get; set; }
}

/// <summary>
/// Event published when candidate characters are offered to a player for selection.
/// </summary>
public sealed record CharactersOfferedEvent(
    Game Game,
    int PlayerSeat,
    IReadOnlyList<string> CandidateCharacterIds,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}

/// <summary>
/// Event published when a player selects a character.
/// </summary>
public sealed record CharacterSelectedEvent(
    Game Game,
    int PlayerSeat,
    string CharacterId,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}

/// <summary>
/// Event published when skills are registered for a player after character selection.
/// </summary>
public sealed record SkillsRegisteredEvent(
    Game Game,
    int PlayerSeat,
    IReadOnlyList<string> SkillIds,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}

/// <summary>
/// Event published when a game is created.
/// </summary>
public sealed record GameCreatedEvent(
    Game Game,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}

/// <summary>
/// Event published when identities have been assigned to all players.
/// </summary>
public sealed record IdentitiesAssignedEvent(
    Game Game,
    IReadOnlyList<(int PlayerSeat, string Role)> Assignments,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}

/// <summary>
/// Event published when the Lord's role is revealed.
/// </summary>
public sealed record LordRevealedEvent(
    Game Game,
    int LordPlayerSeat,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}

/// <summary>
/// Event published when the game starts (after all heroes are selected).
/// </summary>
public sealed record GameStartedEvent(
    Game Game,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}

/// <summary>
/// Event published when the game ends due to a win condition being met.
/// </summary>
public sealed record GameEndedEvent(
    Game Game,
    Identity.WinType WinType,
    IReadOnlyList<int> WinningPlayerSeats,
    string EndReason,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}

/// <summary>
/// Event published before Jie Dao Sha Ren (借刀杀人) effect is executed.
/// This event is published after nullification check passes, before forcing slash use or weapon transfer.
/// </summary>
public sealed record BeforeJieDaoShaRenEffectEvent(
    Game Game,
    int UserSeat,
    int TargetASeat,
    int TargetBSeat,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}

/// <summary>
/// Event published when a forced slash use is requested in Jie Dao Sha Ren effect.
/// This event is published when player A is asked to use Slash on player B.
/// </summary>
public sealed record ForcedSlashUseRequestedEvent(
    Game Game,
    int ActorSeat,
    int TargetSeat,
    int RequesterSeat,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}

/// <summary>
/// Event published when a forced slash use is resolved in Jie Dao Sha Ren effect.
/// This event indicates whether the actor used slash or declined (resulting in weapon transfer).
/// </summary>
public sealed record ForcedSlashUseResolvedEvent(
    Game Game,
    int ActorSeat,
    int TargetSeat,
    int RequesterSeat,
    bool SlashUsed,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}

/// <summary>
/// Event published when a weapon is transferred due to Jie Dao Sha Ren effect.
/// This event is published when player A declines to use slash or cannot use slash,
/// resulting in their weapon being transferred to the requester.
/// </summary>
public sealed record WeaponTransferredEvent(
    Game Game,
    int FromSeat,
    int ToSeat,
    int WeaponCardId,
    CardSubType WeaponSubType,
    string Reason,
    DateTime Timestamp = default
) : IGameEvent
{
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.UtcNow : Timestamp;
}