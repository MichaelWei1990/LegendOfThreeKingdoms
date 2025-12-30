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
/// Interface for skills that can convert multiple cards into a single virtual card.
/// Used by skills like Serpent Spear (丈八蛇矛) that allow using two hand cards as one card.
/// </summary>
public interface IMultiCardConversionSkill : ISkill
{
    /// <summary>
    /// Gets the number of cards required for conversion.
    /// </summary>
    int RequiredCardCount { get; }

    /// <summary>
    /// Gets the target card subtype that the converted cards will become.
    /// </summary>
    CardSubType TargetCardSubType { get; }

    /// <summary>
    /// Creates a virtual card from multiple original cards for conversion purposes.
    /// The virtual card represents the card that the original cards are being converted to.
    /// </summary>
    /// <param name="originalCards">The original cards being converted (must match RequiredCardCount).</param>
    /// <param name="game">The current game state.</param>
    /// <param name="owner">The player who owns this skill.</param>
    /// <returns>The virtual card, or null if conversion is not applicable for these cards.</returns>
    Card? CreateVirtualCardFromMultiple(IReadOnlyList<Card> originalCards, Game game, Player owner);

    /// <summary>
    /// Determines the color of the virtual card based on the original cards.
    /// </summary>
    /// <param name="originalCards">The original cards being converted.</param>
    /// <returns>The color of the virtual card.</returns>
    Model.CardColor DetermineVirtualCardColor(IReadOnlyList<Card> originalCards);
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
/// Interface for active skills that are limited to once per phase or once per turn.
/// This is a marker interface that extends IActionProvidingSkill to indicate
/// that the skill has usage restrictions that need to be tracked.
/// </summary>
public interface IPhaseLimitedActionProvidingSkill : IActionProvidingSkill
{
    /// <summary>
    /// Gets the usage limit type for this skill.
    /// </summary>
    SkillUsageLimitType UsageLimitType { get; }

    /// <summary>
    /// Checks whether this skill has already been used in the current limit period.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="owner">The player who owns this skill.</param>
    /// <returns>True if the skill has already been used, false otherwise.</returns>
    bool IsAlreadyUsed(Game game, Player owner);

    /// <summary>
    /// Marks this skill as used for the current limit period.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="owner">The player who owns this skill.</param>
    void MarkAsUsed(Game game, Player owner);
}

/// <summary>
/// Defines the type of usage limit for phase-limited active skills.
/// </summary>
public enum SkillUsageLimitType
{
    /// <summary>
    /// Skill can be used once per play phase.
    /// </summary>
    OncePerPlayPhase,

