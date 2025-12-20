using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;

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
