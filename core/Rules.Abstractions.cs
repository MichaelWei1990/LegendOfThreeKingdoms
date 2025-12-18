using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Rules;

/// <summary>
/// High-level entry point for rule evaluation and queries.
/// This interface is intentionally read-only over <see cref="Game"/> state.
/// </summary>
public interface IRuleService
{
    RuleResult CanUseCard(CardUsageContext context);

    RuleQueryResult<Player> GetLegalTargetsForUse(CardUsageContext context);

    RuleResult CanRespondWithCard(ResponseContext context);

    RuleQueryResult<Card> GetUsableCards(RuleContext context);

    RuleQueryResult<ActionDescriptor> GetAvailableActions(RuleContext context);

    /// <summary>
    /// Final guard before an action is passed to a resolver for execution.
    /// Can be used to re-check legality or apply last-minute skill modifiers.
    /// </summary>
    RuleResult ValidateActionBeforeResolve(RuleContext context, ActionDescriptor action, ChoiceRequest? choice);
}

/// <summary>
/// Service that computes the list of high-level actions a player can currently perform.
/// </summary>
public interface IActionQueryService
{
    RuleQueryResult<ActionDescriptor> GetAvailableActions(RuleContext context);
}

/// <summary>
/// Phase-related rules (which phases allow which basic actions).
/// </summary>
public interface IPhaseRuleService
{
    bool IsCardUsagePhase(Game game, Player player);
}

/// <summary>
/// Rules around using a card (timing, count limits, target legality).
/// </summary>
public interface ICardUsageRuleService
{
    RuleResult CanUseCard(CardUsageContext context);

    RuleQueryResult<Player> GetLegalTargets(CardUsageContext context);
}

/// <summary>
/// Rules for response windows (who may respond, with which cards).
/// </summary>
public interface IResponseRuleService
{
    RuleResult CanRespondWithCard(ResponseContext context);

    RuleQueryResult<Card> GetLegalResponseCards(ResponseContext context);
}

/// <summary>
/// Seat / attack range rules.
/// </summary>
public interface IRangeRuleService
{
    int GetSeatDistance(Game game, Player from, Player to);

    int GetAttackDistance(Game game, Player from, Player to);

    bool IsWithinAttackRange(Game game, Player from, Player to);
}

/// <summary>
/// Per-turn / per-phase usage limits such as "Slash once per turn".
/// </summary>
public interface ILimitRuleService
{
    int GetMaxSlashPerTurn(Game game, Player player);
}

/// <summary>
/// Shared base context for rule evaluations.
/// </summary>
public record RuleContext(Game Game, Player CurrentPlayer);

/// <summary>
/// Context for card usage checks.
/// </summary>
public sealed record CardUsageContext(
    Game Game,
    Player SourcePlayer,
    Card Card,
    IReadOnlyList<Player> CandidateTargets,
    bool IsExtraAction,
    int UsageCountThisTurn
) : RuleContext(Game, SourcePlayer);

/// <summary>
/// Known response types for basic rules.
/// </summary>
public enum ResponseType
{
    Unknown = 0,
    /// <summary>
    /// Playing a Dodge/Jink against a Slash.
    /// </summary>
    JinkAgainstSlash,
    /// <summary>
    /// Playing a Peach in a dying window.
    /// </summary>
    PeachForDying
}

/// <summary>
/// Context for response rule checks.
/// </summary>
public sealed record ResponseContext(
    Game Game,
    Player Responder,
    ResponseType ResponseType,
    object? SourceEvent
) : RuleContext(Game, Responder);

/// <summary>
/// Standardised rule evaluation result with error information.
/// </summary>
public sealed record RuleResult(
    bool IsAllowed,
    RuleErrorCode ErrorCode = RuleErrorCode.None,
    string? MessageKey = null,
    object? Details = null
)
{
    public static readonly RuleResult Allowed = new(true);

    public static RuleResult Disallowed(RuleErrorCode code, string? messageKey = null, object? details = null) =>
        new(false, code, messageKey, details);
}

/// <summary>
/// Error codes for rule failures.
/// </summary>
public enum RuleErrorCode
{
    None = 0,
    PhaseNotAllowed,
    PlayerNotActive,
    CardNotOwned,
    CardTypeNotAllowed,
    UsageLimitReached,
    TargetRequired,
    TargetOutOfRange,
    TargetNotAlive,
    ResponseNotAllowed,
    NoLegalOptions
}

/// <summary>
/// Result of a rule query that returns a collection of items.
/// </summary>
public sealed record RuleQueryResult<T>(
    IReadOnlyList<T> Items,
    RuleErrorCode ErrorCode = RuleErrorCode.None,
    string? MessageKey = null,
    object? Details = null
)
{
    public bool HasAny => Items.Count > 0;

    public static RuleQueryResult<T> FromItems(IReadOnlyList<T> items) =>
        new(items);

    public static RuleQueryResult<T> Empty(RuleErrorCode code, string? messageKey = null, object? details = null) =>
        new(Array.Empty<T>(), code, messageKey, details);
}

/// <summary>
/// DTO that describes an action available to a player.
/// </summary>
public sealed record ActionDescriptor(
    string ActionId,
    string? DisplayKey,
    bool RequiresTargets,
    TargetConstraints TargetConstraints,
    IReadOnlyList<Card>? CardCandidates = null
);

/// <summary>
/// Target selection constraints for an action.
/// </summary>
public sealed record TargetConstraints(
    int MinTargets,
    int MaxTargets,
    TargetFilterType FilterType = TargetFilterType.Any
);

/// <summary>
/// High level filter for allowed targets.
/// Concrete filtering is still done in rule services.
/// </summary>
public enum TargetFilterType
{
    Any = 0,
    Enemies,
    Friends,
    SelfOrFriends
}

/// <summary>
/// High-level categories of choices that the engine can request from a player.
/// </summary>
public enum ChoiceType
{
    SelectTargets = 0,
    SelectCards = 1,
    Confirm = 2,
    SelectOption = 3
}

/// <summary>
/// Base DTO that describes a choice the player has to make.
/// Engine/network/UI layers are responsible for presenting and resolving the choice.
/// </summary>
public sealed record ChoiceRequest(
    string RequestId,
    int PlayerSeat,
    ChoiceType ChoiceType,
    TargetConstraints? TargetConstraints,
    IReadOnlyList<Card>? AllowedCards
);

/// <summary>
/// Allows skills or mode-specific logic to modify rule decisions without changing core services.
/// Implementations are expected to be side-effect free and only adjust the returned result.
/// </summary>
public interface IRuleModifier
{
    RuleResult ModifyCanUseCard(RuleResult current, CardUsageContext context);

    RuleResult ModifyCanRespondWithCard(RuleResult current, ResponseContext context);

    RuleResult ModifyValidateAction(RuleResult current, RuleContext context, ActionDescriptor action, ChoiceRequest? choice);
}

/// <summary>
/// Provides the set of rule modifiers that should apply for a given game and player.
/// Typical implementations will inspect game mode, hero, equipments and skills.
/// </summary>
public interface IRuleModifierProvider
{
    IReadOnlyList<IRuleModifier> GetModifiersFor(Game game, Player player);
}


