using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Configuration;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Skills.Hero;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests;

/// <summary>
/// Tests for Wusheng (武圣) skill.
/// </summary>
[TestClass]
public sealed class WushengTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }
    #region Factory Tests

    /// <summary>
    /// Tests that WushengSkillFactory creates a WushengSkill instance.
    /// </summary>
    [TestMethod]
    public void WushengSkillFactory_CreatesWushengSkill()
    {
        // Arrange
        var factory = new WushengSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsInstanceOfType(skill, typeof(WushengSkill));
        Assert.AreEqual("wusheng", skill.Id);
        Assert.AreEqual("武圣", skill.Name);
    }

    #endregion

    #region Skill Properties Tests

    /// <summary>
    /// Tests that WushengSkill has correct properties.
    /// </summary>
    [TestMethod]
    public void WushengSkill_HasCorrectProperties()
    {
        // Arrange
        var skill = new WushengSkill();

        // Assert
        Assert.AreEqual("wusheng", skill.Id);
        Assert.AreEqual("武圣", skill.Name);
        Assert.AreEqual(SkillType.Locked, skill.Type);
        Assert.IsTrue(skill.Capabilities.HasFlag(SkillCapability.ModifiesRules));
    }

    #endregion

    #region CreateVirtualCard Tests

    /// <summary>
    /// Tests that CreateVirtualCard returns null for non-red cards.
    /// </summary>
    [TestMethod]
    public void CreateVirtualCard_NonRedCard_ReturnsNull()
    {
        // Arrange
        var skill = new WushengSkill();
        var game = CreateDefaultGame(2);
        var owner = game.Players[0];
        var blackCard = new Card
        {
            Id = 1,
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = Suit.Spade, // Black card
            Rank = 1
        };

        // Act
        var result = skill.CreateVirtualCard(blackCard, game, owner);

        // Assert
        Assert.IsNull(result);
    }

    /// <summary>
    /// Tests that CreateVirtualCard creates virtual Slash for red cards.
    /// </summary>
    [TestMethod]
    public void CreateVirtualCard_RedCard_CreatesVirtualSlash()
    {
        // Arrange
        var skill = new WushengSkill();
        var game = CreateDefaultGame(2);
        var owner = game.Players[0];
        var redCard = new Card
        {
            Id = 1,
            CardType = CardType.Basic,
            CardSubType = CardSubType.Peach, // Not Slash, but red
            Suit = Suit.Heart, // Red card
            Rank = 1
        };

        // Act
        var result = skill.CreateVirtualCard(redCard, game, owner);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(CardSubType.Slash, result.CardSubType);
        Assert.AreEqual(redCard.Suit, result.Suit); // Inherits suit
        Assert.AreEqual(redCard.Rank, result.Rank); // Inherits rank
        Assert.AreEqual(redCard.Id, result.Id); // Keeps same ID
    }

    /// <summary>
    /// Tests that CreateVirtualCard returns null when skill is not active (owner is dead).
    /// </summary>
    [TestMethod]
    public void CreateVirtualCard_SkillNotActive_ReturnsNull()
    {
        // Arrange
        var skill = new WushengSkill();
        var game = CreateDefaultGame(2);
        var owner = game.Players[0];
        
        // Verify IsActive returns false for dead player
        owner.IsAlive = false;
        Assert.IsFalse(skill.IsActive(game, owner), "IsActive should return false for dead player");
        
        var redCard = new Card
        {
            Id = 1,
            CardType = CardType.Basic,
            CardSubType = CardSubType.Peach,
            Suit = Suit.Heart,
            Rank = 1
        };

        // Act
        var result = skill.CreateVirtualCard(redCard, game, owner);

        // Assert
        Assert.IsNull(result, $"Expected null but got {result?.CardSubType} for dead player");
    }

    #endregion

    #region CanUseMaterialCard Tests

    // Note: CanUseMaterialCard tests require proper game state setup
    // (e.g., Play phase, valid targets, etc.) which is complex.
    // These tests can be added later with proper integration test setup.

    #endregion
}

