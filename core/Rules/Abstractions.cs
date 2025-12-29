using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Rules;

/// <summary>
/// Target selection type for cards.
/// </summary>
internal enum TargetSelectionType
{
    /// <summary>
    /// No target selection required.
    /// </summary>
    None,

    /// <summary>
    /// Single other player within attack range.
    /// </summary>
    SingleOtherWithRange,

    /// <summary>
    /// Single other player within distance 1.
    /// </summary>
    SingleOtherWithDistance1,

    /// <summary>
    /// Single other player with no distance restriction.
    /// </summary>
    SingleOtherNoDistance,

    /// <summary>
    /// All other players (automatic selection, no player choice needed).
    /// </summary>
    AllOther,

    /// <summary>
    /// Self-targeting.
    /// </summary>
    Self,

    /// <summary>
    /// Peach targets: injured self or any character in dying state (CurrentHealth <= 0).
    /// </summary>
    PeachTargets
}

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
    PeachForDying,
    /// <summary>
    /// Playing a Jink/Dodge against Wanjian Qifa (万箭齐发).
    /// </summary>
    JinkAgainstWanjianqifa,
    /// <summary>
    /// Playing a Slash against Nanman Rushin (南蛮入侵).
    /// </summary>
    SlashAgainstNanmanRushin,
    /// <summary>
    /// Playing a Slash in a Duel (决斗).
    /// </summary>
    SlashAgainstDuel
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
    IReadOnlyList<Card>? AllowedCards,
    string? ResponseWindowId = null,
    bool CanPass = true
);

/// <summary>
/// Result returned by a player in response to a <see cref="ChoiceRequest"/>.
/// This DTO is intentionally lightweight and uses seats/ids instead of object references
/// so that it can be serialized over the network or stored for replay.
/// </summary>
public sealed record ChoiceResult(
    string RequestId,
    int PlayerSeat,
    IReadOnlyList<int>? SelectedTargetSeats,
    IReadOnlyList<int>? SelectedCardIds,
    string? SelectedOptionId,
    bool? Confirmed
);

/// <summary>
/// Factory responsible for creating <see cref="ChoiceRequest"/> instances
/// from high-level actions, response windows or skill activations.
/// Implementations must be side-effect free.
/// </summary>
public interface IChoiceRequestFactory
{
    /// <summary>
    /// Creates a choice request for a high-level action that requires additional input,
    /// such as selecting targets for a Slash action.
    /// </summary>
    ChoiceRequest CreateForAction(RuleContext context, ActionDescriptor action);

    /// <summary>
    /// Creates a choice request for a response window, e.g. whether to play Jink against Slash.
    /// </summary>
    ChoiceRequest CreateForResponse(ResponseContext context);
}

/// <summary>
/// Validates that an action and its associated player choices are still legal
/// just before they are passed to a resolver for execution.
/// This is a dedicated hook for engine / resolver layers; it does not mutate game state.
/// </summary>
public interface IActionExecutionValidator
{
    RuleResult Validate(RuleContext context, ActionDescriptor action, ChoiceRequest? originalRequest, ChoiceResult? playerChoice);
}

/// <summary>
/// Maps high-level actions and player choices to concrete resolver invocations.
/// Implementations are expected to contain only orchestration logic; actual game
/// state changes should be delegated to resolver components.
/// </summary>
public interface IActionResolutionMapper
{
    void Resolve(RuleContext context, ActionDescriptor action, ChoiceRequest? originalRequest, ChoiceResult? playerChoice);
}

/// <summary>
/// Allows skills or mode-specific logic to modify rule decisions without changing core services.
/// Implementations are expected to be side-effect free and only adjust the returned result.
/// </summary>
public interface IRuleModifier
{
    RuleResult ModifyCanUseCard(RuleResult current, CardUsageContext context);

    RuleResult ModifyCanRespondWithCard(RuleResult current, ResponseContext context);

    RuleResult ModifyValidateAction(RuleResult current, RuleContext context, ActionDescriptor action, ChoiceRequest? choice);

    /// <summary>
    /// Modifies the maximum number of Slash cards a player can use per turn.
    /// Returns null if no modification is needed, otherwise returns the modified value.
    /// </summary>
    /// <param name="current">The current maximum slash count.</param>
    /// <param name="game">The current game state.</param>
    /// <param name="player">The player whose limit is being checked.</param>
    /// <returns>Null if no modification, otherwise the modified value.</returns>
    int? ModifyMaxSlashPerTurn(int current, Game game, Player player);

    /// <summary>
    /// Modifies the attack distance from one player to another.
    /// Returns null if no modification is needed, otherwise returns the modified value.
    /// </summary>
    /// <param name="current">The current attack distance.</param>
    /// <param name="game">The current game state.</param>
    /// <param name="from">The attacking player.</param>
    /// <param name="to">The target player.</param>
    /// <returns>Null if no modification, otherwise the modified value.</returns>
    int? ModifyAttackDistance(int current, Game game, Player from, Player to);

    /// <summary>
    /// Modifies the seat distance from one player to another.
    /// This is used for defensive equipment like defensive horse (+1 defense distance).
    /// Returns null if no modification is needed, otherwise returns the modified value.
    /// </summary>
    /// <param name="current">The current seat distance.</param>
    /// <param name="game">The current game state.</param>
    /// <param name="from">The source player (e.g., attacker).</param>
    /// <param name="to">The target player (e.g., defender).</param>
    /// <returns>Null if no modification, otherwise the modified value.</returns>
    int? ModifySeatDistance(int current, Game game, Player from, Player to);

    /// <summary>
    /// Modifies the number of cards drawn during draw phase.
    /// Returns null if no modification is needed, otherwise returns the modified value.
    /// </summary>
    /// <param name="current">The current draw count.</param>
    /// <param name="game">The current game state.</param>
    /// <param name="player">The player whose draw count is being checked.</param>
    /// <returns>Null if no modification, otherwise the modified value.</returns>
    int? ModifyDrawCount(int current, Game game, Player player);
}

/// <summary>
/// Provides the set of rule modifiers that should apply for a given game and player.
/// Typical implementations will inspect game mode, hero, equipments and skills.
/// </summary>
public interface IRuleModifierProvider
{
    IReadOnlyList<IRuleModifier> GetModifiersFor(Game game, Player player);
}


