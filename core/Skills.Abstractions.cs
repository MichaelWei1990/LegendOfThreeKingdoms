using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;

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
