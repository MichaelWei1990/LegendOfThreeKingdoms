using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Configuration;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Skills.Hero;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests;

[TestClass]
public sealed class JijiangTests
{
    private static Game CreateDefaultGame(int playerCount = 3)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Game CreateGameWithFactions(int playerCount, Dictionary<int, string> factionMap)
    {
        var baseConfig = CoreApi.CreateDefaultConfig(playerCount);
        var playerConfigs = new List<PlayerConfig>();
        
        for (int i = 0; i < playerCount; i++)
        {
            var playerConfig = new PlayerConfig
            {
                Seat = i,
                MaxHealth = 4,
                InitialHealth = 4,
                FactionId = factionMap.TryGetValue(i, out var faction) ? faction : null
            };
            playerConfigs.Add(playerConfig);
        }

        var config = new GameConfig
        {
            PlayerConfigs = playerConfigs,
            DeckConfig = baseConfig.DeckConfig,
            Seed = baseConfig.Seed,
            GameModeId = baseConfig.GameModeId,
            GameVariantOptions = baseConfig.GameVariantOptions
        };

        return Game.FromConfig(config);
    }

    #region Skill Factory Tests

    /// <summary>
    /// Tests that JijiangSkillFactory creates correct skill instance.
    /// </summary>
    [TestMethod]
    public void JijiangSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new JijiangSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("jijiang", skill.Id);
        Assert.AreEqual("激将", skill.Name);
        Assert.AreEqual(SkillType.Trigger, skill.Type);
        Assert.IsTrue(skill is IResponseAssistanceSkill);
    }

    /// <summary>
    /// Tests that SkillRegistry can register and retrieve Jijiang skill.
    /// </summary>
    [TestMethod]
    public void SkillRegistryRegisterJijiangSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new JijiangSkillFactory();

        // Act
        registry.RegisterSkill("jijiang", factory);
        var skill = registry.GetSkill("jijiang");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("jijiang", skill.Id);
        Assert.AreEqual("激将", skill.Name);
    }

    #endregion

    #region Skill Properties Tests

    /// <summary>
    /// Tests that JijiangSkill has correct properties.
    /// </summary>
    [TestMethod]
    public void JijiangSkill_HasCorrectProperties()
    {
        // Arrange
        var skill = new JijiangSkill();

        // Assert
        Assert.AreEqual("jijiang", skill.Id);
        Assert.AreEqual("激将", skill.Name);
        Assert.AreEqual(SkillType.Trigger, skill.Type);
        Assert.AreEqual(SkillCapability.None, skill.Capabilities);
    }

    #endregion

    #region CanProvideAssistance Tests

    /// <summary>
    /// Tests that JijiangSkill can provide assistance when owner is Lord and has Shu faction assistants.
    /// </summary>
    [TestMethod]
    public void JijiangSkill_CanProvideAssistance_WhenOwnerIsLordAndHasShuAssistants()
    {
        // Arrange
        var factionMap = new Dictionary<int, string>
        {
            { 0, null }, // Owner (no faction needed for test)
            { 1, "shu" }, // Assistant
            { 2, "wei" }  // Other player
        };
        var game = CreateGameWithFactions(3, factionMap);
        var owner = game.Players[0];
        owner.Flags["IsLord"] = true;

        var skill = new JijiangSkill();

        // Act
        var canProvide = skill.CanProvideAssistance(
            game,
            owner,
            ResponseType.SlashAgainstDuel,
            null);

        // Assert
        Assert.IsTrue(canProvide);
    }

    /// <summary>
    /// Tests that JijiangSkill cannot provide assistance when owner is not Lord.
    /// </summary>
    [TestMethod]
    public void JijiangSkill_CannotProvideAssistance_WhenOwnerIsNotLord()
    {
        // Arrange
        var factionMap = new Dictionary<int, string>
        {
            { 0, null }, // Owner (not Lord)
            { 1, "shu" }, // Assistant
            { 2, "wei" }  // Other player
        };
        var game = CreateGameWithFactions(3, factionMap);
        var owner = game.Players[0];
        // Owner is not Lord

        var skill = new JijiangSkill();

        // Act
        var canProvide = skill.CanProvideAssistance(
            game,
            owner,
            ResponseType.SlashAgainstDuel,
            null);

        // Assert
        Assert.IsFalse(canProvide);
    }

    /// <summary>
    /// Tests that JijiangSkill cannot provide assistance when there are no Shu faction assistants.
    /// </summary>
    [TestMethod]
    public void JijiangSkill_CannotProvideAssistance_WhenNoShuAssistants()
    {
        // Arrange
        var factionMap = new Dictionary<int, string>
        {
            { 0, null }, // Owner (Lord)
            { 1, "wei" }, // Not Shu
            { 2, "wu" }   // Not Shu
        };
        var game = CreateGameWithFactions(3, factionMap);
        var owner = game.Players[0];
        owner.Flags["IsLord"] = true;

        var skill = new JijiangSkill();

        // Act
        var canProvide = skill.CanProvideAssistance(
            game,
            owner,
            ResponseType.SlashAgainstDuel,
            null);

        // Assert
        Assert.IsFalse(canProvide);
    }

    /// <summary>
    /// Tests that JijiangSkill only works for Slash response types.
    /// </summary>
    [TestMethod]
    public void JijiangSkill_OnlyWorksForSlashResponseTypes()
    {
        // Arrange
        var factionMap = new Dictionary<int, string>
        {
            { 0, null }, // Owner (Lord)
            { 1, "shu" }, // Assistant
            { 2, "wei" }  // Other player
        };
        var game = CreateGameWithFactions(3, factionMap);
        var owner = game.Players[0];
        owner.Flags["IsLord"] = true;

        var skill = new JijiangSkill();

        // Act & Assert
        Assert.IsTrue(skill.CanProvideAssistance(game, owner, ResponseType.SlashAgainstDuel, null));
        Assert.IsTrue(skill.CanProvideAssistance(game, owner, ResponseType.SlashAgainstNanmanRushin, null));
        Assert.IsFalse(skill.CanProvideAssistance(game, owner, ResponseType.JinkAgainstSlash, null));
        Assert.IsFalse(skill.CanProvideAssistance(game, owner, ResponseType.PeachForDying, null));
    }

    #endregion

    #region GetAssistants Tests

    /// <summary>
    /// Tests that JijiangSkill returns Shu faction assistants in seat order starting from owner's next seat.
    /// </summary>
    [TestMethod]
    public void JijiangSkill_GetAssistants_ReturnsShuFactionPlayersInSeatOrder()
    {
        // Arrange
        var factionMap = new Dictionary<int, string>
        {
            { 0, "shu" }, // Owner is also Shu, but should be excluded
            { 1, "shu" }, // Assistant
            { 2, "wei" }, // Not Shu
            { 3, "shu" }  // Assistant
        };
        var game = CreateGameWithFactions(4, factionMap);
        var owner = game.Players[0];

        var skill = new JijiangSkill();

        // Act
        var assistants = skill.GetAssistants(game, owner);

        // Assert
        Assert.AreEqual(2, assistants.Count);
        Assert.AreEqual(game.Players[1].Seat, assistants[0].Seat); // First assistant should be seat 1
        Assert.AreEqual(game.Players[3].Seat, assistants[1].Seat); // Second assistant should be seat 3
    }

    /// <summary>
    /// Tests that JijiangSkill excludes dead players from assistants.
    /// </summary>
    [TestMethod]
    public void JijiangSkill_GetAssistants_ExcludesDeadPlayers()
    {
        // Arrange
        var factionMap = new Dictionary<int, string>
        {
            { 0, null }, // Owner
            { 1, "shu" }, // Assistant (alive)
            { 2, "shu" }  // Assistant (dead)
        };
        var game = CreateGameWithFactions(3, factionMap);
        var owner = game.Players[0];
        game.Players[2].IsAlive = false; // Mark as dead

        var skill = new JijiangSkill();

        // Act
        var assistants = skill.GetAssistants(game, owner);

        // Assert
        Assert.AreEqual(1, assistants.Count);
        Assert.AreEqual(game.Players[1].Seat, assistants[0].Seat);
    }

    /// <summary>
    /// Tests that JijiangSkill excludes owner from assistants.
    /// </summary>
    [TestMethod]
    public void JijiangSkill_GetAssistants_ExcludesOwner()
    {
        // Arrange
        var factionMap = new Dictionary<int, string>
        {
            { 0, "shu" }, // Owner (Shu, but should be excluded)
            { 1, "shu" }, // Assistant
            { 2, "shu" }  // Assistant
        };
        var game = CreateGameWithFactions(3, factionMap);
        var owner = game.Players[0];

        var skill = new JijiangSkill();

        // Act
        var assistants = skill.GetAssistants(game, owner);

        // Assert
        Assert.AreEqual(2, assistants.Count);
        Assert.IsFalse(assistants.Any(a => a.Seat == owner.Seat));
    }

    #endregion

    #region ShouldActivate Tests

    /// <summary>
    /// Tests that JijiangSkill ShouldActivate returns true when owner confirms.
    /// </summary>
    [TestMethod]
    public void JijiangSkill_ShouldActivate_ReturnsTrueWhenOwnerConfirms()
    {
        // Arrange
        var factionMap = new Dictionary<int, string>
        {
            { 0, null }, // Owner (Lord)
            { 1, "shu" }, // Assistant
            { 2, "wei" }  // Other player
        };
        var game = CreateGameWithFactions(3, factionMap);
        var owner = game.Players[0];
        owner.Flags["IsLord"] = true;

        var skill = new JijiangSkill();

        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: true // Owner confirms
            );
        };

        // Act
        var shouldActivate = skill.ShouldActivate(game, owner, getPlayerChoice);

        // Assert
        Assert.IsTrue(shouldActivate);
    }

    /// <summary>
    /// Tests that JijiangSkill ShouldActivate returns false when owner declines.
    /// </summary>
    [TestMethod]
    public void JijiangSkill_ShouldActivate_ReturnsFalseWhenOwnerDeclines()
    {
        // Arrange
        var factionMap = new Dictionary<int, string>
        {
            { 0, null }, // Owner (Lord)
            { 1, "shu" }, // Assistant
            { 2, "wei" }  // Other player
        };
        var game = CreateGameWithFactions(3, factionMap);
        var owner = game.Players[0];
        owner.Flags["IsLord"] = true;

        var skill = new JijiangSkill();

        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: false // Owner declines
            );
        };

        // Act
        var shouldActivate = skill.ShouldActivate(game, owner, getPlayerChoice);

        // Assert
        Assert.IsFalse(shouldActivate);
    }

    #endregion

    #region CanProvideAssistanceForUse Tests

    /// <summary>
    /// Tests that JijiangSkill can provide assistance for active Slash usage when owner is Lord and has Shu faction assistants.
    /// </summary>
    [TestMethod]
    public void JijiangSkill_CanProvideAssistanceForUse_WhenOwnerIsLordAndHasShuAssistants()
    {
        // Arrange
        var factionMap = new Dictionary<int, string>
        {
            { 0, null }, // Owner (Lord)
            { 1, "shu" }, // Assistant
            { 2, "wei" }  // Other player
        };
        var game = CreateGameWithFactions(3, factionMap);
        var owner = game.Players[0];
        owner.Flags["IsLord"] = true;

        var skill = new JijiangSkill();

        // Act
        var canProvide = skill.CanProvideAssistanceForUse(game, owner);

        // Assert
        Assert.IsTrue(canProvide);
    }

    /// <summary>
    /// Tests that JijiangSkill cannot provide assistance for active Slash usage when owner is not Lord.
    /// </summary>
    [TestMethod]
    public void JijiangSkill_CannotProvideAssistanceForUse_WhenOwnerIsNotLord()
    {
        // Arrange
        var factionMap = new Dictionary<int, string>
        {
            { 0, null }, // Owner (not Lord)
            { 1, "shu" }, // Assistant
            { 2, "wei" }  // Other player
        };
        var game = CreateGameWithFactions(3, factionMap);
        var owner = game.Players[0];
        // Owner is not Lord

        var skill = new JijiangSkill();

        // Act
        var canProvide = skill.CanProvideAssistanceForUse(game, owner);

        // Assert
        Assert.IsFalse(canProvide);
    }

    /// <summary>
    /// Tests that JijiangSkill cannot provide assistance for active Slash usage when there are no Shu faction assistants.
    /// </summary>
    [TestMethod]
    public void JijiangSkill_CannotProvideAssistanceForUse_WhenNoShuAssistants()
    {
        // Arrange
        var factionMap = new Dictionary<int, string>
        {
            { 0, null }, // Owner (Lord)
            { 1, "wei" }, // Not Shu
            { 2, "wu" }   // Not Shu
        };
        var game = CreateGameWithFactions(3, factionMap);
        var owner = game.Players[0];
        owner.Flags["IsLord"] = true;

        var skill = new JijiangSkill();

        // Act
        var canProvide = skill.CanProvideAssistanceForUse(game, owner);

        // Assert
        Assert.IsFalse(canProvide);
    }

    /// <summary>
    /// Tests that JijiangSkill cannot provide assistance for active Slash usage when skill is not active.
    /// </summary>
    [TestMethod]
    public void JijiangSkill_CannotProvideAssistanceForUse_WhenSkillIsNotActive()
    {
        // Arrange
        var factionMap = new Dictionary<int, string>
        {
            { 0, null }, // Owner (Lord)
            { 1, "shu" }, // Assistant
            { 2, "wei" }  // Other player
        };
        var game = CreateGameWithFactions(3, factionMap);
        var owner = game.Players[0];
        owner.Flags["IsLord"] = true;
        owner.IsAlive = false; // Owner is dead, skill is not active

        var skill = new JijiangSkill();

        // Act
        var canProvide = skill.CanProvideAssistanceForUse(game, owner);

        // Assert
        Assert.IsFalse(canProvide);
    }

    #endregion
}

