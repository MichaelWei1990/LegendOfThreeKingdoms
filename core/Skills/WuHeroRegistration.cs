using System;

namespace LegendOfThreeKingdoms.Core.Skills;

/// <summary>
/// Registration helper for Wu faction heroes and their skills.
/// Registers all Wu faction heroes with their associated skills in the skill registry.
/// </summary>
public static class WuHeroRegistration
{
    /// <summary>
    /// Registers all Wu faction heroes and their skills in the skill registry.
    /// </summary>
    /// <param name="registry">The skill registry to register skills and heroes in.</param>
    /// <exception cref="ArgumentNullException">Thrown if registry is null.</exception>
    public static void RegisterAll(SkillRegistry registry)
    {
        if (registry is null)
            throw new ArgumentNullException(nameof(registry));

        // Register all skill factories
        registry.RegisterSkill("liuli", new Hero.LiuliSkillFactory());
        registry.RegisterSkill("guose", new Hero.GuoseSkillFactory());

        // Register heroes with their skills
        // 1. 大乔 (Da Qiao)
        registry.RegisterHeroSkills("daqiao", new[] { "liuli", "guose" });
    }
}

