using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Skills;

/// <summary>
/// Manages skills for players in a game.
/// Handles loading skills from registry, attaching/detaching skills, and lifecycle management.
/// </summary>
public sealed class SkillManager
{
    private readonly SkillRegistry _registry;
    private readonly IEventBus _eventBus;
    private readonly Dictionary<int, List<ISkill>> _playerSkills = new();

    /// <summary>
    /// Creates a new SkillManager.
    /// </summary>
    /// <param name="registry">The skill registry to use for looking up skills.</param>
    /// <param name="eventBus">The event bus to use when attaching/detaching skills.</param>
    /// <exception cref="ArgumentNullException">Thrown if registry or eventBus is null.</exception>
    public SkillManager(SkillRegistry registry, IEventBus eventBus)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    /// <summary>
    /// Loads and attaches skills for a player based on their HeroId.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="player">The player to load skills for.</param>
    /// <exception cref="ArgumentNullException">Thrown if game or player is null.</exception>
    public void LoadSkillsForPlayer(Game game, Player player)
    {
        if (game is null)
            throw new ArgumentNullException(nameof(game));
        if (player is null)
            throw new ArgumentNullException(nameof(player));

        // Remove any existing skills for this player first
        if (_playerSkills.ContainsKey(player.Seat))
        {
            DetachSkillsForPlayer(game, player);
        }

        // If player has no HeroId, they have no skills
        if (string.IsNullOrWhiteSpace(player.HeroId))
        {
            _playerSkills[player.Seat] = new List<ISkill>();
            return;
        }

        // Load skills from registry
        var skills = _registry.GetSkillsForHero(player.HeroId).ToList();
        var skillList = new List<ISkill>();

        // Check if player is Lord
        var isLord = player.Flags.TryGetValue("IsLord", out var isLordValue) && 
                     isLordValue is bool isLordFlag && isLordFlag;

        // Filter and attach skills
        foreach (var skill in skills)
        {
            // If skill is a Lord skill, only register if player is Lord
            if (skill is ILordSkill)
            {
                if (!isLord)
                {
                    // Skip this lord skill - player is not Lord
                    continue;
                }
            }

            // Attach the skill to the player
            skill.Attach(game, player, _eventBus);
            skillList.Add(skill);
        }

        _playerSkills[player.Seat] = skillList;
    }

    /// <summary>
    /// Loads and attaches skills for all players in the game.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <exception cref="ArgumentNullException">Thrown if game is null.</exception>
    public void LoadSkillsForAllPlayers(Game game)
    {
        if (game is null)
            throw new ArgumentNullException(nameof(game));

        foreach (var player in game.Players)
        {
            LoadSkillsForPlayer(game, player);
        }
    }

    /// <summary>
    /// Gets all active skills for a player.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="player">The player to get skills for.</param>
    /// <returns>An enumerable of active skills for the player.</returns>
    /// <exception cref="ArgumentNullException">Thrown if game or player is null.</exception>
    public IEnumerable<ISkill> GetActiveSkills(Game game, Player player)
    {
        if (game is null)
            throw new ArgumentNullException(nameof(game));
        if (player is null)
            throw new ArgumentNullException(nameof(player));

        if (!_playerSkills.TryGetValue(player.Seat, out var skills))
            return Enumerable.Empty<ISkill>();

        return skills.Where(skill => skill.IsActive(game, player));
    }

    /// <summary>
    /// Gets all skills (active or inactive) for a player.
    /// </summary>
    /// <param name="player">The player to get skills for.</param>
    /// <returns>An enumerable of all skills for the player.</returns>
    /// <exception cref="ArgumentNullException">Thrown if player is null.</exception>
    public IEnumerable<ISkill> GetAllSkills(Player player)
    {
        if (player is null)
            throw new ArgumentNullException(nameof(player));

        if (!_playerSkills.TryGetValue(player.Seat, out var skills))
            return Enumerable.Empty<ISkill>();

        return skills;
    }

    /// <summary>
    /// Detaches all skills for a player.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="player">The player to detach skills from.</param>
    private void DetachSkillsForPlayer(Game game, Player player)
    {
        if (!_playerSkills.TryGetValue(player.Seat, out var skills))
            return;

        foreach (var skill in skills)
        {
            skill.Detach(game, player, _eventBus);
        }

        _playerSkills.Remove(player.Seat);
    }

    /// <summary>
    /// Removes all skills for a player and cleans up resources.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="player">The player to remove skills from.</param>
    public void RemoveSkillsForPlayer(Game game, Player player)
    {
        if (game is null)
            throw new ArgumentNullException(nameof(game));
        if (player is null)
            throw new ArgumentNullException(nameof(player));

        DetachSkillsForPlayer(game, player);
    }

    /// <summary>
    /// Clears all skills for all players.
    /// Useful for cleanup or testing.
    /// </summary>
    /// <param name="game">The current game state.</param>
    public void ClearAllSkills(Game game)
    {
        if (game is null)
            throw new ArgumentNullException(nameof(game));

        foreach (var player in game.Players)
        {
            DetachSkillsForPlayer(game, player);
        }
    }

    /// <summary>
    /// Adds an equipment skill to a player.
    /// This is used when equipment is equipped.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="player">The player to add the skill to.</param>
    /// <param name="skill">The equipment skill to add.</param>
    /// <exception cref="ArgumentNullException">Thrown if game, player, or skill is null.</exception>
    public void AddEquipmentSkill(Game game, Player player, ISkill skill)
    {
        if (game is null)
            throw new ArgumentNullException(nameof(game));
        if (player is null)
            throw new ArgumentNullException(nameof(player));
        if (skill is null)
            throw new ArgumentNullException(nameof(skill));

        if (!_playerSkills.TryGetValue(player.Seat, out var skills))
        {
            skills = new List<ISkill>();
            _playerSkills[player.Seat] = skills;
        }

        skill.Attach(game, player, _eventBus);
        skills.Add(skill);
    }

    /// <summary>
    /// Removes an equipment skill from a player.
    /// This is used when equipment is unequipped.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="player">The player to remove the skill from.</param>
    /// <param name="skillId">The ID of the skill to remove.</param>
    /// <exception cref="ArgumentNullException">Thrown if game or player is null.</exception>
    public void RemoveEquipmentSkill(Game game, Player player, string skillId)
    {
        if (game is null)
            throw new ArgumentNullException(nameof(game));
        if (player is null)
            throw new ArgumentNullException(nameof(player));
        if (string.IsNullOrWhiteSpace(skillId))
            return;

        if (!_playerSkills.TryGetValue(player.Seat, out var skills))
            return;

        var skillToRemove = skills.FirstOrDefault(s => s.Id == skillId);
        if (skillToRemove is not null)
        {
            skillToRemove.Detach(game, player, _eventBus);
            skills.Remove(skillToRemove);
        }
    }
}
