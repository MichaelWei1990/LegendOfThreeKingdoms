using System;

namespace LegendOfThreeKingdoms.Core.Skills;

/// <summary>
/// Registration helper for Qun (群雄) faction heroes and their skills.
/// Registers all Qun faction heroes with their associated skills in the skill registry.
/// </summary>
public static class QunHeroRegistration
{
    /// <summary>
    /// Registers all Qun faction heroes and their skills in the skill registry.
    /// </summary>
    /// <param name="registry">The skill registry to register skills and heroes in.</param>
    /// <exception cref="ArgumentNullException">Thrown if registry is null.</exception>
    public static void RegisterAll(SkillRegistry registry)
    {
        if (registry is null)
            throw new ArgumentNullException(nameof(registry));

        // Register all skill factories
        registry.RegisterSkill("jijiu", new Hero.JijiuSkillFactory());

        // Register heroes with their skills
        // 1. 华佗 (Hua Tuo)
        registry.RegisterHeroSkills("huatuo", new[] { "jijiu" });
    }
}

