using System.Linq;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Skills.Hero;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests;

[TestClass]
public sealed class WeiHeroRegistrationTests
{
    #region Registration Tests

    /// <summary>
    /// Tests that RegisterAll registers all Wei faction skills.
    /// </summary>
    [TestMethod]
    public void RegisterAll_RegistersAllWeiSkills()
    {
        // Arrange
        var registry = new SkillRegistry();

        // Act
        WeiHeroRegistration.RegisterAll(registry);

        // Assert - Check all skills are registered
        Assert.IsTrue(registry.IsSkillRegistered("jianxiong"));
        Assert.IsTrue(registry.IsSkillRegistered("hujia"));
        Assert.IsTrue(registry.IsSkillRegistered("feedback"));
        Assert.IsTrue(registry.IsSkillRegistered("guicai"));
        Assert.IsTrue(registry.IsSkillRegistered("ganglie"));
        Assert.IsTrue(registry.IsSkillRegistered("tuxi"));
        Assert.IsTrue(registry.IsSkillRegistered("luoyi"));
        Assert.IsTrue(registry.IsSkillRegistered("tiandu"));
        Assert.IsTrue(registry.IsSkillRegistered("yiji"));
        Assert.IsTrue(registry.IsSkillRegistered("qingguo"));
        Assert.IsTrue(registry.IsSkillRegistered("luoshen"));
    }

    /// <summary>
    /// Tests that RegisterAll registers all Wei faction heroes with correct skills.
    /// </summary>
    [TestMethod]
    public void RegisterAll_RegistersAllWeiHeroes()
    {
        // Arrange
        var registry = new SkillRegistry();

        // Act
        WeiHeroRegistration.RegisterAll(registry);

        // Assert - Check all heroes are registered
        Assert.IsTrue(registry.HasHeroSkills("caocao"));
        Assert.IsTrue(registry.HasHeroSkills("simayi"));
        Assert.IsTrue(registry.HasHeroSkills("xiahoudun"));
        Assert.IsTrue(registry.HasHeroSkills("zhangliao"));
        Assert.IsTrue(registry.HasHeroSkills("xuchu"));
        Assert.IsTrue(registry.HasHeroSkills("guojia"));
        Assert.IsTrue(registry.HasHeroSkills("zhenji"));
    }

    /// <summary>
    /// Tests that Cao Cao has correct skills (jianxiong, hujia).
    /// </summary>
    [TestMethod]
    public void RegisterAll_CaoCaoHasCorrectSkills()
    {
        // Arrange
        var registry = new SkillRegistry();
        WeiHeroRegistration.RegisterAll(registry);

        // Act
        var skills = registry.GetSkillsForHero("caocao").ToList();

        // Assert
        Assert.AreEqual(2, skills.Count);
        Assert.IsTrue(skills.Any(s => s.Id == "jianxiong"));
        Assert.IsTrue(skills.Any(s => s.Id == "hujia"));
    }

    /// <summary>
    /// Tests that Sima Yi has correct skills (feedback, guicai).
    /// </summary>
    [TestMethod]
    public void RegisterAll_SimaYiHasCorrectSkills()
    {
        // Arrange
        var registry = new SkillRegistry();
        WeiHeroRegistration.RegisterAll(registry);

        // Act
        var skills = registry.GetSkillsForHero("simayi").ToList();

        // Assert
        Assert.AreEqual(2, skills.Count);
        Assert.IsTrue(skills.Any(s => s.Id == "feedback"));
        Assert.IsTrue(skills.Any(s => s.Id == "guicai"));
    }

    /// <summary>
    /// Tests that Xiahou Dun has correct skills (ganglie).
    /// </summary>
    [TestMethod]
    public void RegisterAll_XiahouDunHasCorrectSkills()
    {
        // Arrange
        var registry = new SkillRegistry();
        WeiHeroRegistration.RegisterAll(registry);

        // Act
        var skills = registry.GetSkillsForHero("xiahoudun").ToList();

        // Assert
        Assert.AreEqual(1, skills.Count);
        Assert.IsTrue(skills.Any(s => s.Id == "ganglie"));
    }

    /// <summary>
    /// Tests that Zhang Liao has correct skills (tuxi).
    /// </summary>
    [TestMethod]
    public void RegisterAll_ZhangLiaoHasCorrectSkills()
    {
        // Arrange
        var registry = new SkillRegistry();
        WeiHeroRegistration.RegisterAll(registry);

        // Act
        var skills = registry.GetSkillsForHero("zhangliao").ToList();

        // Assert
        Assert.AreEqual(1, skills.Count);
        Assert.IsTrue(skills.Any(s => s.Id == "tuxi"));
    }

    /// <summary>
    /// Tests that Xu Chu has correct skills (luoyi).
    /// </summary>
    [TestMethod]
    public void RegisterAll_XuChuHasCorrectSkills()
    {
        // Arrange
        var registry = new SkillRegistry();
        WeiHeroRegistration.RegisterAll(registry);

        // Act
        var skills = registry.GetSkillsForHero("xuchu").ToList();

        // Assert
        Assert.AreEqual(1, skills.Count);
        Assert.IsTrue(skills.Any(s => s.Id == "luoyi"));
    }

