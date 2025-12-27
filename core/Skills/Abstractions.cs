using System;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Judgement;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Skills;

/// <summary>
/// Base interface for all skills in the game.
/// Skills can be attached to players and respond to game events.
/// </summary>
public interface ISkill
{
    /// <summary>
    /// Unique identifier of this skill.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Display name of this skill (for logging/UI).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Type of this skill (Active, Trigger, or Locked).
    /// </summary>
    SkillType Type { get; }

    /// <summary>
    /// Capabilities this skill provides.
    /// </summary>
    SkillCapability Capabilities { get; }

    /// <summary>
    /// Determines whether this skill is currently active for the owner.
    /// Used for locked skills that may be enabled/disabled under certain conditions.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="owner">The player who owns this skill.</param>
    /// <returns>True if the skill is active, false otherwise.</returns>
    bool IsActive(Game game, Player owner);

    /// <summary>
    /// Attaches this skill to a player and subscribes to relevant events.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="owner">The player who owns this skill.</param>
    /// <param name="eventBus">The event bus to subscribe to.</param>
    void Attach(Game game, Player owner, IEventBus eventBus);

    /// <summary>
    /// Detaches this skill from a player and unsubscribes from events.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="owner">The player who owns this skill.</param>
    /// <param name="eventBus">The event bus to unsubscribe from.</param>
    void Detach(Game game, Player owner, IEventBus eventBus);
}

/// <summary>
/// Factory interface for creating skill instances.
/// Each player should have independent skill instances to avoid state sharing.
/// </summary>
public interface ISkillFactory
{
    /// <summary>
    /// Creates a new instance of the skill.
    /// </summary>
    /// <returns>A new skill instance.</returns>
    ISkill CreateSkill();
}

/// <summary>
/// Interface for skills that can modify rule judgments.
/// Skills implementing this interface can intervene in rule checks such as card usage limits,
/// response opportunities, and action validation.
/// </summary>
public interface IRuleModifyingSkill : ISkill
{
    /// <summary>
    /// Modifies the result of CanUseCard rule check.
    /// Returns null if no modification is needed, otherwise returns the modified result.
    /// </summary>
    /// <param name="current">The current rule result.</param>
    /// <param name="context">The card usage context.</param>
    /// <returns>Null if no modification, otherwise the modified result.</returns>
    RuleResult? ModifyCanUseCard(RuleResult current, CardUsageContext context);

    /// <summary>
    /// Modifies the result of CanRespondWithCard rule check.
    /// Returns null if no modification is needed, otherwise returns the modified result.
    /// </summary>
    /// <param name="current">The current rule result.</param>
    /// <param name="context">The response context.</param>
    /// <returns>Null if no modification, otherwise the modified result.</returns>
    RuleResult? ModifyCanRespondWithCard(RuleResult current, ResponseContext context);

    /// <summary>
    /// Modifies the result of ValidateAction rule check.
    /// Returns null if no modification is needed, otherwise returns the modified result.
    /// </summary>
    /// <param name="current">The current rule result.</param>
    /// <param name="context">The rule context.</param>
    /// <param name="action">The action descriptor.</param>
    /// <param name="choice">The choice request, if any.</param>
    /// <returns>Null if no modification, otherwise the modified result.</returns>
    RuleResult? ModifyValidateAction(RuleResult current, RuleContext context, ActionDescriptor action, ChoiceRequest? choice);

    /// <summary>
    /// Modifies the maximum number of Slash cards a player can use per turn.
    /// Returns null if no modification is needed, otherwise returns the modified value.
    /// </summary>
    /// <param name="current">The current maximum slash count.</param>
    /// <param name="game">The current game state.</param>
    /// <param name="owner">The player who owns this skill.</param>
    /// <returns>Null if no modification, otherwise the modified value.</returns>
    int? ModifyMaxSlashPerTurn(int current, Game game, Player owner);

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
    /// <param name="owner">The player who owns this skill.</param>
    /// <returns>Null if no modification, otherwise the modified value.</returns>
    int? ModifyDrawCount(int current, Game game, Player owner);
}

/// <summary>
/// Interface for skills that modify attack distance.
/// Used by weapons and offensive equipment that increase attack range.
/// </summary>
public interface IAttackDistanceModifyingSkill : ISkill
{
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
}

/// <summary>
/// Interface for skills that modify seat distance.
/// Used by defensive/offensive horses that affect the distance calculation for attacks.
/// </summary>
public interface ISeatDistanceModifyingSkill : ISkill
{
    /// <summary>
    /// Modifies the seat distance from one player to another.
    /// This is used for defensive equipment like defensive horse (+1 defense distance)
    /// or offensive equipment like offensive horse (-1 attack distance requirement).
    /// Returns null if no modification is needed, otherwise returns the modified value.
    /// </summary>
    /// <param name="current">The current seat distance.</param>
    /// <param name="game">The current game state.</param>
    /// <param name="from">The source player (e.g., attacker).</param>
    /// <param name="to">The target player (e.g., defender).</param>
    /// <returns>Null if no modification, otherwise the modified value.</returns>
    int? ModifySeatDistance(int current, Game game, Player from, Player to);
}