    /// <summary>
    /// Skill can be used once per turn.
    /// </summary>
    OncePerTurn
}

/// <summary>
/// Interface for skills that respond to before damage events.
/// Used by trigger skills that need to react before damage is applied,
/// such as Ice Sword (寒冰剑) that can prevent damage.
/// </summary>
public interface IBeforeDamageSkill : ISkill
{
    /// <summary>
    /// Handles the before damage event.
    /// This method is called when a BeforeDamageEvent is published.
    /// Skills can modify the event (e.g., set IsPrevented to true) to prevent or modify damage.
    /// </summary>
    /// <param name="evt">The before damage event.</param>
    void OnBeforeDamage(BeforeDamageEvent evt);
}

/// <summary>
/// Interface for skills that can modify damage.
/// Used by skills that need to modify damage values, such as LuoYi (裸衣) that increases damage.
/// </summary>
public interface IDamageModifyingSkill : ISkill
{
    /// <summary>
    /// Modifies the damage amount.
    /// This method is called when damage is being calculated.
    /// </summary>
    /// <param name="damage">The damage descriptor to modify.</param>
    /// <param name="game">The current game state.</param>
    /// <param name="owner">The player who owns this skill.</param>
    /// <returns>The damage modification amount (positive to increase, negative to decrease). Returns 0 if no modification.</returns>
    int ModifyDamage(DamageDescriptor damage, Game game, Player owner);
}

/// <summary>
/// Interface for skills that respond to after slash dodged events.
/// Used by trigger skills that need to react when a Slash is successfully dodged,
/// such as Stone Axe (贯石斧) that can force damage even after a successful Dodge.
/// </summary>
public interface IAfterSlashDodgedSkill : ISkill
{
    /// <summary>
    /// Handles the after slash dodged event.
    /// This method is called when an AfterSlashDodgedEvent is published.
    /// Skills can use this to take actions after a Slash has been successfully dodged.
    /// </summary>
    /// <param name="evt">The after slash dodged event.</param>
    void OnAfterSlashDodged(AfterSlashDodgedEvent evt);
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

/// <summary>
/// Interface for skills that respond to after damage events.
/// Used by trigger skills that need to react after damage and dying are fully resolved,
/// such as Feedback (反馈).
/// </summary>
public interface IAfterDamageSkill : ISkill
{
    /// <summary>
    /// Handles the after damage event.
    /// This method is called when an AfterDamageEvent is published.
    /// </summary>
    /// <param name="evt">The after damage event.</param>
    void OnAfterDamage(AfterDamageEvent evt);
}

/// <summary>
/// Interface for skills that can replace the draw phase behavior.
/// Used by active skills like Tuxi (突袭) that allow the player to choose
/// to replace normal card drawing with an alternative action.
/// </summary>
public interface IDrawPhaseReplacementSkill : ISkill
{
    /// <summary>
    /// Checks whether this skill can replace the draw phase for the owner.
    /// This is called at the start of the draw phase to determine if the skill
    /// should be offered as an option to replace normal drawing.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="owner">The player who owns this skill.</param>
    /// <returns>True if the skill can replace the draw phase, false otherwise.</returns>
    bool CanReplaceDrawPhase(Game game, Player owner);

    /// <summary>
    /// Asks the player whether they want to use this skill to replace the draw phase.
    /// This method should present a choice to the player and return their decision.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="owner">The player who owns this skill.</param>
    /// <param name="getPlayerChoice">Function to get player choice for a given choice request.</param>
    /// <returns>True if the player chooses to replace the draw phase, false otherwise.</returns>
    bool ShouldReplaceDrawPhase(Game game, Player owner, Func<Rules.ChoiceRequest, Rules.ChoiceResult> getPlayerChoice);

    /// <summary>
    /// Executes the replacement logic for the draw phase.
    /// This method is called when the player chooses to replace normal drawing.
    /// It should handle the full flow including player choices and card movements.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="owner">The player who owns this skill.</param>
    /// <param name="getPlayerChoice">Function to get player choice for a given choice request.</param>
    /// <param name="cardMoveService">The card move service for moving cards.</param>
    /// <param name="eventBus">The event bus for publishing events.</param>
    /// <param name="stack">The resolution stack for pushing sub-resolvers if needed.</param>
    /// <param name="context">The resolution context.</param>
    void ExecuteDrawPhaseReplacement(
        Game game,
        Player owner,
        Func<Rules.ChoiceRequest, Rules.ChoiceResult> getPlayerChoice,
        Zones.ICardMoveService cardMoveService,
        Events.IEventBus? eventBus,
        Resolution.IResolutionStack stack,
        Resolution.ResolutionContext context);
}

/// <summary>
/// Interface for skills that can actively lose HP (not from damage).
/// Used by active skills like Kurou (苦肉) that allow the player to lose HP as a cost
/// to gain other benefits. This is distinct from damage and should not trigger damage-related skills.
/// </summary>
public interface IActiveHpLossSkill : ISkill
{
    /// <summary>
    /// Gets the amount of HP that will be lost when this skill is activated.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="owner">The player who owns this skill.</param>
    /// <returns>The amount of HP to lose (must be positive).</returns>
    int GetHpLossAmount(Game game, Player owner);

