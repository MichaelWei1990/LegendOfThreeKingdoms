using System;
using System.Collections.Generic;
using LegendOfThreeKingdoms.Core.Model;

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
/// Maps equipment definition IDs and card subtypes to skill factories.
/// Supports both specific equipment (by DefinitionId) and category-based (by CardSubType) skill registration.
/// </summary>
public sealed class EquipmentSkillRegistry
{
    private readonly Dictionary<string, IEquipmentSkillFactory> _equipmentSkillFactories = new();
    private readonly Dictionary<CardSubType, IEquipmentSkillFactory> _subTypeSkillFactories = new();

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
    /// Registers an equipment skill factory for a given card subtype.
    /// This allows all equipment cards of the same subtype to share the same skill.
    /// For example, all offensive horse cards (chitu, etc.) can share the same skill.
    /// </summary>
    /// <param name="cardSubType">The card subtype to register the skill for.</param>
    /// <param name="factory">Factory that creates instances of the equipment skill.</param>
    /// <exception cref="ArgumentException">Thrown if cardSubType is Unknown or already registered.</exception>
    /// <exception cref="ArgumentNullException">Thrown if factory is null.</exception>
    public void RegisterEquipmentSkillBySubType(CardSubType cardSubType, IEquipmentSkillFactory factory)
    {
        if (cardSubType == CardSubType.Unknown)
            throw new ArgumentException("Cannot register skill for Unknown card subtype.", nameof(cardSubType));
        if (factory is null)
            throw new ArgumentNullException(nameof(factory));

        if (_subTypeSkillFactories.ContainsKey(cardSubType))
            throw new ArgumentException($"Equipment skill for '{cardSubType}' is already registered.", nameof(cardSubType));

        _subTypeSkillFactories[cardSubType] = factory;
    }

    /// <summary>
    /// Gets a skill instance for a given card subtype.
    /// </summary>
    /// <param name="cardSubType">The card subtype to look up.</param>
    /// <returns>A new skill instance, or null if no skill is registered for this subtype.</returns>
    public ISkill? GetSkillForEquipmentBySubType(CardSubType cardSubType)
    {
        if (cardSubType == CardSubType.Unknown)
            return null;

        if (!_subTypeSkillFactories.TryGetValue(cardSubType, out var factory))
            return null;

        return factory.CreateSkill();
    }

    /// <summary>
    /// Checks whether a card subtype has a registered skill.
    /// </summary>
    /// <param name="cardSubType">The card subtype to check.</param>
    /// <returns>True if a skill is registered for this subtype, false otherwise.</returns>
    public bool HasEquipmentSkillBySubType(CardSubType cardSubType)
    {
        return cardSubType != CardSubType.Unknown && _subTypeSkillFactories.ContainsKey(cardSubType);
    }

    /// <summary>
    /// Clears all registered equipment skills (both by DefinitionId and by CardSubType).
    /// Primarily used for testing.
    /// </summary>
    public void Clear()
    {
        _equipmentSkillFactories.Clear();
        _subTypeSkillFactories.Clear();
    }
}
