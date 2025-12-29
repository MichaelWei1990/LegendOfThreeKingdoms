using System.Linq;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Skills.Hero;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests;

[TestClass]
public sealed class WuHeroRegistrationTests
{
    #region Registration Tests

    /// <summary>
    /// Tests that RegisterAll registers all Wu faction skills (Standard Edition).
    /// </summary>
    [TestMethod]
    public void RegisterAll_RegistersAllWuSkills()
    {
        // Arrange
        var registry = new SkillRegistry();

        // Act
        WuHeroRegistration.RegisterAll(registry);

        // Assert - Check all skills are registered
        Assert.IsTrue(registry.IsSkillRegistered("liuli"), "liuli should be registered");
        Assert.IsTrue(registry.IsSkillRegistered("guose"), "guose should be registered");
        Assert.IsTrue(registry.IsSkillRegistered("lianying"), "lianying should be registered");
        Assert.IsTrue(registry.IsSkillRegistered("jieyin"), "jieyin should be registered");
        Assert.IsTrue(registry.IsSkillRegistered("yingzi"), "yingzi should be registered");
        Assert.IsTrue(registry.IsSkillRegistered("fanjian"), "fanjian should be registered");
        Assert.IsTrue(registry.IsSkillRegistered("zhiheng"), "zhiheng should be registered");
        Assert.IsTrue(registry.IsSkillRegistered("rescue"), "rescue should be registered");
        Assert.IsTrue(registry.IsSkillRegistered("qixi"), "qixi should be registered");
        Assert.IsTrue(registry.IsSkillRegistered("keji"), "keji should be registered");
        Assert.IsTrue(registry.IsSkillRegistered("kurou"), "kurou should be registered");
        Assert.IsTrue(registry.IsSkillRegistered("xiaoji"), "xiaoji should be registered");
        Assert.IsTrue(registry.IsSkillRegistered("modesty"), "modesty should be registered");
    }

    /// <summary>
    /// Tests that RegisterAll registers all Wu faction heroes with correct skills (Standard Edition).
    /// </summary>
    [TestMethod]
    public void RegisterAll_RegistersAllWuHeroes()
    {
        // Arrange
        var registry = new SkillRegistry();

        // Act
        WuHeroRegistration.RegisterAll(registry);

        // Assert - Check all heroes are registered
        Assert.IsTrue(registry.HasHeroSkills("sunquan"), "Sun Quan should be registered");
        Assert.IsTrue(registry.HasHeroSkills("ganning"), "Gan Ning should be registered");
        Assert.IsTrue(registry.HasHeroSkills("lvmeng"), "Lü Meng should be registered");
        Assert.IsTrue(registry.HasHeroSkills("huanggai"), "Huang Gai should be registered");
        Assert.IsTrue(registry.HasHeroSkills("zhouyu"), "Zhou Yu should be registered");
        Assert.IsTrue(registry.HasHeroSkills("daqiao"), "Da Qiao should be registered");
        Assert.IsTrue(registry.HasHeroSkills("luxun"), "Lu Xun should be registered");
        Assert.IsTrue(registry.HasHeroSkills("sunshangxiang"), "Sun Shangxiang should be registered");
    }

    /// <summary>
    /// Tests that Sun Quan has correct skills (zhiheng, rescue).
    /// </summary>
    [TestMethod]
    public void RegisterAll_SunQuanHasCorrectSkills()
    {
        // Arrange
        var registry = new SkillRegistry();
        WuHeroRegistration.RegisterAll(registry);

        // Act
        var skills = registry.GetSkillsForHero("sunquan").ToList();

        // Assert
        Assert.AreEqual(2, skills.Count, "Sun Quan should have 2 skills");
        Assert.IsTrue(skills.Any(s => s.Id == "zhiheng"), "Sun Quan should have zhiheng skill");
        Assert.IsTrue(skills.Any(s => s.Id == "rescue"), "Sun Quan should have rescue skill");
    }

    /// <summary>
    /// Tests that Gan Ning has correct skills (qixi).
    /// </summary>
    [TestMethod]
    public void RegisterAll_GanNingHasCorrectSkills()
    {
        // Arrange
        var registry = new SkillRegistry();
        WuHeroRegistration.RegisterAll(registry);

        // Act
        var skills = registry.GetSkillsForHero("ganning").ToList();

        // Assert
        Assert.AreEqual(1, skills.Count, "Gan Ning should have 1 skill");
        Assert.IsTrue(skills.Any(s => s.Id == "qixi"), "Gan Ning should have qixi skill");
    }

    /// <summary>
    /// Tests that Lü Meng has correct skills (keji).
    /// </summary>
    [TestMethod]
    public void RegisterAll_LvMengHasCorrectSkills()
    {
        // Arrange
        var registry = new SkillRegistry();
        WuHeroRegistration.RegisterAll(registry);

        // Act
        var skills = registry.GetSkillsForHero("lvmeng").ToList();

        // Assert
        Assert.AreEqual(1, skills.Count, "Lü Meng should have 1 skill");
        Assert.IsTrue(skills.Any(s => s.Id == "keji"), "Lü Meng should have keji skill");
    }