    /// <summary>
    /// Handles the after HP loss event.
    /// This method is called when an AfterHpLostEvent is published after the HP loss is resolved.
    /// Skills can use this to perform actions after losing HP (e.g., draw cards).
    /// </summary>
    /// <param name="evt">The after HP lost event.</param>
    void OnAfterHpLost(AfterHpLostEvent evt);
}

/// <summary>
/// Interface for skills that can modify the draw count during draw phase.
/// Unlike IDrawPhaseReplacementSkill which replaces the entire draw phase,
/// this interface allows skills to modify the draw count (e.g., reduce by 1)
/// while still performing the normal draw action.
/// Used by skills like LuoYi (裸衣) that reduce draw count in exchange for other benefits.
/// </summary>
public interface IDrawPhaseModifyingSkill : ISkill
{
    /// <summary>
    /// Checks whether this skill can modify the draw phase for the owner.
    /// This is called at the start of the draw phase to determine if the skill
    /// should be offered as an option to modify drawing.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="owner">The player who owns this skill.</param>
    /// <returns>True if the skill can modify the draw phase, false otherwise.</returns>
    bool CanModifyDrawPhase(Game game, Player owner);

    /// <summary>
    /// Asks the player whether they want to use this skill to modify the draw phase.
    /// This method should present a choice to the player and return their decision.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="owner">The player who owns this skill.</param>
    /// <param name="getPlayerChoice">Function to get player choice for a given choice request.</param>
    /// <returns>True if the player chooses to modify the draw phase, false otherwise.</returns>
    bool ShouldModifyDrawPhase(Game game, Player owner, Func<Rules.ChoiceRequest, Rules.ChoiceResult> getPlayerChoice);

    /// <summary>
    /// Gets the draw count modification to apply.
    /// This method is called after the player chooses to modify the draw phase.
    /// The returned value will be added to the base draw count (can be negative).
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="owner">The player who owns this skill.</param>
    /// <returns>The draw count modification (e.g., -1 to reduce by 1, +1 to increase by 1).</returns>
    int GetDrawCountModification(Game game, Player owner);

    /// <summary>
    /// Called when the draw phase modification is activated.
    /// This method can be used to set up turn-based effects (e.g., damage boost until end of turn).
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="owner">The player who owns this skill.</param>
    /// <param name="eventBus">The event bus for publishing events or subscribing to events.</param>
    void OnDrawPhaseModified(Game game, Player owner, Events.IEventBus? eventBus);
}

/// <summary>
/// Interface for skills that can provide response assistance.
/// Used by skills like Hujia (护驾) that allow other players to assist the owner
/// by playing response cards on their behalf.
/// </summary>
public interface IResponseAssistanceSkill : ISkill
{
    /// <summary>
    /// Checks whether this skill can provide assistance for the given response type.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="owner">The player who owns this skill.</param>
    /// <param name="responseType">The type of response needed.</param>
    /// <param name="sourceEvent">The source event that triggered the response requirement.</param>
    /// <returns>True if the skill can provide assistance, false otherwise.</returns>
    bool CanProvideAssistance(Game game, Player owner, Rules.ResponseType responseType, object? sourceEvent);

    /// <summary>
    /// Gets the list of players who can assist the owner.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="owner">The player who owns this skill.</param>
    /// <returns>List of players who can assist, ordered by seat.</returns>
    IReadOnlyList<Player> GetAssistants(Game game, Player owner);

    /// <summary>
    /// Asks the owner whether they want to activate this assistance skill.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="owner">The player who owns this skill.</param>
    /// <param name="getPlayerChoice">Function to get player choice for a given choice request.</param>
    /// <returns>True if the owner chooses to activate the skill, false otherwise.</returns>
    bool ShouldActivate(Game game, Player owner, Func<Rules.ChoiceRequest, Rules.ChoiceResult> getPlayerChoice);
}

/// <summary>
/// Interface for skills that can modify response requirements (e.g., Wushuang).
/// Skills implementing this interface can change the required number of response units.
/// </summary>
public interface IResponseRequirementModifyingSkill : ISkill
{
    /// <summary>
    /// Modifies the required number of Jink response units for a Slash.
    /// Returns null if no modification is needed, otherwise returns the modified count.
    /// </summary>
    /// <param name="current">The current required count (default: 1).</param>
    /// <param name="game">The current game state.</param>
    /// <param name="slashSource">The player who used the Slash.</param>
    /// <param name="target">The target player who needs to respond.</param>
    /// <param name="slashCard">The Slash card being used.</param>
    /// <returns>Null if no modification, otherwise the modified count.</returns>
    int? ModifyJinkRequirementForSlash(int current, Game game, Player slashSource, Player target, Card slashCard);

