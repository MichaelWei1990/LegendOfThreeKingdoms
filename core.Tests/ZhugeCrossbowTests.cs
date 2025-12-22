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
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests;

[TestClass]
public sealed class ZhugeCrossbowTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
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

    #region Equipment Skill Registry Tests

    /// <summary>
    /// Tests that EquipmentSkillRegistry can register and retrieve Zhuge Crossbow skill by DefinitionId.
    /// Input: Empty registry, ZhugeCrossbowSkillFactory, definitionId "zhuge_crossbow".
    /// Expected: After registration, GetSkillForEquipment returns a skill with correct Id and SkillType.Locked.
    /// </summary>
    [TestMethod]
    public void EquipmentSkillRegistryRegisterZhugeCrossbowByDefinitionIdCanRetrieveSkill()
    {
        // Arrange
        var registry = new EquipmentSkillRegistry();
        var factory = new ZhugeCrossbowSkillFactory();

        // Act
        registry.RegisterEquipmentSkill("zhuge_crossbow", factory);
        var skill = registry.GetSkillForEquipment("zhuge_crossbow");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("zhuge_crossbow", skill.Id);
        Assert.AreEqual(SkillType.Locked, skill.Type);
    }

    /// <summary>
    /// Tests that EquipmentSkillRegistry can register and retrieve Zhuge Crossbow skill by CardSubType.
    /// Input: Empty registry, ZhugeCrossbowSkillFactory, CardSubType.Weapon.
    /// Expected: After registration, GetSkillForEquipmentBySubType returns a skill with correct Id.
    /// </summary>
    [TestMethod]
    public void EquipmentSkillRegistryRegisterZhugeCrossbowBySubTypeCanRetrieveSkill()
    {
        // Arrange
        var registry = new EquipmentSkillRegistry();
        var factory = new ZhugeCrossbowSkillFactory();

        // Act
        registry.RegisterEquipmentSkillBySubType(CardSubType.Weapon, factory);
        var skill = registry.GetSkillForEquipmentBySubType(CardSubType.Weapon);

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("zhuge_crossbow", skill.Id);
        Assert.AreEqual(SkillType.Locked, skill.Type);
    }

    #endregion

    #region Equip Resolver Tests

    /// <summary>
    /// Tests that EquipResolver successfully moves Zhuge Crossbow from hand to equipment zone.
    /// Input: 2-player game, player has zhuge crossbow card in hand, ChoiceResult selecting the card.
    /// Expected: Resolution succeeds, card is removed from hand zone and added to equipment zone.
    /// </summary>
    [TestMethod]
    public void EquipResolverEquipZhugeCrossbowMovesCardToEquipmentZone()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        var zhugeCrossbow = CreateZhugeCrossbowCard();
        
        // Add card to hand
        if (player.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(zhugeCrossbow);
        }

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();
        var context = new ResolutionContext(
            game,
            player,
            null,
            new ChoiceResult("test", player.Seat, null, new[] { zhugeCrossbow.Id }, null, null),
            stack,
            cardMoveService,
            ruleService,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null
        );

        var resolver = new EquipResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsFalse(player.HandZone.Cards.Contains(zhugeCrossbow));
        Assert.IsTrue(player.EquipmentZone.Cards.Contains(zhugeCrossbow));
    }

    #endregion

    #region Zhuge Crossbow Skill Tests

    /// <summary>
    /// Tests that ZhugeCrossbowSkill returns int.MaxValue for ModifyMaxSlashPerTurn when active.
    /// Input: 2-player game, active player with zhuge crossbow skill.
    /// Expected: ModifyMaxSlashPerTurn returns int.MaxValue, representing unlimited Slash usage.
    /// </summary>
    [TestMethod]
    public void ZhugeCrossbowSkillModifyMaxSlashPerTurnReturnsMaxValue()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = true;
        var skill = new ZhugeCrossbowSkill();

        // Act
        var result = skill.ModifyMaxSlashPerTurn(1, game, player);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(int.MaxValue, result.Value);
    }

    /// <summary>
    /// Tests that ZhugeCrossbowSkill does not modify limit when the owner is not active.
    /// Input: 2-player game, player is dead (IsAlive = false), zhuge crossbow skill.
    /// Expected: ModifyMaxSlashPerTurn returns null (no modification) when owner is not active.
    /// </summary>
    [TestMethod]
    public void ZhugeCrossbowSkillModifyMaxSlashPerTurnWhenOwnerIsNotActiveReturnsNull()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = false; // Skill should not be active
        var skill = new ZhugeCrossbowSkill();

        // Act
        var result = skill.ModifyMaxSlashPerTurn(1, game, player);

        // Assert
        Assert.IsNull(result);
    }

    /// <summary>
    /// Tests that ZhugeCrossbowSkill returns int.MaxValue regardless of the current limit value.
    /// Input: 2-player game, active player, current limit values of 1, 2, and 10.
    /// Expected: ModifyMaxSlashPerTurn always returns int.MaxValue when skill is active.
    /// </summary>
    [TestMethod]
    public void ZhugeCrossbowSkillModifyMaxSlashPerTurnAlwaysReturnsMaxValueRegardlessOfCurrent()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = true;
        var skill = new ZhugeCrossbowSkill();

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

    #region Integration Tests

    /// <summary>
    /// Tests that CardUsageRuleService allows unlimited Slash usage when player has Zhuge Crossbow equipped.
    /// Input: 2-player game, player has zhuge crossbow equipped, UsageCountThisTurn = 100 (far exceeds normal limit).
    /// Expected: CanUseCard returns allowed, because zhuge crossbow sets limit to int.MaxValue.
    /// </summary>
    [TestMethod]
    public void CardUsageRuleServiceWithZhugeCrossbowAllowsUnlimitedSlashUsage()
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

        // Setup equipment skill system
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("zhuge_crossbow", new ZhugeCrossbowSkillFactory());

        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.LoadSkillsForPlayer(game, source);

        // Manually add equipment skill (simulating EquipResolver behavior)
        var zhugeCrossbowSkill = equipmentSkillRegistry.GetSkillForEquipment("zhuge_crossbow");
        if (zhugeCrossbowSkill is not null)
        {
            skillManager.AddEquipmentSkill(game, source, zhugeCrossbowSkill);
        }

        // Create rule service with skill modifier provider
        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var phaseRules = new PhaseRuleService();
        var rangeRules = new RangeRuleService();
        var limitRules = new LimitRuleService();
        var service = new CardUsageRuleService(phaseRules, rangeRules, limitRules, modifierProvider);

        // Test with a very high usage count (should still be allowed)
        var context = new CardUsageContext(
            game,
            source,
            slash,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 100); // Far exceeds normal limit of 1

        // Act
        var result = service.CanUseCard(context);

        // Assert
        Assert.IsTrue(result.IsAllowed, "Slash should be allowed with Zhuge Crossbow even after 100 uses.");
    }

    /// <summary>
    /// Tests that CardUsageRuleService blocks Slash when Zhuge Crossbow is not equipped.
    /// Input: 2-player game, player does not have zhuge crossbow, UsageCountThisTurn = 1 (reached normal limit).
    /// Expected: CanUseCard returns disallowed with UsageLimitReached.
    /// </summary>
    [TestMethod]
    public void CardUsageRuleServiceWithoutZhugeCrossbowBlocksSlashWhenLimitReached()
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

        // Do NOT equip zhuge crossbow

        // Setup skill system (without equipment skill)
        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.LoadSkillsForPlayer(game, source);

        // Create rule service with skill modifier provider
        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var phaseRules = new PhaseRuleService();
        var rangeRules = new RangeRuleService();
        var limitRules = new LimitRuleService();
        var service = new CardUsageRuleService(phaseRules, rangeRules, limitRules, modifierProvider);

        // Test with usage count at limit
        var context = new CardUsageContext(
            game,
            source,
            slash,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 1); // Reached normal limit

        // Act
        var result = service.CanUseCard(context);

        // Assert
        Assert.IsFalse(result.IsAllowed);
        Assert.AreEqual(RuleErrorCode.UsageLimitReached, result.ErrorCode);
    }

    /// <summary>
    /// Tests that EquipResolver loads Zhuge Crossbow skill when equipping the weapon.
    /// Input: 2-player game, player equips zhuge crossbow via EquipResolver.
    /// Expected: Skill is loaded and active for the player.
    /// </summary>
    [TestMethod]
    public void EquipResolverLoadsZhugeCrossbowSkillWhenEquipping()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        var zhugeCrossbow = CreateZhugeCrossbowCard();
        
        // Add card to hand
        if (player.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(zhugeCrossbow);
        }

        // Setup equipment skill system
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("zhuge_crossbow", new ZhugeCrossbowSkillFactory());

        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.LoadSkillsForPlayer(game, player);

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();
        var context = new ResolutionContext(
            game,
            player,
            null,
            new ChoiceResult("test", player.Seat, null, new[] { zhugeCrossbow.Id }, null, null),
            stack,
            cardMoveService,
            ruleService,
            null,
            null,
            null,
            null,
            null,
            null,
            skillManager,
            equipmentSkillRegistry
        );

        var resolver = new EquipResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsTrue(player.EquipmentZone.Cards.Contains(zhugeCrossbow));

        // Verify skill is active by checking if it modifies the slash limit
        var modifiers = new SkillRuleModifierProvider(skillManager).GetModifiersFor(game, player);
        var maxSlash = 1;
        foreach (var modifier in modifiers)
        {
            var modified = modifier.ModifyMaxSlashPerTurn(maxSlash, game, player);
            if (modified.HasValue)
            {
                maxSlash = modified.Value;
            }
        }
        Assert.AreEqual(int.MaxValue, maxSlash, "Zhuge Crossbow skill should set max slash to int.MaxValue.");
    }

    #endregion
}
