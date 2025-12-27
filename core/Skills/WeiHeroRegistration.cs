using System;

namespace LegendOfThreeKingdoms.Core.Skills;

/// <summary>
/// Registration helper for Wei faction heroes and their skills.
/// Registers all Wei faction heroes with their associated skills in the skill registry.
/// </summary>
public static class WeiHeroRegistration
{
    /// <summary>
    /// Registers all Wei faction heroes and their skills in the skill registry.
    /// </summary>
    /// <param name="registry">The skill registry to register skills and heroes in.</param>
    /// <exception cref="ArgumentNullException">Thrown if registry is null.</exception>
    public static void RegisterAll(SkillRegistry registry)
    {
        if (registry is null)
            throw new ArgumentNullException(nameof(registry));

        // Register all skill factories
        registry.RegisterSkill("jianxiong", new Hero.JianxiongSkillFactory());
        registry.RegisterSkill("hujia", new Hero.HujiaSkillFactory());
        registry.RegisterSkill("feedback", new Hero.FeedbackSkillFactory());
        registry.RegisterSkill("guicai", new Hero.GuicaiSkillFactory());
        registry.RegisterSkill("ganglie", new Hero.GanglieSkillFactory());
        registry.RegisterSkill("tuxi", new Hero.TuxiSkillFactory());
        registry.RegisterSkill("luoyi", new Hero.LuoYiSkillFactory());
        registry.RegisterSkill("tiandu", new Hero.TianduSkillFactory());
        registry.RegisterSkill("yiji", new Hero.YiJiSkillFactory());
        registry.RegisterSkill("qingguo", new Hero.QingGuoSkillFactory());
        registry.RegisterSkill("luoshen", new Hero.LuoshenSkillFactory());

        // Register heroes with their skills
        // 1. 曹操 (Cao Cao)
        registry.RegisterHeroSkills("caocao", new[] { "jianxiong", "hujia" });

        // 2. 司马懿 (Sima Yi)
        registry.RegisterHeroSkills("simayi", new[] { "feedback", "guicai" });

        // 3. 夏侯惇 (Xiahou Dun)
        registry.RegisterHeroSkills("xiahoudun", new[] { "ganglie" });

        // 4. 张辽 (Zhang Liao)
        registry.RegisterHeroSkills("zhangliao", new[] { "tuxi" });

        // 5. 许褚 (Xu Chu)
        registry.RegisterHeroSkills("xuchu", new[] { "luoyi" });

        // 6. 郭嘉 (Guo Jia)
        registry.RegisterHeroSkills("guojia", new[] { "tiandu", "yiji" });

        // 7. 甄姬 (Zhen Ji)
        registry.RegisterHeroSkills("zhenji", new[] { "qingguo", "luoshen" });
    }
}

