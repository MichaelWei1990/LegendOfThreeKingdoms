using System;
using System.Collections.Generic;
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
public sealed class FangTianHuaJiTests
{
    private static Game CreateDefaultGame(int playerCount = 4)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Card CreateFangTianHuaJiCard(int id = 1, string definitionId = "fang_tian_hua_ji", string name = "方天画戟")
    {
        return new Card
        {
            Id = id,
            DefinitionId = definitionId,
            Name = name,
            CardType = CardType.Equip,
            CardSubType = CardSubType.Weapon,
            Suit = Suit.Spade,
            Rank = 5
        };
    }

    private static Card CreateSlashCard(int id, Suit suit = Suit.Spade, int rank = 5)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"slash_{id}",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = suit,
            Rank = rank
        };
    }

    private static Card CreateTestCard(int id, CardSubType subType = CardSubType.Slash)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"test_card_{id}",
            CardType = CardType.Basic,
            CardSubType = subType,
            Suit = Suit.Spade,
            Rank = 5
        };
    }

    #region Skill Factory Tests

    /// <summary>
    /// Tests that FangTianHuaJiSkillFactory creates correct skill instance.
    /// Input: FangTianHuaJiSkillFactory instance.
    /// Expected: Created skill has correct Id, Name, and SkillType.Locked.
    /// </summary>
    [TestMethod]
    public void FangTianHuaJiSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new FangTianHuaJiSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("fang_tian_hua_ji", skill.Id);
        Assert.AreEqual("方天画戟", skill.Name);
        Assert.AreEqual(SkillType.Locked, skill.Type);
    }

    /// <summary>
    /// Tests that EquipmentSkillRegistry can register and retrieve Fang Tian Hua Ji skill by DefinitionId.
    /// Input: Empty registry, FangTianHuaJiSkillFactory, DefinitionId "fang_tian_hua_ji".
    /// Expected: After registration, GetSkillForEquipment returns a skill with correct Id.
    /// </summary>
    [TestMethod]
    public void EquipmentSkillRegistryRegisterFangTianHuaJiCanRetrieveSkill()
    {
        // Arrange
        var registry = new EquipmentSkillRegistry();
        var factory = new FangTianHuaJiSkillFactory();

        // Act
        registry.RegisterEquipmentSkill("fang_tian_hua_ji", factory);
        var skill = registry.GetSkillForEquipment("fang_tian_hua_ji");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("fang_tian_hua_ji", skill.Id);
        Assert.AreEqual("方天画戟", skill.Name);
    }

    #endregion

    #region Attack Range Tests

    /// <summary>
    /// Tests that Fang Tian Hua Ji provides attack range of 4.
    /// Input: Game, attacker with Fang Tian Hua Ji equipped.
    /// Expected: ModifyAttackDistance returns 4.
    /// </summary>
    [TestMethod]
    public void FangTianHuaJiProvidesAttackRangeOf4()
    {
        // Arrange
        var game = CreateDefaultGame(4);
        var attacker = game.Players[0];
        var defender = game.Players[1];
        var skill = new FangTianHuaJiSkill();

        // Act
        var modified = skill.ModifyAttackDistance(1, game, attacker, defender);

        // Assert
        Assert.IsNotNull(modified);
        Assert.AreEqual(4, modified.Value);
    }

    /// <summary>
    /// Tests that Fang Tian Hua Ji does not modify distance when the owner is not active.
    /// Input: 4-player game, attacker is dead (IsAlive = false), Fang Tian Hua Ji skill.
    /// Expected: ModifyAttackDistance returns null (no modification) when owner is not active.
    /// </summary>
    [TestMethod]
    public void FangTianHuaJiModifyAttackDistanceWhenOwnerIsNotActiveReturnsNull()
    {
        // Arrange
        var game = CreateDefaultGame(4);
        var attacker = game.Players[0];
        var defender = game.Players[1];
        attacker.IsAlive = false; // Owner is not active
        var skill = new FangTianHuaJiSkill();

        // Act
        var modified = skill.ModifyAttackDistance(1, game, attacker, defender);

        // Assert
        Assert.IsNull(modified);
    }

    /// <summary>
    /// Tests that RangeRuleService correctly applies Fang Tian Hua Ji skill to set attack distance to 4.
    /// Input: 4-player game, attacker and defender, attacker has Fang Tian Hua Ji equipped and skill active.
    /// Expected: Attack distance is set to 4.
    /// </summary>
    [TestMethod]
    public void RangeRuleServiceWithFangTianHuaJiSetsAttackRangeTo4()
    {
        // Arrange
        var game = CreateDefaultGame(4);
        var attacker = game.Players[0];
        var defender = game.Players[1];
        
        // Equip Fang Tian Hua Ji to attacker
        var fangTianHuaJi = CreateFangTianHuaJiCard();
        if (attacker.EquipmentZone is Zone equipmentZone)
        {
            equipmentZone.MutableCards.Add(fangTianHuaJi);
        }

        // Setup skill manager and equipment skill registry
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("fang_tian_hua_ji", new FangTianHuaJiSkillFactory());
        
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.LoadSkillsForPlayer(game, attacker);
        
        var fangTianHuaJiSkill = equipmentSkillRegistry.GetSkillForEquipment("fang_tian_hua_ji");
        if (fangTianHuaJiSkill is not null)
        {
            skillManager.AddEquipmentSkill(game, attacker, fangTianHuaJiSkill);
        }

        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var rangeRuleService = new RangeRuleService(modifierProvider);

        // Act
        var attackDistance = rangeRuleService.GetAttackDistance(game, attacker, defender);

        // Assert
        // With Fang Tian Hua Ji, attack distance should be set to 4
        Assert.AreEqual(4, attackDistance);
    }

    #endregion

    #region Target Limit Modification Tests

    /// <summary>
    /// Tests that Fang Tian Hua Ji increases max targets to 3 when Slash is the last hand card.
    /// Input: 4-player game, player has Fang Tian Hua Ji equipped, hand has only 1 Slash card.
    /// Expected: ModifyMaxTargets returns 3 (base 1 + 2 additional) when Slash is the last hand card.
    /// </summary>
    [TestMethod]
    public void FangTianHuaJiIncreasesMaxTargetsTo3WhenSlashIsLastHandCard()
    {
        // Arrange
        var game = CreateDefaultGame(4);
        var player = game.Players[0];
        player.IsAlive = true;
        
        // Player has only 1 Slash card in hand (will be last card after paying cost)
        var slash = CreateSlashCard(100);
        if (player.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(slash);
        }

        // Equip Fang Tian Hua Ji
        var fangTianHuaJi = CreateFangTianHuaJiCard();
        if (player.EquipmentZone is Zone equipmentZone)
        {
            equipmentZone.MutableCards.Add(fangTianHuaJi);
        }

        // Setup skill
        var eventBus = new BasicEventBus();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("fang_tian_hua_ji", new FangTianHuaJiSkillFactory());
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.LoadSkillsForPlayer(game, player);
        
        var skill = equipmentSkillRegistry.GetSkillForEquipment("fang_tian_hua_ji");
        if (skill is not null)
        {
            skillManager.AddEquipmentSkill(game, player, skill);
        }

        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var modifiers = modifierProvider.GetModifiersFor(game, player);

        var context = new CardUsageContext(
            game,
            player,
            slash,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 0);

        // Act
        var baseMaxTargets = 1;
        var modifiedMaxTargets = baseMaxTargets;
        foreach (var modifier in modifiers)
        {
            var modified = modifier.ModifyMaxTargets(modifiedMaxTargets, context);
            if (modified.HasValue)
            {
                modifiedMaxTargets = modified.Value;
            }
        }

        // Assert
        Assert.AreEqual(3, modifiedMaxTargets, "Fang Tian Hua Ji should allow up to 3 targets when Slash is the last hand card.");
    }

    /// <summary>
    /// Tests that Fang Tian Hua Ji does not increase max targets when player has multiple hand cards.
    /// Input: 4-player game, player has Fang Tian Hua Ji equipped, hand has 1 Slash and 1 other card.
    /// Expected: ModifyMaxTargets returns null or base value (1) because Slash is not the last hand card.
    /// </summary>
    [TestMethod]
    public void FangTianHuaJiDoesNotIncreaseMaxTargetsWhenPlayerHasMultipleHandCards()
    {
        // Arrange
        var game = CreateDefaultGame(4);
        var player = game.Players[0];
        player.IsAlive = true;
        
        // Player has 2 cards in hand: 1 Slash and 1 other card
        var slash = CreateSlashCard(100);
        var otherCard = CreateTestCard(200, CardSubType.Peach);
        if (player.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(slash);
            handZone.MutableCards.Add(otherCard);
        }

        // Equip Fang Tian Hua Ji
        var fangTianHuaJi = CreateFangTianHuaJiCard();
        if (player.EquipmentZone is Zone equipmentZone)
        {
            equipmentZone.MutableCards.Add(fangTianHuaJi);
        }

        // Setup skill
        var eventBus = new BasicEventBus();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("fang_tian_hua_ji", new FangTianHuaJiSkillFactory());
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.LoadSkillsForPlayer(game, player);
        
        var skill = equipmentSkillRegistry.GetSkillForEquipment("fang_tian_hua_ji");
        if (skill is not null)
        {
            skillManager.AddEquipmentSkill(game, player, skill);
        }

        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var modifiers = modifierProvider.GetModifiersFor(game, player);

        var context = new CardUsageContext(
            game,
            player,
            slash,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 0);

        // Act
        var baseMaxTargets = 1;
        var modifiedMaxTargets = baseMaxTargets;
        foreach (var modifier in modifiers)
        {
            var modified = modifier.ModifyMaxTargets(modifiedMaxTargets, context);
            if (modified.HasValue)
            {
                modifiedMaxTargets = modified.Value;
            }
        }

        // Assert
        Assert.AreEqual(1, modifiedMaxTargets, "Fang Tian Hua Ji should not increase targets when player has multiple hand cards.");
    }

    /// <summary>
    /// Tests that Fang Tian Hua Ji does not increase max targets for non-Slash cards.
    /// Input: 4-player game, player has Fang Tian Hua Ji equipped, hand has only 1 Peach card.
    /// Expected: ModifyMaxTargets returns null or base value because card is not Slash.
    /// </summary>
    [TestMethod]
    public void FangTianHuaJiDoesNotIncreaseMaxTargetsForNonSlashCards()
    {
        // Arrange
        var game = CreateDefaultGame(4);
        var player = game.Players[0];
        player.IsAlive = true;
        
        // Player has only 1 Peach card in hand
        var peach = CreateTestCard(100, CardSubType.Peach);
        if (player.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(peach);
        }

        // Equip Fang Tian Hua Ji
        var fangTianHuaJi = CreateFangTianHuaJiCard();
        if (player.EquipmentZone is Zone equipmentZone)
        {
            equipmentZone.MutableCards.Add(fangTianHuaJi);
        }

        // Setup skill
        var eventBus = new BasicEventBus();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("fang_tian_hua_ji", new FangTianHuaJiSkillFactory());
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.LoadSkillsForPlayer(game, player);
        
        var skill = equipmentSkillRegistry.GetSkillForEquipment("fang_tian_hua_ji");
        if (skill is not null)
        {
            skillManager.AddEquipmentSkill(game, player, skill);
        }

        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var modifiers = modifierProvider.GetModifiersFor(game, player);

        var context = new CardUsageContext(
            game,
            player,
            peach,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 0);

        // Act
        var baseMaxTargets = 0; // Peach has 0 max targets
        var modifiedMaxTargets = baseMaxTargets;
        foreach (var modifier in modifiers)
        {
            var modified = modifier.ModifyMaxTargets(modifiedMaxTargets, context);
            if (modified.HasValue)
            {
                modifiedMaxTargets = modified.Value;
            }
        }

        // Assert
        Assert.AreEqual(0, modifiedMaxTargets, "Fang Tian Hua Ji should not modify targets for non-Slash cards.");
    }

    /// <summary>
    /// Tests that Fang Tian Hua Ji does not increase max targets when Slash is not from hand.
    /// Input: 4-player game, player has Fang Tian Hua Ji equipped, Slash card is in equipment zone (not hand).
    /// Expected: ModifyMaxTargets returns null or base value because Slash is not from hand.
    /// </summary>
    [TestMethod]
    public void FangTianHuaJiDoesNotIncreaseMaxTargetsWhenSlashIsNotFromHand()
    {
        // Arrange
        var game = CreateDefaultGame(4);
        var player = game.Players[0];
        player.IsAlive = true;
        
        // Slash card is in equipment zone (not hand) - this simulates a virtual/converted Slash
        var slash = CreateSlashCard(100);
        if (player.EquipmentZone is Zone equipmentZone)
        {
            equipmentZone.MutableCards.Add(slash);
        }

        // Equip Fang Tian Hua Ji
        var fangTianHuaJi = CreateFangTianHuaJiCard();
        if (player.EquipmentZone is Zone equipmentZone2)
        {
            equipmentZone2.MutableCards.Add(fangTianHuaJi);
        }

        // Setup skill
        var eventBus = new BasicEventBus();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("fang_tian_hua_ji", new FangTianHuaJiSkillFactory());
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.LoadSkillsForPlayer(game, player);
        
        var skill = equipmentSkillRegistry.GetSkillForEquipment("fang_tian_hua_ji");
        if (skill is not null)
        {
            skillManager.AddEquipmentSkill(game, player, skill);
        }

        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var modifiers = modifierProvider.GetModifiersFor(game, player);

        var context = new CardUsageContext(
            game,
            player,
            slash,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 0);

        // Act
        var baseMaxTargets = 1;
        var modifiedMaxTargets = baseMaxTargets;
        foreach (var modifier in modifiers)
        {
            var modified = modifier.ModifyMaxTargets(modifiedMaxTargets, context);
            if (modified.HasValue)
            {
                modifiedMaxTargets = modified.Value;
            }
        }

        // Assert
        Assert.AreEqual(1, modifiedMaxTargets, "Fang Tian Hua Ji should not increase targets when Slash is not from hand.");
    }

    #endregion

    #region Integration Tests

    /// <summary>
    /// Tests that ActionQueryService correctly applies Fang Tian Hua Ji to increase max targets in ActionDescriptor.
    /// Input: 4-player game, player has Fang Tian Hua Ji equipped, hand has only 1 Slash card.
    /// Expected: ActionDescriptor for Slash has MaxTargets = 3.
    /// </summary>
    [TestMethod]
    public void ActionQueryServiceWithFangTianHuaJiIncreasesMaxTargetsInActionDescriptor()
    {
        // Arrange
        var game = CreateDefaultGame(4);
        game.CurrentPhase = Phase.Play;
        var player = game.Players[0];
        
        // Player has only 1 Slash card in hand
        var slash = CreateSlashCard(100);
        if (player.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(slash);
        }

        // Equip Fang Tian Hua Ji
        var fangTianHuaJi = CreateFangTianHuaJiCard();
        if (player.EquipmentZone is Zone equipmentZone)
        {
            equipmentZone.MutableCards.Add(fangTianHuaJi);
        }

        // Setup equipment skill system
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("fang_tian_hua_ji", new FangTianHuaJiSkillFactory());

        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.LoadSkillsForPlayer(game, player);

        // Manually add equipment skill (simulating EquipResolver behavior)
        var fangTianHuaJiSkill = equipmentSkillRegistry.GetSkillForEquipment("fang_tian_hua_ji");
        if (fangTianHuaJiSkill is not null)
        {
            skillManager.AddEquipmentSkill(game, player, fangTianHuaJiSkill);
        }

        // Create rule service with skill modifier provider
        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var phaseRules = new PhaseRuleService();
        var rangeRules = new RangeRuleService(modifierProvider);
        var limitRules = new LimitRuleService();
        var cardUsageRules = new CardUsageRuleService(phaseRules, rangeRules, limitRules, modifierProvider);
        var actionQueryService = new ActionQueryService(phaseRules, cardUsageRules, skillManager, modifierProvider);

        var ruleContext = new RuleContext(game, player);

        // Act
        var actionsResult = actionQueryService.GetAvailableActions(ruleContext);

        // Assert
        Assert.IsTrue(actionsResult.HasAny, "Should have available actions.");
        
        var slashAction = actionsResult.Items.FirstOrDefault(a => a.ActionId == "UseSlash");
        Assert.IsNotNull(slashAction, "Should have UseSlash action.");
        Assert.IsNotNull(slashAction.TargetConstraints, "TargetConstraints should not be null.");
        Assert.AreEqual(3, slashAction.TargetConstraints.MaxTargets, "Fang Tian Hua Ji should allow up to 3 targets when Slash is the last hand card.");
    }

    /// <summary>
    /// Tests that ActionQueryService does not increase max targets when player has multiple hand cards.
    /// Input: 4-player game, player has Fang Tian Hua Ji equipped, hand has 1 Slash and 1 other card.
    /// Expected: ActionDescriptor for Slash has MaxTargets = 1 (not increased).
    /// </summary>
    [TestMethod]
    public void ActionQueryServiceWithFangTianHuaJiDoesNotIncreaseMaxTargetsWhenMultipleHandCards()
    {
        // Arrange
        var game = CreateDefaultGame(4);
        game.CurrentPhase = Phase.Play;
        var player = game.Players[0];
        
        // Player has 2 cards in hand: 1 Slash and 1 other card
        var slash = CreateSlashCard(100);
        var otherCard = CreateTestCard(200, CardSubType.Peach);
        if (player.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(slash);
            handZone.MutableCards.Add(otherCard);
        }

        // Equip Fang Tian Hua Ji
        var fangTianHuaJi = CreateFangTianHuaJiCard();
        if (player.EquipmentZone is Zone equipmentZone)
        {
            equipmentZone.MutableCards.Add(fangTianHuaJi);
        }

        // Setup equipment skill system
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("fang_tian_hua_ji", new FangTianHuaJiSkillFactory());

        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.LoadSkillsForPlayer(game, player);

        // Manually add equipment skill (simulating EquipResolver behavior)
        var fangTianHuaJiSkill = equipmentSkillRegistry.GetSkillForEquipment("fang_tian_hua_ji");
        if (fangTianHuaJiSkill is not null)
        {
            skillManager.AddEquipmentSkill(game, player, fangTianHuaJiSkill);
        }

        // Create rule service with skill modifier provider
        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var phaseRules = new PhaseRuleService();
        var rangeRules = new RangeRuleService(modifierProvider);
        var limitRules = new LimitRuleService();
        var cardUsageRules = new CardUsageRuleService(phaseRules, rangeRules, limitRules, modifierProvider);
        var actionQueryService = new ActionQueryService(phaseRules, cardUsageRules, skillManager, modifierProvider);

        var ruleContext = new RuleContext(game, player);

        // Act
        var actionsResult = actionQueryService.GetAvailableActions(ruleContext);

        // Assert
        Assert.IsTrue(actionsResult.HasAny, "Should have available actions.");
        
        var slashAction = actionsResult.Items.FirstOrDefault(a => a.ActionId == "UseSlash");
        Assert.IsNotNull(slashAction, "Should have UseSlash action.");
        Assert.IsNotNull(slashAction.TargetConstraints, "TargetConstraints should not be null.");
        Assert.AreEqual(1, slashAction.TargetConstraints.MaxTargets, "Fang Tian Hua Ji should not increase targets when player has multiple hand cards.");
    }

    /// <summary>
    /// Tests that EquipResolver loads Fang Tian Hua Ji skill when equipping the weapon.
    /// Input: 4-player game, player equips Fang Tian Hua Ji via EquipResolver.
    /// Expected: Skill is loaded and active for the player.
    /// </summary>
    [TestMethod]
    public void EquipResolverLoadsFangTianHuaJiSkillWhenEquipping()
    {
        // Arrange
        var game = CreateDefaultGame(4);
        var player = game.Players[0];
        var fangTianHuaJi = CreateFangTianHuaJiCard();
        
        // Add card to hand
        if (player.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(fangTianHuaJi);
        }

        // Setup equipment skill system
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("fang_tian_hua_ji", new FangTianHuaJiSkillFactory());

        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.LoadSkillsForPlayer(game, player);

        var cardMoveService = new BasicCardMoveService();
        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var ruleService = new RuleService(modifierProvider: modifierProvider);
        var stack = new BasicResolutionStack();
        var context = new ResolutionContext(
            game,
            player,
            null,
            new ChoiceResult("test", player.Seat, null, new[] { fangTianHuaJi.Id }, null, null),
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: null,
            IntermediateResults: null,
            EventBus: null,
            LogCollector: null,
            SkillManager: skillManager,
            EquipmentSkillRegistry: equipmentSkillRegistry,
            JudgementService: null
        );

        var resolver = new EquipResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsTrue(player.EquipmentZone.Cards.Contains(fangTianHuaJi));

        // Verify skill is active by checking if it modifies the attack distance
        var modifiers = modifierProvider.GetModifiersFor(game, player);
        var attackDistance = 1;
        foreach (var modifier in modifiers)
        {
            var modified = modifier.ModifyAttackDistance(attackDistance, game, player, game.Players[1]);
            if (modified.HasValue)
            {
                attackDistance = modified.Value;
            }
        }
        Assert.AreEqual(4, attackDistance, "Fang Tian Hua Ji skill should set attack distance to 4.");
    }

    #endregion
}
