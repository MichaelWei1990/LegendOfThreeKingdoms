using System;
using System.Collections.Generic;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Judgement;

/// <summary>
/// Reason for triggering a judgement.
/// </summary>
public enum JudgementReason
{
    /// <summary>
    /// Delayed trick (延时锦囊).
    /// </summary>
    DelayedTrick = 0,

    /// <summary>
    /// Skill trigger (技能触发).
    /// </summary>
    Skill,

    /// <summary>
    /// Armor trigger (防具触发).
    /// </summary>
    Armor,

    /// <summary>
    /// Weapon trigger (武器触发).
    /// </summary>
    Weapon,

    /// <summary>
    /// Other reason.
    /// </summary>
    Other
}

/// <summary>
/// Interface for effect sources that trigger judgements.
/// Used to identify the source of a judgement (delayed trick, skill, equipment, etc.).
/// </summary>
public interface IEffectSource
{
    /// <summary>
    /// Unique identifier of the effect source.
    /// </summary>
    string SourceId { get; }

    /// <summary>
    /// Type of the effect source (e.g., "DelayedTrick", "Skill", "Equipment").
    /// </summary>
    string SourceType { get; }

    /// <summary>
    /// Display name of the effect source (for logging/UI).
    /// </summary>
    string? DisplayName { get; }
}

/// <summary>
/// Request to execute a judgement.
/// Contains all information needed to perform a judgement.
/// </summary>
public sealed record JudgementRequest(
    Guid JudgementId,
    int JudgeOwnerSeat,
    JudgementReason Reason,
    IEffectSource Source,
    IJudgementRule Rule,
    IReadOnlyDictionary<string, string>? Tags = null,
    bool AllowModify = true
);

/// <summary>
/// Record of a judgement modification (for future use when implementing modification skills).
/// </summary>
public sealed record JudgementModificationRecord(
    int ModifierSeat,
    string ModifierSource,
    Card? OriginalCard,
    Card? ModifiedCard,
    DateTime Timestamp
);

/// <summary>
/// Result of a judgement execution.
/// Contains the original card, final card (after modifications), and success status.
/// </summary>
public sealed record JudgementResult(
    Guid JudgementId,
    int JudgeOwnerSeat,
    Card OriginalCard,
    Card FinalCard,
    bool IsSuccess,
    string RuleSnapshot,
    IReadOnlyList<JudgementModificationRecord> ModifiersApplied
);

/// <summary>
/// Interface for judgement rules that determine success/failure based on card properties.
/// </summary>
public interface IJudgementRule
{
    /// <summary>
    /// Human-readable description of the rule (e.g., "红色判定成功").
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Evaluates whether the judgement is successful for the given card.
    /// </summary>
    /// <param name="card">The card to evaluate.</param>
    /// <returns>True if the judgement is successful, false otherwise.</returns>
    bool Evaluate(Card card);
}

/// <summary>
/// Interface for the judgement service that executes judgements.
/// </summary>
public interface IJudgementService
{
    /// <summary>
    /// Executes a judgement: draws a card from the top of the draw pile, places it in JudgementZone, and calculates the result.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="judgeOwner">The player who owns this judgement.</param>
    /// <param name="request">The judgement request.</param>
    /// <param name="cardMoveService">The card move service for moving cards.</param>
    /// <returns>The judgement result.</returns>
    JudgementResult ExecuteJudgement(
        Game game,
        Player judgeOwner,
        JudgementRequest request,
        ICardMoveService cardMoveService);

    /// <summary>
    /// Completes a judgement: moves the judgement card from JudgementZone to the discard pile.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="judgeOwner">The player who owns this judgement.</param>
    /// <param name="judgementCard">The judgement card to move.</param>
    /// <param name="cardMoveService">The card move service for moving cards.</param>
    void CompleteJudgement(
        Game game,
        Player judgeOwner,
        Card judgementCard,
        ICardMoveService cardMoveService);
}

/// <summary>
/// Interface for judgement modification windows (reserved for future implementation).
/// This interface defines the contract for skills that can modify judgement cards.
/// </summary>
public interface IJudgementModificationWindow
{
    /// <summary>
    /// Checks whether any player can modify the judgement.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="request">The judgement request.</param>
    /// <param name="candidates">List of candidate players who might be able to modify.</param>
    /// <returns>True if modification is possible, false otherwise.</returns>
    bool CanModify(Game game, JudgementRequest request, IReadOnlyList<Player> candidates);

    /// <summary>
    /// Executes the modification window (not implemented in this phase, interface reserved for future use).
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="request">The judgement request.</param>
    /// <param name="originalCard">The original judgement card.</param>
    /// <param name="candidates">List of candidate players who might be able to modify.</param>
    /// <param name="getPlayerChoice">Function to get player choice for a given choice request.</param>
    /// <returns>Modification result if modification occurred, null otherwise.</returns>
    JudgementModificationResult? ExecuteModification(
        Game game,
        JudgementRequest request,
        Card originalCard,
        IReadOnlyList<Player> candidates,
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice);
}

/// <summary>
/// Result of a judgement modification (reserved for future implementation).
/// </summary>
public sealed record JudgementModificationResult(
    Card OriginalCard,
    Card ModifiedCard,
    int ModifierSeat,
    string ModifierSource
);
