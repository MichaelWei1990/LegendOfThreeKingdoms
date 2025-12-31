using System;

namespace LegendOfThreeKingdoms.Core.Skills;

/// <summary>
/// Registration helper for Shu (蜀) faction heroes and their skills.
/// Registers all Shu faction heroes with their associated skills in the skill registry.
/// </summary>
public static class ShuHeroRegistration
{
    /// <summary>
    /// Registers all Shu faction heroes and their skills in the skill registry.
    /// </summary>
    /// <param name="registry">The skill registry to register skills and heroes in.</param>
    /// <exception cref="ArgumentNullException">Thrown if registry is null.</exception>
    public static void RegisterAll(SkillRegistry registry)
    {
        if (registry is null)
            throw new ArgumentNullException(nameof(registry));

        // Register all skill factories
        registry.RegisterSkill("longdan", new Hero.LongdanSkillFactory());

        // Register heroes with their skills (Standard Edition)
        // 1. 赵云 (Zhao Yun)
        registry.RegisterHeroSkills("zhaoyun", new[] { "longdan" });
    }
}

