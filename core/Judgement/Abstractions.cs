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

/// <summary>
/// Context for a judgement that can be modified.
/// Contains information about the judgement and tracks modifications.
/// </summary>
public sealed class JudgementContext
{
    /// <summary>
    /// The game instance.
    /// </summary>
    public Game Game { get; init; }

    /// <summary>
    /// The player who owns this judgement.
    /// </summary>
    public Player JudgeTarget { get; init; }

    /// <summary>
    /// The original judgement card (first card drawn).
    /// </summary>
    public Card OriginalJudgementCard { get; init; }

    /// <summary>
    /// The current effective judgement card (may be modified).
    /// </summary>
    public Card CurrentJudgementCard { get; set; }

    /// <summary>
    /// The judgement request.
    /// </summary>
    public JudgementRequest Request { get; init; }

    /// <summary>
    /// List of modifications applied to this judgement.
    /// </summary>
    public List<JudgementModificationRecord> Modifications { get; init; } = new();

    /// <summary>
    /// Timestamp when the judgement started.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Creates a new JudgementContext.
    /// </summary>
    public JudgementContext(
        Game game,
        Player judgeTarget,
        Card originalJudgementCard,
        JudgementRequest request)
    {
        Game = game ?? throw new ArgumentNullException(nameof(game));
        JudgeTarget = judgeTarget ?? throw new ArgumentNullException(nameof(judgeTarget));
        OriginalJudgementCard = originalJudgementCard ?? throw new ArgumentNullException(nameof(originalJudgementCard));
        Request = request ?? throw new ArgumentNullException(nameof(request));
        CurrentJudgementCard = originalJudgementCard;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Decision made by a player to modify a judgement.
/// </summary>
public sealed record JudgementModifyDecision(
    int ModifierSeat,
    string ModifierSource,
    Card ReplacementCard
);

/// <summary>
/// Interface for skills that can modify judgement cards.
/// Provides a way for skills to participate in the judgement modification window.
/// </summary>
public interface IJudgementModifier
{
    /// <summary>
    /// Checks whether this modifier can modify the given judgement.
    /// </summary>
    /// <param name="ctx">The judgement context.</param>
    /// <param name="self">The player who owns this modifier.</param>
    /// <returns>True if this modifier can modify the judgement, false otherwise.</returns>
    bool CanModify(JudgementContext ctx, Player self);

    /// <summary>
    /// Gets the decision to modify the judgement.
    /// This method should ask the player to choose a card to replace the judgement card.
    /// </summary>
    /// <param name="ctx">The judgement context.</param>
    /// <param name="self">The player who owns this modifier.</param>
    /// <param name="getPlayerChoice">Function to get player choice for a given choice request.</param>
    /// <returns>The modification decision, or null if the player chooses not to modify.</returns>
    JudgementModifyDecision? GetDecision(JudgementContext ctx, Player self, Func<ChoiceRequest, ChoiceResult>? getPlayerChoice);
}