using System;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Skills;

/// <summary>
/// Registration helper for equipment skills.
/// Registers all equipment skills with their associated factories in the equipment skill registry.
/// </summary>
public static class EquipmentRegistration
{
    /// <summary>
    /// Registers all equipment skills in the equipment skill registry.
    /// </summary>
    /// <param name="registry">The equipment skill registry to register skills in.</param>
    /// <exception cref="ArgumentNullException">Thrown if registry is null.</exception>
    public static void RegisterAll(EquipmentSkillRegistry registry)
    {
        if (registry is null)
            throw new ArgumentNullException(nameof(registry));

        // Register weapon skills by DefinitionId
        registry.RegisterEquipmentSkill("Weapon_QinglongYanyueDao", new Equipment.QinglongYanyueDaoSkillFactory());
        registry.RegisterEquipmentSkill("serpent_spear", new Equipment.SerpentSpearSkillFactory());
        registry.RegisterEquipmentSkill("stone_axe", new Equipment.StoneAxeSkillFactory());
        registry.RegisterEquipmentSkill("qinggang_sword", new Equipment.QinggangSwordSkillFactory());
        registry.RegisterEquipmentSkill("ice_sword", new Equipment.IceSwordSkillFactory());
        registry.RegisterEquipmentSkill("twin_swords", new Equipment.TwinSwordsSkillFactory());
        registry.RegisterEquipmentSkill("kirin_bow", new Equipment.KirinBowSkillFactory());
        registry.RegisterEquipmentSkill("zhuge_crossbow", new Equipment.ZhugeCrossbowSkillFactory());

        // Register armor skills by DefinitionId
        registry.RegisterEquipmentSkill("renwang_shield", new Equipment.RenwangShieldSkillFactory());
        registry.RegisterEquipmentSkill("bagua_array", new Equipment.BaguaFormationFactory());

        // Register horse skills by CardSubType (shared skills for all horses of the same type)
        registry.RegisterEquipmentSkillBySubType(CardSubType.OffensiveHorse, new Equipment.OffensiveHorseSkillFactory());
        registry.RegisterEquipmentSkillBySubType(CardSubType.DefensiveHorse, new Equipment.DefensiveHorseSkillFactory());
    }
}