    /// <summary>
    /// Tests that Huang Gai has correct skills (kurou).
    /// </summary>
    [TestMethod]
    public void RegisterAll_HuangGaiHasCorrectSkills()
    {
        // Arrange
        var registry = new SkillRegistry();
        WuHeroRegistration.RegisterAll(registry);

        // Act
        var skills = registry.GetSkillsForHero("huanggai").ToList();

        // Assert
        Assert.AreEqual(1, skills.Count, "Huang Gai should have 1 skill");
        Assert.IsTrue(skills.Any(s => s.Id == "kurou"), "Huang Gai should have kurou skill");
    }

    /// <summary>
    /// Tests that Zhou Yu has correct skills (yingzi, fanjian).
    /// </summary>
    [TestMethod]
    public void RegisterAll_ZhouYuHasCorrectSkills()
    {
        // Arrange
        var registry = new SkillRegistry();
        WuHeroRegistration.RegisterAll(registry);

        // Act
        var skills = registry.GetSkillsForHero("zhouyu").ToList();

        // Assert
        Assert.AreEqual(2, skills.Count, "Zhou Yu should have 2 skills");
        Assert.IsTrue(skills.Any(s => s.Id == "yingzi"), "Zhou Yu should have yingzi skill");
        Assert.IsTrue(skills.Any(s => s.Id == "fanjian"), "Zhou Yu should have fanjian skill");
    }

    /// <summary>
    /// Tests that Da Qiao has correct skills (guose, liuli).
    /// </summary>
    [TestMethod]
    public void RegisterAll_DaQiaoHasCorrectSkills()
    {
        // Arrange
        var registry = new SkillRegistry();
        WuHeroRegistration.RegisterAll(registry);

        // Act
        var skills = registry.GetSkillsForHero("daqiao").ToList();

        // Assert
        Assert.AreEqual(2, skills.Count, "Da Qiao should have 2 skills");
        Assert.IsTrue(skills.Any(s => s.Id == "guose"), "Da Qiao should have guose skill");
        Assert.IsTrue(skills.Any(s => s.Id == "liuli"), "Da Qiao should have liuli skill");
    }

    /// <summary>
    /// Tests that Lu Xun has correct skills (modesty, lianying).
    /// </summary>
    [TestMethod]
    public void RegisterAll_LuXunHasCorrectSkills()
    {
        // Arrange
        var registry = new SkillRegistry();
        WuHeroRegistration.RegisterAll(registry);

        // Act
        var skills = registry.GetSkillsForHero("luxun").ToList();

        // Assert
        Assert.AreEqual(2, skills.Count, "Lu Xun should have 2 skills");
        Assert.IsTrue(skills.Any(s => s.Id == "modesty"), "Lu Xun should have modesty skill");
        Assert.IsTrue(skills.Any(s => s.Id == "lianying"), "Lu Xun should have lianying skill");
    }

    /// <summary>
    /// Tests that Sun Shangxiang has correct skills (jieyin, xiaoji).
    /// </summary>
    [TestMethod]
    public void RegisterAll_SunShangxiangHasCorrectSkills()
    {
        // Arrange
        var registry = new SkillRegistry();
        WuHeroRegistration.RegisterAll(registry);

        // Act
        var skills = registry.GetSkillsForHero("sunshangxiang").ToList();

        // Assert
        Assert.AreEqual(2, skills.Count, "Sun Shangxiang should have 2 skills");
        Assert.IsTrue(skills.Any(s => s.Id == "jieyin"), "Sun Shangxiang should have jieyin skill");
        Assert.IsTrue(skills.Any(s => s.Id == "xiaoji"), "Sun Shangxiang should have xiaoji skill");
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
        WuHeroRegistration.RegisterAll(registry);
        try
        {
            WuHeroRegistration.RegisterAll(registry);
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
            WuHeroRegistration.RegisterAll(null!));
    }

    /// <summary>
    /// Tests that all registered skills can be retrieved and have correct properties.
    /// </summary>
    [TestMethod]
    public void RegisterAll_AllSkillsCanBeRetrievedWithCorrectProperties()
    {
        // Arrange
        var registry = new SkillRegistry();
        WuHeroRegistration.RegisterAll(registry);

        // Act & Assert - Verify each skill can be retrieved and has correct ID
        var skillIds = new[] { "liuli", "guose", "lianying", "jieyin", "yingzi", "fanjian", "zhiheng", "rescue", "qixi", "keji", "kurou", "xiaoji", "modesty" };
        
        foreach (var skillId in skillIds)
        {
            var skill = registry.GetSkill(skillId);
            Assert.IsNotNull(skill, $"Skill {skillId} should be retrievable");
            Assert.AreEqual(skillId, skill.Id, $"Skill {skillId} should have correct ID");
        }
    }

    #endregion
}

