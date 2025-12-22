using System;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Skills.Equipment;
using LegendOfThreeKingdoms.Core.Skills.Hero;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests;

[TestClass]
public sealed class RoarTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Card CreateSlashCard(int id = 100)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "slash_basic",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = Suit.Spade,
            Rank = 5
        };
    }

    private static Card CreateZhugeCrossbowCard(int id = 1, string definitionId = "zhuge_crossbow", string name = "诸葛连弩")
    {
        return new Card
        {
            Id = id,
            DefinitionId = definitionId,
            Name = name,
            CardType = CardType.Equip,
            CardSubType = CardSubType.Weapon,
            Suit = Suit.Spade,
            Rank = 1
        };
    }

    #region Skill Registry Tests

    /// <summary>
    /// Tests that RoarSkillFactory creates correct skill instance.
    /// Input: RoarSkillFactory instance.
    /// Expected: Created skill has correct Id, Name, and SkillType.Locked.
    /// </summary>
    [TestMethod]
    public void RoarSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new RoarSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("roar", skill.Id);
        Assert.AreEqual("咆哮", skill.Name);
        Assert.AreEqual(SkillType.Locked, skill.Type);
    }

    /// <summary>
    /// Tests that SkillRegistry can register and retrieve Roar skill.
    /// Input: Empty registry, RoarSkillFactory.
    /// Expected: After registration, GetSkill returns a skill with correct Id.
    /// </summary>
    [TestMethod]
    public void SkillRegistryRegisterRoarSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new RoarSkillFactory();

        // Act
        registry.RegisterSkill("roar", factory);
        var skill = registry.GetSkill("roar");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("roar", skill.Id);
        Assert.AreEqual("咆哮", skill.Name);
    }

    /// <summary>
    /// Tests that SkillRegistry prevents duplicate skill registrations.
    /// Input: Registry with "roar" already registered, attempting to register again.
    /// Expected: ArgumentException is thrown when trying to register duplicate skill ID.
    /// </summary>
    [TestMethod]
    public void SkillRegistryRegisterRoarSkillWithDuplicateIdThrowsException()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory1 = new RoarSkillFactory();
        var factory2 = new RoarSkillFactory();

        // Act
        registry.RegisterSkill("roar", factory1);

        // Assert
        Assert.ThrowsException<ArgumentException>(() =>
            registry.RegisterSkill("roar", factory2));
    }

    #endregion

    #region Max Slash Per Turn Modification Tests

    /// <summary>
    /// Tests that RoarSkill returns int.MaxValue for ModifyMaxSlashPerTurn when active.
    /// Input: 2-player game, active player with roar skill.
    /// Expected: ModifyMaxSlashPerTurn returns int.MaxValue, representing unlimited Slash usage.
    /// </summary>
    [TestMethod]
    public void RoarSkillModifyMaxSlashPerTurnReturnsMaxValue()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = true;
        var skill = new RoarSkill();

        // Act
        var result = skill.ModifyMaxSlashPerTurn(1, game, player);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(int.MaxValue, result.Value);
    }

    /// <summary>
    /// Tests that RoarSkill does not modify limit when the owner is not active.
    /// Input: 2-player game, player is dead (IsAlive = false), roar skill.
    /// Expected: ModifyMaxSlashPerTurn returns null (no modification) when owner is not active.
    /// </summary>
    [TestMethod]
    public void RoarSkillModifyMaxSlashPerTurnWhenOwnerIsNotActiveReturnsNull()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = false; // Skill should not be active
        var skill = new RoarSkill();

        // Act
        var result = skill.ModifyMaxSlashPerTurn(1, game, player);

        // Assert
        Assert.IsNull(result);
    }

    /// <summary>
    /// Tests that RoarSkill returns int.MaxValue regardless of the current limit value.
    /// Input: 2-player game, active player, current limit values of 1, 2, and 10.
    /// Expected: ModifyMaxSlashPerTurn always returns int.MaxValue when skill is active.
    /// </summary>
    [TestMethod]
    public void RoarSkillModifyMaxSlashPerTurnAlwaysReturnsMaxValueRegardlessOfCurrent()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = true;
        var skill = new RoarSkill();

        // Act & Assert
        var result1 = skill.ModifyMaxSlashPerTurn(1, game, player);
        Assert.IsNotNull(result1);
        Assert.AreEqual(int.MaxValue, result1.Value);

        var result2 = skill.ModifyMaxSlashPerTurn(2, game, player);
        Assert.IsNotNull(result2);
        Assert.AreEqual(int.MaxValue, result2.Value);

        var result10 = skill.ModifyMaxSlashPerTurn(10, game, player);
        Assert.IsNotNull(result10);
        Assert.AreEqual(int.MaxValue, result10.Value);
    }

    #endregion

    #region Skill Stacking Tests

    /// <summary>
    /// Tests that RoarSkill stacks with ZhugeCrossbowSkill (both return int.MaxValue).
    /// Input: 2-player game, player has both roar skill and zhuge crossbow equipment.
    /// Expected: After both modifications, max slash is still int.MaxValue (stacking doesn't change result).
    /// </summary>
    [TestMethod]
    public void RoarSkillStacksWithZhugeCrossbowSkill()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = true;

        var roarSkill = new RoarSkill();
        var zhugeCrossbowSkill = new ZhugeCrossbowSkill();

        // Simulate stacking: apply roar first, then zhuge crossbow
        var initialLimit = 1;

        // Act
        var afterRoar = roarSkill.ModifyMaxSlashPerTurn(initialLimit, game, player);
        var afterBoth = zhugeCrossbowSkill.ModifyMaxSlashPerTurn(afterRoar!.Value, game, player);

        // Assert
        Assert.IsNotNull(afterRoar);
        Assert.IsNotNull(afterBoth);
        Assert.AreEqual(int.MaxValue, afterRoar.Value); // First modification: 1 -> int.MaxValue
        Assert.AreEqual(int.MaxValue, afterBoth.Value); // Second modification: int.MaxValue -> int.MaxValue
    }

    /// <summary>
    /// Tests that stacking order does not affect the result (commutative property).
    /// Input: 2-player game, player has both skills.
    /// Expected: Whether roar or zhuge crossbow is applied first, final result is the same (int.MaxValue).
    /// </summary>
    [TestMethod]
    public void RoarSkillStackingOrderDoesNotAffectResult()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = true;

        var roarSkill = new RoarSkill();
        var zhugeCrossbowSkill = new ZhugeCrossbowSkill();

        var initialLimit = 1;

        // Act - Apply roar first, then zhuge crossbow
        var order1Step1 = roarSkill.ModifyMaxSlashPerTurn(initialLimit, game, player);
        var order1Step2 = zhugeCrossbowSkill.ModifyMaxSlashPerTurn(order1Step1!.Value, game, player);

        // Act - Apply zhuge crossbow first, then roar
        var order2Step1 = zhugeCrossbowSkill.ModifyMaxSlashPerTurn(initialLimit, game, player);
        var order2Step2 = roarSkill.ModifyMaxSlashPerTurn(order2Step1!.Value, game, player);

        // Assert
        Assert.IsNotNull(order1Step2);
        Assert.IsNotNull(order2Step2);
        Assert.AreEqual(int.MaxValue, order1Step2.Value);
        Assert.AreEqual(int.MaxValue, order2Step2.Value);
        Assert.AreEqual(order1Step2.Value, order2Step2.Value, "Stacking order should not affect the result");
    }

    #endregion

    #region Integration Tests

    /// <summary>
    /// Tests that CardUsageRuleService allows unlimited Slash usage when player has Roar skill.
    /// Input: 2-player game, player has roar skill, UsageCountThisTurn = 100 (far exceeds normal limit).
    /// Expected: CanUseCard returns allowed, because roar sets limit to int.MaxValue.
    /// </summary>
    [TestMethod]
    public void CardUsageRuleServiceWithRoarAllowsUnlimitedSlashUsage()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];

        var slash = CreateSlashCard();

        // Add slash to hand
        if (source.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(slash);
        }

        // Setup skill system
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("roar", new RoarSkillFactory());
        skillRegistry.RegisterHeroSkills("hero_test", new[] { "roar" });

        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Create player with hero ID
        var playerWithHero = new Player
        {
            Seat = source.Seat,
            CampId = source.CampId,
            FactionId = source.FactionId,
            HeroId = "hero_test",
            MaxHealth = source.MaxHealth,
            CurrentHealth = source.CurrentHealth,
            IsAlive = source.IsAlive,
            HandZone = source.HandZone,
            EquipmentZone = source.EquipmentZone,
            JudgementZone = source.JudgementZone
        };

        skillManager.LoadSkillsForPlayer(game, playerWithHero);

        // Create rule service with skill modifier provider
        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var phaseRules = new PhaseRuleService();
        var rangeRules = new RangeRuleService();
        var limitRules = new LimitRuleService();
        var service = new CardUsageRuleService(phaseRules, rangeRules, limitRules, modifierProvider);

        // Test with a very high usage count (should still be allowed)
        var context = new CardUsageContext(
            game,
            playerWithHero,
            slash,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 100); // Far exceeds normal limit of 1

        // Act
        var result = service.CanUseCard(context);

        // Assert
        Assert.IsTrue(result.IsAllowed, "Slash should be allowed with Roar even after 100 uses.");
    }

    /// <summary>
    /// Tests that CardUsageRuleService correctly applies both RoarSkill and ZhugeCrossbowSkill (stacking).
    /// Input: 2-player game, player has roar skill and zhuge crossbow equipment.
    /// Expected: CanUseCard correctly applies both modifications (both set limit to int.MaxValue).
    /// </summary>
    [TestMethod]
    public void CardUsageRuleServiceAppliesRoarAndZhugeCrossbowStacking()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];

        var slash = CreateSlashCard();
        var zhugeCrossbow = CreateZhugeCrossbowCard();

        // Add slash to hand
        if (source.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(slash);
        }

        // Equip zhuge crossbow
        if (source.EquipmentZone is Zone equipmentZone)
        {
            equipmentZone.MutableCards.Add(zhugeCrossbow);
        }

        // Setup skill system
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("roar", new RoarSkillFactory());
        skillRegistry.RegisterHeroSkills("hero_test", new[] { "roar" });

        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Create player with hero ID
        var playerWithHero = new Player
        {
            Seat = source.Seat,
            CampId = source.CampId,
            FactionId = source.FactionId,
            HeroId = "hero_test",
            MaxHealth = source.MaxHealth,
            CurrentHealth = source.CurrentHealth,
            IsAlive = source.IsAlive,
            HandZone = source.HandZone,
            EquipmentZone = source.EquipmentZone,
            JudgementZone = source.JudgementZone
        };

        skillManager.LoadSkillsForPlayer(game, playerWithHero);

        // Add zhuge crossbow equipment skill
        var equipmentRegistry = new EquipmentSkillRegistry();
        equipmentRegistry.RegisterEquipmentSkill("zhuge_crossbow", new ZhugeCrossbowSkillFactory());
        skillManager.AddEquipmentSkill(game, playerWithHero, equipmentRegistry.GetSkillForEquipment("zhuge_crossbow")!);

        // Create rule service with skill modifier provider
        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var phaseRules = new PhaseRuleService();
        var rangeRules = new RangeRuleService();
        var limitRules = new LimitRuleService();
        var service = new CardUsageRuleService(phaseRules, rangeRules, limitRules, modifierProvider);

        // Test with a very high usage count (should still be allowed)
        var context = new CardUsageContext(
            game,
            playerWithHero,
            slash,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 1000); // Very high usage count

        // Act
        var result = service.CanUseCard(context);

        // Assert
        Assert.IsTrue(result.IsAllowed, "Slash should be allowed with both Roar and Zhuge Crossbow even after 1000 uses.");

        // Verify that max slash is set to int.MaxValue
        var modifiers = modifierProvider.GetModifiersFor(game, playerWithHero);
        var maxSlash = 1;
        foreach (var modifier in modifiers)
        {
            var modified = modifier.ModifyMaxSlashPerTurn(maxSlash, game, playerWithHero);
            if (modified.HasValue)
            {
                maxSlash = modified.Value;
            }
        }
        Assert.AreEqual(int.MaxValue, maxSlash, "Both Roar and Zhuge Crossbow should set max slash to int.MaxValue.");
    }

    /// <summary>
    /// Tests that CardUsageRuleService blocks Slash when player has no unlimited slash skills.
    /// Input: 2-player game, player has no roar skill and no zhuge crossbow, UsageCountThisTurn = 1.
    /// Expected: CanUseCard returns disallowed when usage count reaches the limit.
    /// </summary>
    [TestMethod]
    public void CardUsageRuleServiceBlocksSlashWhenNoUnlimitedSlashSkills()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];

        var slash = CreateSlashCard();

        // Add slash to hand
        if (source.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(slash);
        }

        // Setup skill system (no roar skill)
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.LoadSkillsForPlayer(game, source);

        // Create rule service with skill modifier provider
        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var phaseRules = new PhaseRuleService();
        var rangeRules = new RangeRuleService();
        var limitRules = new LimitRuleService();
        var service = new CardUsageRuleService(phaseRules, rangeRules, limitRules, modifierProvider);

        // Test with usage count at limit (should be blocked)
        var context = new CardUsageContext(
            game,
            source,
            slash,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 1); // At normal limit

        // Act
        var result = service.CanUseCard(context);

        // Assert
        Assert.IsFalse(result.IsAllowed, "Slash should be blocked when usage count reaches limit and no unlimited slash skills.");
    }

    #endregion
}