    /// <summary>
    /// Tests that Guo Jia has correct skills (tiandu, yiji).
    /// </summary>
    [TestMethod]
    public void RegisterAll_GuoJiaHasCorrectSkills()
    {
        // Arrange
        var registry = new SkillRegistry();
        WeiHeroRegistration.RegisterAll(registry);

        // Act
        var skills = registry.GetSkillsForHero("guojia").ToList();

        // Assert
        Assert.AreEqual(2, skills.Count);
        Assert.IsTrue(skills.Any(s => s.Id == "tiandu"));
        Assert.IsTrue(skills.Any(s => s.Id == "yiji"));
    }

    /// <summary>
    /// Tests that Zhen Ji has correct skills (qingguo, luoshen).
    /// </summary>
    [TestMethod]
    public void RegisterAll_ZhenJiHasCorrectSkills()
    {
        // Arrange
        var registry = new SkillRegistry();
        WeiHeroRegistration.RegisterAll(registry);

        // Act
        var skills = registry.GetSkillsForHero("zhenji").ToList();

        // Assert
        Assert.AreEqual(2, skills.Count);
        Assert.IsTrue(skills.Any(s => s.Id == "qingguo"));
        Assert.IsTrue(skills.Any(s => s.Id == "luoshen"));
    }

    /// <summary>
    /// Tests that RegisterAll can be called multiple times without errors.
    /// </summary>
    [TestMethod]
    public void RegisterAll_CanBeCalledMultipleTimes()
    {
        // Arrange
        var registry = new SkillRegistry();

        // Act & Assert - Should not throw
        WeiHeroRegistration.RegisterAll(registry);
        try
        {
            WeiHeroRegistration.RegisterAll(registry);
            Assert.Fail("Expected ArgumentException when registering duplicate skills");
        }
        catch (System.ArgumentException)
        {
            // Expected: duplicate skill registration should throw
        }
    }

    /// <summary>
    /// Tests that RegisterAll throws ArgumentNullException when registry is null.
    /// </summary>
    [TestMethod]
    public void RegisterAll_ThrowsWhenRegistryIsNull()
    {
        // Act & Assert
        Assert.ThrowsException<System.ArgumentNullException>(() =>
            WeiHeroRegistration.RegisterAll(null!));
    }

    /// <summary>
    /// Tests that all registered skills can be retrieved and have correct properties.
    /// </summary>
    [TestMethod]
    public void RegisterAll_AllSkillsCanBeRetrievedWithCorrectProperties()
    {
        // Arrange
        var registry = new SkillRegistry();
        WeiHeroRegistration.RegisterAll(registry);

        // Act & Assert - Verify each skill can be retrieved and has correct ID
        var jianxiong = registry.GetSkill("jianxiong");
        Assert.IsNotNull(jianxiong);
        Assert.AreEqual("jianxiong", jianxiong.Id);
        Assert.AreEqual("奸雄", jianxiong.Name);

        var hujia = registry.GetSkill("hujia");
        Assert.IsNotNull(hujia);
        Assert.AreEqual("hujia", hujia.Id);
        Assert.AreEqual("护驾", hujia.Name);

        var feedback = registry.GetSkill("feedback");
        Assert.IsNotNull(feedback);
        Assert.AreEqual("feedback", feedback.Id);
        Assert.AreEqual("反馈", feedback.Name);

        var guicai = registry.GetSkill("guicai");
        Assert.IsNotNull(guicai);
        Assert.AreEqual("guicai", guicai.Id);
        Assert.AreEqual("鬼才", guicai.Name);

        var ganglie = registry.GetSkill("ganglie");
        Assert.IsNotNull(ganglie);
        Assert.AreEqual("ganglie", ganglie.Id);
        Assert.AreEqual("刚烈", ganglie.Name);

        var tuxi = registry.GetSkill("tuxi");
        Assert.IsNotNull(tuxi);
        Assert.AreEqual("tuxi", tuxi.Id);
        Assert.AreEqual("突袭", tuxi.Name);

        var luoyi = registry.GetSkill("luoyi");
        Assert.IsNotNull(luoyi);
        Assert.AreEqual("luoyi", luoyi.Id);
        Assert.AreEqual("裸衣", luoyi.Name);

        var tiandu = registry.GetSkill("tiandu");
        Assert.IsNotNull(tiandu);
        Assert.AreEqual("tiandu", tiandu.Id);
        Assert.AreEqual("天妒", tiandu.Name);

        var yiji = registry.GetSkill("yiji");
        Assert.IsNotNull(yiji);
        Assert.AreEqual("yiji", yiji.Id);
        Assert.AreEqual("遗计", yiji.Name);

        var qingguo = registry.GetSkill("qingguo");
        Assert.IsNotNull(qingguo);
        Assert.AreEqual("qingguo", qingguo.Id);
        Assert.AreEqual("倾国", qingguo.Name);

        var luoshen = registry.GetSkill("luoshen");
        Assert.IsNotNull(luoshen);
        Assert.AreEqual("luoshen", luoshen.Id);
        Assert.AreEqual("洛神", luoshen.Name);
    }

    #endregion
}

