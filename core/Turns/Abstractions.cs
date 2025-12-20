using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Turns;

/// <summary>
/// Immutable snapshot of the current turn and phase state.
/// This is a value view over the underlying <see cref="Model.Game"/> fields.
/// </summary>
public readonly record struct TurnState
{
    /// <summary>
    /// Seat index of the active player.
    /// </summary>
    public int CurrentPlayerSeat { get; init; }

    /// <summary>
    /// Current phase of the active player's turn.
    /// </summary>
    public Phase CurrentPhase { get; init; }

    /// <summary>
    /// Turn counter starting from 1.
    /// </summary>
    public int TurnNumber { get; init; }
}

/// <summary>
/// Optional finer grained phase descriptor to support future sub-phase extensions.
/// Currently unused by the basic engine implementation but provided for completeness.
/// </summary>
public readonly record struct PhaseState
{
    /// <summary>
    /// High-level phase.
    /// </summary>
    public Phase Phase { get; init; }

    /// <summary>
    /// Optional sub-phase discriminator within a high-level phase.
    /// 0 means "no specific sub-phase".
    /// </summary>
    public int SubPhase { get; init; }
}

/// <summary>
/// Error codes for turn/phase progression attempts.
/// </summary>
public enum TurnTransitionErrorCode
{
    None = 0,

    /// <summary>
    /// No alive players remain so a new turn cannot be started.
    /// </summary>
    NoAlivePlayers,

    /// <summary>
    /// Current phase cannot be ended or advanced yet according to the rules layer.
    /// </summary>
    PhaseCannotEndYet
}

/// <summary>
/// Result of a single turn/phase progression step.
/// </summary>
public readonly record struct TurnTransitionResult
{
    /// <summary>
    /// Whether the transition succeeded.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Resulting turn state after the transition.
    /// When <see cref="IsSuccess"/> is false this is typically the unchanged previous state.
    /// </summary>
    public TurnState TurnState { get; init; }

    /// <summary>
    /// Optional machine-readable error code.
    /// </summary>
    public TurnTransitionErrorCode ErrorCode { get; init; }
}

/// <summary>
/// Abstraction over the core turn/phase engine.
/// Concrete implementations drive turn order and phase progression
/// on top of the pure <see cref="Model.Game"/> state.
/// </summary>
public interface ITurnEngine
{
    /// <summary>
    /// Optional event bus for publishing turn and phase events.
    /// </summary>
    IEventBus? EventBus { get; set; }

    /// <summary>
    /// Initializes the turn-related fields on the given game instance
    /// and returns the initial <see cref="TurnState"/> snapshot.
    /// </summary>
    TurnState InitializeTurnState(Model.Game game);

    /// <summary>
    /// Advances the game from the current phase to the next one
    /// according to the engine's phase schedule.
    /// </summary>
    TurnTransitionResult AdvancePhase(Model.Game game);

    /// <summary>
    /// Starts the next player's turn, updating the underlying game state
    /// and returning the resulting <see cref="TurnState"/>.
    /// </summary>
    TurnTransitionResult StartNextTurn(Model.Game game);

    /// <summary>
    /// Returns a read-only snapshot of the current turn state derived from the game.
    /// </summary>
    TurnState GetCurrentTurnState(Model.Game game);

    /// <summary>
    /// Checks whether the current phase is allowed to end from the perspective
    /// of turn/phase structure (rules layer may still impose additional constraints).
    /// </summary>
    bool CanEndCurrentPhase(Model.Game game);
}
