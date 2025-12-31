using System.Collections.Generic;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Character;

/// <summary>
/// Definition of a character (hero) in the game.
/// Contains basic information about the character including skills.
/// </summary>
public sealed class CharacterDefinition
{
    /// <summary>
    /// Unique identifier for this character.
    /// </summary>
    public string CharacterId { get; init; } = string.Empty;

    /// <summary>
    /// Display name of this character.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Faction identifier (e.g., "Shu", "Wei", "Wu", "Qun").
    /// </summary>
    public string? FactionId { get; init; }

    /// <summary>
    /// Gender of this character.
    /// </summary>
    public Gender Gender { get; init; } = Gender.Neutral;

    /// <summary>
    /// Maximum health points for this character.
    /// </summary>
    public int MaxHp { get; init; }

    /// <summary>
    /// List of skills that belong to this character.
    /// </summary>
    public IReadOnlyList<SkillDefinitionRef> Skills { get; init; } = new List<SkillDefinitionRef>();
}

/// <summary>
/// Reference to a skill definition for a character.
/// Contains the skill ID and whether it is a Lord skill.
/// </summary>
public sealed class SkillDefinitionRef
{
    /// <summary>
    /// Unique identifier of the skill.
    /// </summary>
    public string SkillId { get; init; } = string.Empty;

    /// <summary>
    /// Whether this skill is a Lord skill (主公技).
    /// Lord skills are only registered if the player is the Lord.
    /// </summary>
    public bool IsLordSkill { get; init; }
}