    /// <summary>
    /// Modifies the required number of Slash response units for a Duel.
    /// Returns null if no modification is needed, otherwise returns the modified count.
    /// </summary>
    /// <param name="current">The current required count (default: 1).</param>
    /// <param name="game">The current game state.</param>
    /// <param name="playerToRespond">The player who needs to respond.</param>
    /// <param name="opposingPlayer">The opposing player in the duel.</param>
    /// <param name="duelCard">The Duel card being used.</param>
    /// <returns>Null if no modification, otherwise the modified count.</returns>
    int? ModifySlashRequirementForDuel(int current, Game game, Player playerToRespond, Player opposingPlayer, Card? duelCard);
}

/// <summary>
/// Interface for skills that respond to card moved events.
/// Used by trigger skills like Lianying (连营) that need to react when cards are moved,
/// particularly when tracking hand card count changes.
/// </summary>
public interface ICardMovedSkill : ISkill
{
    /// <summary>
    /// Handles the card moved event.
    /// This method is called when a CardMovedEvent is published.
    /// Skills can use this to track card movements and trigger effects based on zone changes.
    /// </summary>
    /// <param name="evt">The card moved event.</param>
    void OnCardMoved(CardMovedEvent evt);
}

/// <summary>
/// Interface for skills that can modify or redirect Slash targets.
/// Used by skills like Liuli (流离) that allow a player to redirect a Slash to another target.
/// </summary>
public interface ISlashTargetModifyingSkill : ISkill
{
    /// <summary>
    /// Checks whether this skill can modify the Slash target.
    /// This is called when a Slash targets the owner, before the response window.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="owner">The player who owns this skill (the current target).</param>
    /// <param name="attacker">The player who used the Slash.</param>
    /// <param name="slashCard">The Slash card being used.</param>
    /// <param name="ruleService">The rule service (contains range rule service internally).</param>
    /// <returns>True if the skill can modify the target, false otherwise.</returns>
    bool CanModifyTarget(
        Game game,
        Player owner,
        Player attacker,
        Model.Card slashCard,
        Rules.IRuleService ruleService);

    /// <summary>
    /// Creates a resolver that handles the target modification.
    /// This resolver will be pushed onto the resolution stack before the response window.
    /// </summary>
    /// <param name="owner">The player who owns this skill (the current target).</param>
    /// <param name="attacker">The player who used the Slash.</param>
    /// <param name="slashCard">The Slash card being used.</param>
    /// <param name="pendingDamage">The pending damage descriptor (can be modified to change target).</param>
    /// <returns>A resolver that handles the target modification, or null if the skill should not be activated.</returns>
    Resolution.IResolver? CreateTargetModificationResolver(
        Player owner,
        Player attacker,
        Model.Card slashCard,
        Resolution.DamageDescriptor pendingDamage);
}

/// <summary>
/// Interface for skills that can modify health recovery amount.
/// Used by skills like Rescue (救援) that can increase recovery amount when certain conditions are met.
/// </summary>
public interface IRecoverAmountModifyingSkill : ISkill
{
    /// <summary>
    /// Handles the before recover event.
    /// This method is called when a BeforeRecoverEvent is published.
    /// Skills can modify the event (e.g., set RecoveryModification) to modify recovery amount.
    /// </summary>
    /// <param name="evt">The before recover event.</param>
    void OnBeforeRecover(BeforeRecoverEvent evt);
}