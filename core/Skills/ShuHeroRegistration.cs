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
        registry.RegisterSkill("guanxing", new Hero.GuanxingSkillFactory());
        registry.RegisterSkill("rende", new Hero.RendeSkillFactory());

        // Register heroes with their skills (Standard Edition)
        // 1. 赵云 (Zhao Yun): 龙胆 (Longdan)
        registry.RegisterHeroSkills("zhaoyun", new[] { "longdan" });
        // 2. 诸葛亮 (Zhuge Liang): 观星 (Guanxing)
        registry.RegisterHeroSkills("zhugeliang", new[] { "guanxing" });
        // 3. 刘备 (Liu Bei): 仁德 (Rende)
        registry.RegisterHeroSkills("liubei", new[] { "rende" });
    }
}