/// <summary>
/// Skill that can filter legal targets for card usage.
/// Used by equipment like Renwang Shield to prevent certain cards from targeting the owner.
/// </summary>
public interface ITargetFilteringSkill : ISkill
{
    /// <summary>
    /// Determines whether a target should be excluded from legal targets for a card usage.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="owner">The player who owns this skill.</param>
    /// <param name="card">The card being used.</param>
    /// <param name="potentialTarget">The potential target player.</param>
    /// <returns>True if the target should be excluded, false otherwise.</returns>
    bool ShouldExcludeTarget(Game game, Player owner, Card card, Player potentialTarget);
}

/// <summary>
/// Skill that can filter card effects on targets.
/// Used by equipment like Renwang Shield to invalidate card effects before response windows.
/// </summary>
public interface ICardEffectFilteringSkill : ISkill
{
    /// <summary>
    /// Determines whether a card effect is effective on the target.
    /// Returns false if the effect should be invalidated (vetoed), true otherwise.
    /// </summary>
    /// <param name="context">The card effect context.</param>
    /// <param name="reason">If the effect is vetoed, contains the reason; otherwise null.</param>
    /// <returns>True if the effect is effective, false if it should be vetoed.</returns>
    bool IsEffective(CardEffectContext context, out EffectVetoReason? reason);
}

/// <summary>
/// Interface for skills that can modify Slash response capabilities.
/// Used by skills like Tieqi (铁骑) that can prevent targets from using Dodge to respond to Slash.
/// </summary>
public interface ISlashResponseModifier : ISkill
{
    /// <summary>
    /// Processes the Slash target confirmation and may perform actions (e.g., judgement).
    /// This method is called when a Slash card's targets are confirmed, before the response window opens.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="sourcePlayer">The player who used the Slash.</param>
    /// <param name="slashCard">The Slash card being used.</param>
    /// <param name="targetPlayer">The target player.</param>
    /// <param name="judgementService">The judgement service for executing judgements.</param>
    /// <param name="cardMoveService">The card move service for moving cards.</param>
    /// <param name="eventBus">The event bus for publishing events.</param>
    /// <returns>True if the target cannot use Dodge to respond to this Slash, false otherwise.</returns>
    bool ProcessSlashTargetConfirmed(
        Game game,
        Player sourcePlayer,
        Card slashCard,
        Player targetPlayer,
        IJudgementService judgementService,
        ICardMoveService cardMoveService,
        IEventBus? eventBus);
}

/// <summary>
/// Interface for skills that can provide alternative response capabilities.
/// Used by equipment like Bagua Array that can provide virtual responses through judgement.
/// </summary>
public interface IResponseEnhancementSkill : ISkill
{
    /// <summary>
    /// Gets the priority of this response enhancement skill.
    /// Lower values indicate higher priority (executed first).
    /// Skills with higher priority are checked before skills with lower priority.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Checks whether this skill can provide an alternative response for the given response type.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="owner">The player who owns this skill.</param>
    /// <param name="responseType">The type of response needed.</param>
    /// <param name="sourceEvent">The source event that triggered the response window.</param>
    /// <returns>True if this skill can provide an alternative response, false otherwise.</returns>
    bool CanProvideResponse(Game game, Player owner, ResponseType responseType, object? sourceEvent);

    /// <summary>
    /// Executes the alternative response mechanism (e.g., judgement).
    /// This method should handle the full flow including player choice and judgement execution.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="owner">The player who owns this skill.</param>
    /// <param name="responseType">The type of response needed.</param>
    /// <param name="sourceEvent">The source event that triggered the response window.</param>
    /// <param name="getPlayerChoice">Function to get player choice for a given choice request.</param>
    /// <param name="judgementService">The judgement service for executing judgements.</param>
    /// <param name="cardMoveService">The card move service for moving cards.</param>
    /// <returns>True if the alternative response was successful, false otherwise.</returns>
    bool ExecuteAlternativeResponse(
        Game game,
        Player owner,
        ResponseType responseType,
        object? sourceEvent,
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice,
        Judgement.IJudgementService judgementService,
        Zones.ICardMoveService cardMoveService);
}

/// <summary>
/// Interface for skills that can convert cards to virtual cards.
/// Used by skills like Qixi (奇袭) that allow using one card as another card.
/// </summary>
public interface ICardConversionSkill : ISkill
{
    /// <summary>
    /// Creates a virtual card from the original card for conversion purposes.
    /// The virtual card represents the card that the original card is being converted to.
    /// </summary>
    /// <param name="originalCard">The original card being converted.</param>
    /// <param name="game">The current game state.</param>
    /// <param name="owner">The player who owns this skill.</param>
    /// <returns>The virtual card, or null if conversion is not applicable for this card.</returns>
    Card? CreateVirtualCard(Card originalCard, Game game, Player owner);
}

/// <summary>
/// Interface for skills that can provide actions.
/// Used by active skills that initiate choices and need to generate action descriptors.
/// </summary>
public interface IActionProvidingSkill : ISkill
{
    /// <summary>
    /// Generates an action descriptor for this skill if conditions are met.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="owner">The player who owns this skill.</param>
    /// <returns>The action descriptor if conditions are met, null otherwise.</returns>
    Rules.ActionDescriptor? GenerateAction(Game game, Player owner);
}

/// <summary>
/// Interface for skills that respond to damage resolved events.
/// Used by trigger skills that need to react when damage is resolved.
/// </summary>
public interface IDamageResolvedSkill : ISkill
{
    /// <summary>
    /// Handles the damage resolved event.
    /// This method is called when a DamageResolvedEvent is published.
    /// </summary>
    /// <param name="evt">The damage resolved event.</param>
    void OnDamageResolved(DamageResolvedEvent evt);
}
