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
        registry.RegisterSkill("empty_city", new Hero.EmptyCitySkillFactory());
        registry.RegisterSkill("rende", new Hero.RendeSkillFactory());
        registry.RegisterSkill("jijiang", new Hero.JijiangSkillFactory());
        registry.RegisterSkill("wusheng", new Hero.WushengSkillFactory());
        registry.RegisterSkill("roar", new Hero.RoarSkillFactory());
        registry.RegisterSkill("horsemanship", new Hero.HorsemanshipSkillFactory());
        registry.RegisterSkill("tieqi", new Hero.TieqiSkillFactory());
        registry.RegisterSkill("jizhi", new Hero.JizhiSkillFactory());
        registry.RegisterSkill("qicai", new Hero.QicaiSkillFactory());

        // Register heroes with their skills (Standard Edition)
        // 1. 刘备 (Liu Bei, SHU 001): 仁德 (Rende), 激将 (Jijiang) - Lord skill
        registry.RegisterHeroSkills("liubei", new[] { "rende", "jijiang" });
        // 2. 关羽 (Guan Yu, SHU 002): 武圣 (Wusheng)
        registry.RegisterHeroSkills("guanyu", new[] { "wusheng" });
        // 3. 张飞 (Zhang Fei, SHU 003): 咆哮 (Roar)
        registry.RegisterHeroSkills("zhangfei", new[] { "roar" });
        // 4. 诸葛亮 (Zhuge Liang, SHU 004): 观星 (Guanxing), 空城 (Empty City)
        registry.RegisterHeroSkills("zhugeliang", new[] { "guanxing", "empty_city" });
        // 5. 赵云 (Zhao Yun, SHU 005): 龙胆 (Longdan)
        registry.RegisterHeroSkills("zhaoyun", new[] { "longdan" });
        // 6. 马超 (Ma Chao, SHU 006): 马术 (Horsemanship), 铁骑 (Tieqi)
        registry.RegisterHeroSkills("machao", new[] { "horsemanship", "tieqi" });
        // 7. 黄月英 (Huang Yueying, SHU 007): 集智 (Jizhi), 奇才 (Qicai)
        registry.RegisterHeroSkills("huangyueying", new[] { "jizhi", "qicai" });
    }
}

