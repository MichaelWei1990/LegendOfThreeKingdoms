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
        registry.RegisterSkill("lianying", new Hero.LianyingSkillFactory());
        registry.RegisterSkill("jieyin", new Hero.JieYinSkillFactory());
        registry.RegisterSkill("yingzi", new Hero.YingziSkillFactory());
        registry.RegisterSkill("fanjian", new Hero.FanJianSkillFactory());
        registry.RegisterSkill("zhiheng", new Hero.ZhiHengSkillFactory());

        // Register heroes with their skills
        // 1. 大乔 (Da Qiao)
        registry.RegisterHeroSkills("daqiao", new[] { "liuli", "guose" });

        // 2. 陆逊 (Lu Xun)
        registry.RegisterHeroSkills("luxun", new[] { "lianying", "qianxun" });

        // 3. 孙尚香 (Sun Shangxiang)
        registry.RegisterHeroSkills("sunshangxiang", new[] { "jieyin", "xiaoji" });

        // 4. 周瑜 (Zhou Yu)
        registry.RegisterHeroSkills("zhouyu", new[] { "fanjian", "yingzi" });

        // 5. 孙权 (Sun Quan)
        registry.RegisterHeroSkills("sunquan", new[] { "zhiheng" });
    }
}

