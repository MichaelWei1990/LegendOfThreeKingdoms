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
        registry.RegisterSkill("rescue", new Hero.RescueSkillFactory());
        registry.RegisterSkill("qixi", new Hero.QixiSkillFactory());
        registry.RegisterSkill("keji", new Hero.KejiSkillFactory());
        registry.RegisterSkill("kurou", new Hero.KurouSkillFactory());
        registry.RegisterSkill("xiaoji", new Hero.XiaojiSkillFactory());
        registry.RegisterSkill("modesty", new Hero.ModestySkillFactory());

        // Register heroes with their skills (Standard Edition)
        // 1. 孙权 (Sun Quan) - Lord
        registry.RegisterHeroSkills("sunquan", new[] { "zhiheng", "rescue" });

        // 2. 甘宁 (Gan Ning)
        registry.RegisterHeroSkills("ganning", new[] { "qixi" });

        // 3. 吕蒙 (Lü Meng)
        registry.RegisterHeroSkills("lvmeng", new[] { "keji" });

        // 4. 黄盖 (Huang Gai)
        registry.RegisterHeroSkills("huanggai", new[] { "kurou" });

        // 5. 周瑜 (Zhou Yu)
        registry.RegisterHeroSkills("zhouyu", new[] { "yingzi", "fanjian" });

        // 6. 大乔 (Da Qiao)
        registry.RegisterHeroSkills("daqiao", new[] { "guose", "liuli" });

        // 7. 陆逊 (Lu Xun)
        registry.RegisterHeroSkills("luxun", new[] { "modesty", "lianying" });

        // 8. 孙尚香 (Sun Shangxiang)
        registry.RegisterHeroSkills("sunshangxiang", new[] { "jieyin", "xiaoji" });
    }
}

