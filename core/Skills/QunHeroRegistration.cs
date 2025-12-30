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
        registry.RegisterSkill("qingnang", new Hero.QingnangSkillFactory());
        registry.RegisterSkill("wushuang", new Hero.WushuangSkillFactory());
        registry.RegisterSkill("biyue", new Hero.BiyueSkillFactory());
        registry.RegisterSkill("lijian", new Hero.LijianSkillFactory());

        // Register heroes with their skills
        // 1. 华佗 (Hua Tuo): 急救 (Jijiu), 青囊 (Qingnang)
        registry.RegisterHeroSkills("huatuo", new[] { "jijiu", "qingnang" });
        // 2. 吕布 (Lu Bu): 无双 (Wushuang)
        registry.RegisterHeroSkills("lubu", new[] { "wushuang" });
        // 3. 貂蝉 (Diao Chan): 离间 (Lijian), 闭月 (Biyue)
        registry.RegisterHeroSkills("diaochan", new[] { "lijian", "biyue" });
    }
}

