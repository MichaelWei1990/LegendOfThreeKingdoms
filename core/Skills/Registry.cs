using System;
using System.Collections.Generic;
using System.Linq;

namespace LegendOfThreeKingdoms.Core.Skills;

/// <summary>
/// Central registry for skills and hero-skill mappings.
/// Supports registering skill factories and querying skills by ID or hero ID.
/// </summary>
public sealed class SkillRegistry
{
    private readonly Dictionary<string, ISkillFactory> _skillFactories = new();
    private readonly Dictionary<string, List<string>> _heroSkillMap = new();

    /// <summary>
    /// Registers a skill factory with the given skill ID.
    /// </summary>
    /// <param name="skillId">Unique identifier for the skill.</param>
    /// <param name="factory">Factory that creates instances of the skill.</param>
    /// <exception cref="ArgumentNullException">Thrown if skillId or factory is null.</exception>
    /// <exception cref="ArgumentException">Thrown if skillId is already registered.</exception>
    public void RegisterSkill(string skillId, ISkillFactory factory)
    {
        if (string.IsNullOrWhiteSpace(skillId))
            throw new ArgumentException("Skill ID cannot be null or empty.", nameof(skillId));
        if (factory is null)
            throw new ArgumentNullException(nameof(factory));

        if (_skillFactories.ContainsKey(skillId))
            throw new ArgumentException($"Skill with ID '{skillId}' is already registered.", nameof(skillId));

        _skillFactories[skillId] = factory;
    }

    /// <summary>
    /// Registers a mapping between a hero ID and a list of skill IDs.
    /// </summary>
    /// <param name="heroId">Unique identifier for the hero.</param>
    /// <param name="skillIds">List of skill IDs that belong to this hero.</param>
    /// <exception cref="ArgumentNullException">Thrown if heroId or skillIds is null.</exception>
    public void RegisterHeroSkills(string heroId, IEnumerable<string> skillIds)
    {
        if (string.IsNullOrWhiteSpace(heroId))
            throw new ArgumentException("Hero ID cannot be null or empty.", nameof(heroId));
        if (skillIds is null)
            throw new ArgumentNullException(nameof(skillIds));

        var skillList = skillIds.ToList();
        if (_heroSkillMap.ContainsKey(heroId))
        {
            // Append to existing list
            _heroSkillMap[heroId].AddRange(skillList);
        }
        else
        {
            _heroSkillMap[heroId] = new List<string>(skillList);
        }
    }

    /// <summary>
    /// Gets a skill instance by its ID.
    /// </summary>
    /// <param name="skillId">The skill ID to look up.</param>
    /// <returns>A new skill instance, or null if the skill is not registered.</returns>
    public ISkill? GetSkill(string skillId)
    {
        if (string.IsNullOrWhiteSpace(skillId))
            return null;

        if (!_skillFactories.TryGetValue(skillId, out var factory))
            return null;

        return factory.CreateSkill();
    }

    /// <summary>
    /// Gets all skill instances for a given hero ID.
    /// </summary>
    /// <param name="heroId">The hero ID to look up.</param>
    /// <returns>An enumerable of skill instances for the hero, or empty if the hero is not registered or has no skills.</returns>
    public IEnumerable<ISkill> GetSkillsForHero(string heroId)
    {
        if (string.IsNullOrWhiteSpace(heroId))
            return Enumerable.Empty<ISkill>();

        if (!_heroSkillMap.TryGetValue(heroId, out var skillIds))
            return Enumerable.Empty<ISkill>();

        var skills = new List<ISkill>();
        foreach (var skillId in skillIds)
        {
            var skill = GetSkill(skillId);
            if (skill is not null)
            {
                skills.Add(skill);
            }
        }

        return skills;
    }

    /// <summary>
    /// Checks whether a skill ID is registered.
    /// </summary>
    /// <param name="skillId">The skill ID to check.</param>
    /// <returns>True if the skill is registered, false otherwise.</returns>
    public bool IsSkillRegistered(string skillId)
    {
        return !string.IsNullOrWhiteSpace(skillId) && _skillFactories.ContainsKey(skillId);
    }

    /// <summary>
    /// Checks whether a hero ID has any registered skills.
    /// </summary>
    /// <param name="heroId">The hero ID to check.</param>
    /// <returns>True if the hero has registered skills, false otherwise.</returns>
    public bool HasHeroSkills(string heroId)
    {
        return !string.IsNullOrWhiteSpace(heroId) && _heroSkillMap.ContainsKey(heroId);
    }

    /// <summary>
    /// Clears all registered skills and hero mappings.
    /// Primarily used for testing.
    /// </summary>
    public void Clear()
    {
        _skillFactories.Clear();
        _heroSkillMap.Clear();
    }
}
