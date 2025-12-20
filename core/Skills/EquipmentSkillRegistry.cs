using System;
using System.Collections.Generic;

namespace LegendOfThreeKingdoms.Core.Skills;

/// <summary>
/// Factory interface for creating equipment skill instances.
/// Similar to ISkillFactory but specifically for equipment-provided skills.
/// </summary>
public interface IEquipmentSkillFactory
{
    /// <summary>
    /// Creates a new instance of the equipment skill.
    /// </summary>
    /// <returns>A new skill instance.</returns>
    ISkill CreateSkill();
}

/// <summary>
/// Central registry for equipment skills.
/// Maps equipment definition IDs to skill factories.
/// </summary>
public sealed class EquipmentSkillRegistry
{
    private readonly Dictionary<string, IEquipmentSkillFactory> _equipmentSkillFactories = new();

    /// <summary>
    /// Registers an equipment skill factory for a given equipment definition ID.
    /// </summary>
    /// <param name="equipmentDefinitionId">The definition ID of the equipment card.</param>
    /// <param name="factory">Factory that creates instances of the equipment skill.</param>
    /// <exception cref="ArgumentNullException">Thrown if equipmentDefinitionId or factory is null.</exception>
    /// <exception cref="ArgumentException">Thrown if equipmentDefinitionId is already registered.</exception>
    public void RegisterEquipmentSkill(string equipmentDefinitionId, IEquipmentSkillFactory factory)
    {
        if (string.IsNullOrWhiteSpace(equipmentDefinitionId))
            throw new ArgumentException("Equipment definition ID cannot be null or empty.", nameof(equipmentDefinitionId));
        if (factory is null)
            throw new ArgumentNullException(nameof(factory));

        if (_equipmentSkillFactories.ContainsKey(equipmentDefinitionId))
            throw new ArgumentException($"Equipment skill for '{equipmentDefinitionId}' is already registered.", nameof(equipmentDefinitionId));

        _equipmentSkillFactories[equipmentDefinitionId] = factory;
    }

    /// <summary>
    /// Gets a skill instance for a given equipment definition ID.
    /// </summary>
    /// <param name="equipmentDefinitionId">The equipment definition ID to look up.</param>
    /// <returns>A new skill instance, or null if no skill is registered for this equipment.</returns>
    public ISkill? GetSkillForEquipment(string equipmentDefinitionId)
    {
        if (string.IsNullOrWhiteSpace(equipmentDefinitionId))
            return null;

        if (!_equipmentSkillFactories.TryGetValue(equipmentDefinitionId, out var factory))
            return null;

        return factory.CreateSkill();
    }

    /// <summary>
    /// Checks whether an equipment definition ID has a registered skill.
    /// </summary>
    /// <param name="equipmentDefinitionId">The equipment definition ID to check.</param>
    /// <returns>True if a skill is registered for this equipment, false otherwise.</returns>
    public bool HasEquipmentSkill(string equipmentDefinitionId)
    {
        return !string.IsNullOrWhiteSpace(equipmentDefinitionId) && _equipmentSkillFactories.ContainsKey(equipmentDefinitionId);
    }

    /// <summary>
    /// Clears all registered equipment skills.
    /// Primarily used for testing.
    /// </summary>
    public void Clear()
    {
        _equipmentSkillFactories.Clear();
    }
}
