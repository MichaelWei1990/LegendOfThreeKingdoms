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
public sealed class RenwangShieldTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Card CreateRenwangShieldCard(int id = 1, string definitionId = "renwang_shield", string name = "仁王盾")
    {
        return new Card
        {
            Id = id,
            DefinitionId = definitionId,
            Name = name,
            CardType = CardType.Equip,
            CardSubType = CardSubType.Armor,
            Suit = Suit.Spade,
            Rank = 5
        };
    }

    private static Card CreateSlashCard(int id, Suit suit)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"slash_{id}",
            Name = "杀",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = suit,
            Rank = 5
        };
    }

    #region Equipment Skill Registry Tests

    /// <summary>
    /// Tests that EquipmentSkillRegistry can register and retrieve equipment skills by CardSubType.
    /// Input: Empty registry, RenwangShieldSkillFactory, CardSubType.Armor.
    /// Expected: After registration, GetSkillForEquipmentBySubType returns a skill with correct Id and SkillType.Locked.
    /// </summary>
    [TestMethod]
    public void EquipmentSkillRegistryRegisterEquipmentSkillBySubTypeCanRetrieveSkill()
    {
        // Arrange
        var registry = new EquipmentSkillRegistry();
        var factory = new RenwangShieldSkillFactory();

        // Act
        registry.RegisterEquipmentSkillBySubType(CardSubType.Armor, factory);
        var skill = registry.GetSkillForEquipmentBySubType(CardSubType.Armor);

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("renwang_shield", skill.Id);
        Assert.AreEqual(SkillType.Locked, skill.Type);
    }

    /// <summary>
    /// Tests that EquipmentSkillRegistry prevents duplicate equipment skill registrations by CardSubType.
    /// Input: Registry with CardSubType.Armor already registered, attempting to register same subtype again.
    /// Expected: ArgumentException is thrown when trying to register duplicate card subtype.
    /// </summary>
    [TestMethod]
    public void EquipmentSkillRegistryRegisterEquipmentSkillBySubTypeWithDuplicateSubTypeThrowsException()
    {
        // Arrange
        var registry = new EquipmentSkillRegistry();
        var factory1 = new RenwangShieldSkillFactory();
        var factory2 = new RenwangShieldSkillFactory();

        // Act
        registry.RegisterEquipmentSkillBySubType(CardSubType.Armor, factory1);

        // Assert
        Assert.ThrowsException<ArgumentException>(() =>
            registry.RegisterEquipmentSkillBySubType(CardSubType.Armor, factory2));
    }

    /// <summary>
    /// Tests that EquipmentSkillRegistry returns null for unregistered card subtypes.
    /// Input: Empty registry, querying for CardSubType.Armor.
    /// Expected: GetSkillForEquipmentBySubType returns null when card subtype is not registered.
    /// </summary>
    [TestMethod]
    public void EquipmentSkillRegistryGetSkillForEquipmentBySubTypeWithUnregisteredSubTypeReturnsNull()
    {
        // Arrange
        var registry = new EquipmentSkillRegistry();

        // Act
        var skill = registry.GetSkillForEquipmentBySubType(CardSubType.Armor);

        // Assert
        Assert.IsNull(skill);
    }

    #endregion

    #region Equip Resolver Tests

    /// <summary>
    /// Tests that EquipResolver successfully moves an equipment card from hand to equipment zone.
    /// Input: 2-player game, player has renwang shield card in hand, ChoiceResult selecting the card.
    /// Expected: Resolution succeeds, card is removed from hand zone and added to equipment zone.
    /// </summary>
    [TestMethod]
    public void EquipResolverEquipRenwangShieldMovesCardToEquipmentZone()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        var renwangShield = CreateRenwangShieldCard();
        
        // Add card to hand
        if (player.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(renwangShield);
        }

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();
        var context = new ResolutionContext(
            game,
            player,
            null,
            new ChoiceResult("test", player.Seat, null, new[] { renwangShield.Id }, null, null),
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
        Assert.IsFalse(player.HandZone.Cards.Contains(renwangShield));
        Assert.IsTrue(player.EquipmentZone.Cards.Contains(renwangShield));
    }

    /// <summary>
    /// Tests that EquipResolver replaces existing armor when equipping a new one.
    /// Input: 2-player game, player has old armor equipped, new renwang shield in hand.
    /// Expected: Resolution succeeds, old armor is moved to discard pile, new armor is equipped.
    /// </summary>
    [TestMethod]
    public void EquipResolverEquipRenwangShieldReplacesExistingArmor()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        var oldArmor = CreateRenwangShieldCard(1, "old_armor", "旧防具");
        var newShield = CreateRenwangShieldCard(2, "renwang_shield", "仁王盾");
        
        // Add old armor to equipment zone
        if (player.EquipmentZone is Zone equipmentZone)
        {
            equipmentZone.MutableCards.Add(oldArmor);
        }
        
        // Add new shield to hand
        if (player.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(newShield);
        }

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();
        var context = new ResolutionContext(
            game,
            player,
            null,
            new ChoiceResult("test", player.Seat, null, new[] { newShield.Id }, null, null),
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
        Assert.IsFalse(player.EquipmentZone.Cards.Contains(oldArmor));
        Assert.IsTrue(player.EquipmentZone.Cards.Contains(newShield));
        Assert.IsTrue(game.DiscardPile.Cards.Contains(oldArmor));
    }

    #endregion

    #region Renwang Shield Skill Tests

    /// <summary>
    /// Tests that RenwangShieldSkill makes black Slash ineffective on the owner.
    /// Input: 2-player game, defender has renwang shield, attacker uses black Slash (Spade).
    /// Expected: IsEffective returns false with veto reason when black Slash targets the owner.
    /// </summary>
    [TestMethod]
    public void RenwangShieldSkillIsEffectiveWithBlackSlashReturnsFalse()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var defender = game.Players[1];
        var blackSlash = CreateSlashCard(1, Suit.Spade); // Black suit
        var skill = new RenwangShieldSkill();
        var effectContext = new CardEffectContext(game, blackSlash, attacker, defender);

        // Act
        var isEffective = skill.IsEffective(effectContext, out var reason);

        // Assert
        Assert.IsFalse(isEffective);
        Assert.IsNotNull(reason);
        Assert.AreEqual("RenwangShield", reason.Source);
    }

    /// <summary>
    /// Tests that RenwangShieldSkill allows red Slash to be effective on the owner.
    /// Input: 2-player game, defender has renwang shield, attacker uses red Slash (Heart).
    /// Expected: IsEffective returns true (no veto) when red Slash targets the owner.
    /// </summary>
    [TestMethod]
    public void RenwangShieldSkillIsEffectiveWithRedSlashReturnsTrue()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var defender = game.Players[1];
        var redSlash = CreateSlashCard(1, Suit.Heart); // Red suit
        var skill = new RenwangShieldSkill();
        var effectContext = new CardEffectContext(game, redSlash, attacker, defender);

        // Act
        var isEffective = skill.IsEffective(effectContext, out var reason);

        // Assert
        Assert.IsTrue(isEffective);
        Assert.IsNull(reason);
    }

    /// <summary>
    /// Tests that RenwangShieldSkill allows non-Slash cards to be effective.
    /// Input: 2-player game, defender has renwang shield, attacker uses Peach card.
    /// Expected: IsEffective returns true (no veto) for non-Slash cards.
    /// </summary>
    [TestMethod]
    public void RenwangShieldSkillIsEffectiveWithNonSlashCardReturnsTrue()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var defender = game.Players[1];
        var peach = new Card
        {
            Id = 1,
            DefinitionId = "peach",
            Name = "桃",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Peach,
            Suit = Suit.Heart,
            Rank = 5
        };
        var skill = new RenwangShieldSkill();
        var effectContext = new CardEffectContext(game, peach, attacker, defender);

        // Act
        var isEffective = skill.IsEffective(effectContext, out var reason);

        // Assert
        Assert.IsTrue(isEffective);
        Assert.IsNull(reason);
    }

    /// <summary>
    /// Tests that RenwangShieldSkill makes black Slash ineffective for both black suits (Spade and Club).
    /// Input: 2-player game, defender has renwang shield, attacker uses Slash with Spade and Club suits.
    /// Expected: IsEffective returns false for both Spade and Club suits.
    /// </summary>
    [TestMethod]
    public void RenwangShieldSkillIsEffectiveWithBothBlackSuitsReturnsFalse()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var defender = game.Players[1];
        var spadeSlash = CreateSlashCard(1, Suit.Spade);
        var clubSlash = CreateSlashCard(2, Suit.Club);
        var skill = new RenwangShieldSkill();
        var spadeContext = new CardEffectContext(game, spadeSlash, attacker, defender);
        var clubContext = new CardEffectContext(game, clubSlash, attacker, defender);

        // Act
        var spadeEffective = skill.IsEffective(spadeContext, out var spadeReason);
        var clubEffective = skill.IsEffective(clubContext, out var clubReason);

        // Assert
        Assert.IsFalse(spadeEffective);
        Assert.IsNotNull(spadeReason);
        Assert.IsFalse(clubEffective);
        Assert.IsNotNull(clubReason);
    }

    #endregion

    #region Card Usage Rule Service with Equipment Tests

    /// <summary>
    /// Tests that CardUsageRuleService allows defender with Renwang Shield as legal target for black Slash.
    /// Input: 2-player game, defender has renwang shield equipped, attacker uses black Slash.
    /// Expected: GetLegalTargets includes defender as a legal target (target selection is allowed, but damage will be prevented).
    /// </summary>
    [TestMethod]
    public void CardUsageRuleServiceWithRenwangShieldAllowsDefenderForBlackSlash()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var defender = game.Players[1];
        
        // Equip renwang shield to defender
        var renwangShield = CreateRenwangShieldCard();
        if (defender.EquipmentZone is Zone equipmentZone)
        {
            equipmentZone.MutableCards.Add(renwangShield);
        }

        // Setup skill manager and equipment skill registry
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkillBySubType(CardSubType.Armor, new RenwangShieldSkillFactory());
        
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.LoadSkillsForPlayer(game, defender);
        
        // Add renwang shield skill to defender
        var renwangShieldSkill = equipmentSkillRegistry.GetSkillForEquipmentBySubType(CardSubType.Armor);
        if (renwangShieldSkill is not null)
        {
            skillManager.AddEquipmentSkill(game, defender, renwangShieldSkill);
        }

        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var rangeRuleService = new RangeRuleService(modifierProvider);
        var cardUsageRuleService = new CardUsageRuleService(
            new PhaseRuleService(),
            rangeRuleService,
            new LimitRuleService(),
            modifierProvider,
            skillManager);

        // Create black Slash card
        var blackSlash = CreateSlashCard(1, Suit.Spade);
        var usageContext = new CardUsageContext(
            game,
            attacker,
            blackSlash,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 0);

        // Act
        var legalTargets = cardUsageRuleService.GetLegalTargets(usageContext);

        // Assert
        // Black Slash can still select defender as target (damage will be prevented during resolution)
        Assert.IsTrue(legalTargets.HasAny);
        Assert.AreEqual(1, legalTargets.Items.Count);
        Assert.AreEqual(defender.Seat, legalTargets.Items[0].Seat);
    }

    /// <summary>
    /// Tests that CardUsageRuleService allows defender with Renwang Shield as legal target for red Slash.
    /// Input: 2-player game, defender has renwang shield equipped, attacker uses red Slash.
    /// Expected: GetLegalTargets includes defender as a legal target (renwang shield only blocks black Slash).
    /// </summary>
    [TestMethod]
    public void CardUsageRuleServiceWithRenwangShieldAllowsDefenderForRedSlash()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var defender = game.Players[1];
        
        // Equip renwang shield to defender
        var renwangShield = CreateRenwangShieldCard();
        if (defender.EquipmentZone is Zone equipmentZone)
        {
            equipmentZone.MutableCards.Add(renwangShield);
        }

        // Setup skill manager and equipment skill registry
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkillBySubType(CardSubType.Armor, new RenwangShieldSkillFactory());
        
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.LoadSkillsForPlayer(game, defender);
        
        // Add renwang shield skill to defender
        var renwangShieldSkill = equipmentSkillRegistry.GetSkillForEquipmentBySubType(CardSubType.Armor);
        if (renwangShieldSkill is not null)
        {
            skillManager.AddEquipmentSkill(game, defender, renwangShieldSkill);
        }

        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var rangeRuleService = new RangeRuleService(modifierProvider);
        var cardUsageRuleService = new CardUsageRuleService(
            new PhaseRuleService(),
            rangeRuleService,
            new LimitRuleService(),
            modifierProvider,
            skillManager);

        // Create red Slash card
        var redSlash = CreateSlashCard(1, Suit.Heart);
        var usageContext = new CardUsageContext(
            game,
            attacker,
            redSlash,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 0);

        // Act
        var legalTargets = cardUsageRuleService.GetLegalTargets(usageContext);

        // Assert
        Assert.IsTrue(legalTargets.HasAny);
        Assert.AreEqual(1, legalTargets.Items.Count);
        Assert.AreEqual(defender.Seat, legalTargets.Items[0].Seat);
    }

    /// <summary>
    /// Tests that CardUsageRuleService allows normal target selection when no renwang shield is equipped.
    /// Input: 2-player game, defender has no equipment, attacker uses black Slash.
    /// Expected: GetLegalTargets includes defender as a legal target (no filtering applied).
    /// </summary>
    [TestMethod]
    public void CardUsageRuleServiceWithoutRenwangShieldAllowsNormalTargetSelection()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var defender = game.Players[1];
        
        // No equipment on defender

        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var skillManager = new SkillManager(skillRegistry, eventBus);
        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var rangeRuleService = new RangeRuleService(modifierProvider);
        var cardUsageRuleService = new CardUsageRuleService(
            new PhaseRuleService(),
            rangeRuleService,
            new LimitRuleService(),
            modifierProvider,
            skillManager);

        // Create black Slash card
        var blackSlash = CreateSlashCard(1, Suit.Spade);
        var usageContext = new CardUsageContext(
            game,
            attacker,
            blackSlash,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 0);

        // Act
        var legalTargets = cardUsageRuleService.GetLegalTargets(usageContext);

        // Assert
        Assert.IsTrue(legalTargets.HasAny);
        Assert.AreEqual(1, legalTargets.Items.Count);
        Assert.AreEqual(defender.Seat, legalTargets.Items[0].Seat);
    }

    /// <summary>
    /// Tests that CardUsageRuleService correctly handles multiple players with renwang shield.
    /// Input: 3-player game, players 1 and 2 have renwang shield equipped, attacker uses black Slash.
    /// Expected: GetLegalTargets includes both defenders as legal targets (damage will be prevented during resolution).
    /// </summary>
    [TestMethod]
    public void CardUsageRuleServiceWithMultipleRenwangShieldsAllowsAllDefenders()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        var attacker = game.Players[0];
        var defender1 = game.Players[1];
        var defender2 = game.Players[2];
        
        // Equip renwang shield to both defenders
        var shield1 = CreateRenwangShieldCard(1, "renwang_shield_1", "仁王盾");
        var shield2 = CreateRenwangShieldCard(2, "renwang_shield_2", "仁王盾");
        if (defender1.EquipmentZone is Zone equipmentZone1)
        {
            equipmentZone1.MutableCards.Add(shield1);
        }
        if (defender2.EquipmentZone is Zone equipmentZone2)
        {
            equipmentZone2.MutableCards.Add(shield2);
        }

        // Setup skill manager and equipment skill registry
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkillBySubType(CardSubType.Armor, new RenwangShieldSkillFactory());
        
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.LoadSkillsForPlayer(game, defender1);
        skillManager.LoadSkillsForPlayer(game, defender2);
        
        // Add renwang shield skill to both defenders
        var renwangShieldSkill = equipmentSkillRegistry.GetSkillForEquipmentBySubType(CardSubType.Armor);
        if (renwangShieldSkill is not null)
        {
            skillManager.AddEquipmentSkill(game, defender1, renwangShieldSkill);
            skillManager.AddEquipmentSkill(game, defender2, renwangShieldSkill);
        }

        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var rangeRuleService = new RangeRuleService(modifierProvider);
        var cardUsageRuleService = new CardUsageRuleService(
            new PhaseRuleService(),
            rangeRuleService,
            new LimitRuleService(),
            modifierProvider,
            skillManager);

        // Create black Slash card
        var blackSlash = CreateSlashCard(1, Suit.Spade);
        var usageContext = new CardUsageContext(
            game,
            attacker,
            blackSlash,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 0);

        // Act
        var legalTargets = cardUsageRuleService.GetLegalTargets(usageContext);

        // Assert
        // Black Slash can still select both defenders as targets (damage will be prevented during resolution)
        Assert.IsTrue(legalTargets.HasAny);
        Assert.AreEqual(2, legalTargets.Items.Count);
    }

    #endregion

    #region Slash Resolution Integration Tests

    /// <summary>
    /// Tests that SlashResolver invalidates black Slash on Renwang Shield before response window.
    /// Input: 2-player game, defender has renwang shield equipped, attacker uses black Slash.
    /// Expected: SlashResolver returns success without creating response window (effect invalidated).
    /// </summary>
    [TestMethod]
    public void SlashResolverWithRenwangShieldInvalidatesBlackSlashBeforeResponseWindow()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var attacker = game.Players[0];
        var defender = game.Players[1];
        
        // Equip renwang shield to defender
        var renwangShield = CreateRenwangShieldCard();
        if (defender.EquipmentZone is Zone equipmentZone)
        {
            equipmentZone.MutableCards.Add(renwangShield);
        }

        // Create black Slash card in attacker's hand
        var blackSlash = CreateSlashCard(1, Suit.Spade);
        if (attacker.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(blackSlash);
        }

        // Setup skill manager and equipment skill registry
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkillBySubType(CardSubType.Armor, new RenwangShieldSkillFactory());
        
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.LoadSkillsForPlayer(game, defender);
        
        // Add renwang shield skill to defender
        var renwangShieldSkill = equipmentSkillRegistry.GetSkillForEquipmentBySubType(CardSubType.Armor);
        if (renwangShieldSkill is not null)
        {
            skillManager.AddEquipmentSkill(game, defender, renwangShieldSkill);
        }

        var stack = new BasicResolutionStack();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        
        var action = new ActionDescriptor(
            ActionId: "UseSlash",
            DisplayKey: "action.useSlash",
            RequiresTargets: true,
            TargetConstraints: new TargetConstraints(1, 1, TargetFilterType.Enemies),
            CardCandidates: new[] { blackSlash }
        );

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: attacker.Seat,
            SelectedTargetSeats: new[] { defender.Seat },
            SelectedCardIds: new[] { blackSlash.Id },
            SelectedOptionId: null,
            Confirmed: null
        );

        // Create getPlayerChoice function (should not be called for invalidated effect)
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            Assert.Fail("GetPlayerChoice should not be called when effect is invalidated");
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: new List<int>(),
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var context = new ResolutionContext(
            game,
            attacker,
            action,
            choice,
            stack,
            cardMoveService,
            ruleService,
            GetPlayerChoice: getPlayerChoice,
            SkillManager: skillManager,
            EquipmentSkillRegistry: equipmentSkillRegistry
        );

        var resolver = new SlashResolver();
        var initialStackCount = stack.GetHistory().Count;

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);
        // Stack should be empty (no response window or damage resolver pushed)
        Assert.IsTrue(stack.IsEmpty);
        // No new resolvers should have been pushed
        Assert.AreEqual(initialStackCount, stack.GetHistory().Count);
    }

    /// <summary>
    /// Tests that SlashResolver creates response window for red Slash on Renwang Shield.
    /// Input: 2-player game, defender has renwang shield equipped, attacker uses red Slash.
    /// Expected: SlashResolver creates response window (effect is effective).
    /// </summary>
    [TestMethod]
    public void SlashResolverWithRenwangShieldCreatesResponseWindowForRedSlash()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var attacker = game.Players[0];
        var defender = game.Players[1];
        
        // Equip renwang shield to defender
        var renwangShield = CreateRenwangShieldCard();
        if (defender.EquipmentZone is Zone equipmentZone)
        {
            equipmentZone.MutableCards.Add(renwangShield);
        }

        // Create red Slash card in attacker's hand
        var redSlash = CreateSlashCard(1, Suit.Heart);
        if (attacker.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(redSlash);
        }

        // Setup skill manager and equipment skill registry
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkillBySubType(CardSubType.Armor, new RenwangShieldSkillFactory());
        
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.LoadSkillsForPlayer(game, defender);
        
        // Add renwang shield skill to defender
        var renwangShieldSkill = equipmentSkillRegistry.GetSkillForEquipmentBySubType(CardSubType.Armor);
        if (renwangShieldSkill is not null)
        {
            skillManager.AddEquipmentSkill(game, defender, renwangShieldSkill);
        }

        var stack = new BasicResolutionStack();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        
        var action = new ActionDescriptor(
            ActionId: "UseSlash",
            DisplayKey: "action.useSlash",
            RequiresTargets: true,
            TargetConstraints: new TargetConstraints(1, 1, TargetFilterType.Enemies),
            CardCandidates: new[] { redSlash }
        );

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: attacker.Seat,
            SelectedTargetSeats: new[] { defender.Seat },
            SelectedCardIds: new[] { redSlash.Id },
            SelectedOptionId: null,
            Confirmed: null
        );

        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: new List<int>(),
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var context = new ResolutionContext(
            game,
            attacker,
            action,
            choice,
            stack,
            cardMoveService,
            ruleService,
            GetPlayerChoice: getPlayerChoice,
            SkillManager: skillManager,
            EquipmentSkillRegistry: equipmentSkillRegistry
        );

        var resolver = new SlashResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);
        // Response window should be created (stack should not be empty)
        Assert.IsFalse(stack.IsEmpty);
    }

    /// <summary>
    /// Tests that SlashResolver allows black Slash to be effective when attacker has Qinggang Sword (armor ignored).
    /// Input: 2-player game, defender has renwang shield, attacker has qinggang sword, attacker uses black Slash.
    /// Expected: SlashResolver creates response window (armor ignored, effect is effective).
    /// </summary>
    [TestMethod]
    public void SlashResolverWithQinggangSwordIgnoresRenwangShield()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var attacker = game.Players[0];
        var defender = game.Players[1];
        
        // Equip renwang shield to defender
        var renwangShield = CreateRenwangShieldCard();
        if (defender.EquipmentZone is Zone defenderEquipmentZone)
        {
            defenderEquipmentZone.MutableCards.Add(renwangShield);
        }

        // Equip qinggang sword to attacker
        var qinggangSword = new Card
        {
            Id = 2,
            DefinitionId = "qinggang_sword",
            Name = "青釭剑",
            CardType = CardType.Equip,
            CardSubType = CardSubType.Weapon,
            Suit = Suit.Spade,
            Rank = 5
        };
        if (attacker.EquipmentZone is Zone attackerEquipmentZone)
        {
            attackerEquipmentZone.MutableCards.Add(qinggangSword);
        }

        // Create black Slash card in attacker's hand
        var blackSlash = CreateSlashCard(1, Suit.Spade);
        if (attacker.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(blackSlash);
        }

        // Setup skill manager and equipment skill registry
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkillBySubType(CardSubType.Armor, new RenwangShieldSkillFactory());
        equipmentSkillRegistry.RegisterEquipmentSkillBySubType(CardSubType.Weapon, new QinggangSwordSkillFactory());
        
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.LoadSkillsForPlayer(game, attacker);
        skillManager.LoadSkillsForPlayer(game, defender);
        
        // Add equipment skills
        var renwangShieldSkill = equipmentSkillRegistry.GetSkillForEquipmentBySubType(CardSubType.Armor);
        var qinggangSwordSkill = equipmentSkillRegistry.GetSkillForEquipmentBySubType(CardSubType.Weapon);
        if (renwangShieldSkill is not null)
        {
            skillManager.AddEquipmentSkill(game, defender, renwangShieldSkill);
        }
        if (qinggangSwordSkill is not null)
        {
            skillManager.AddEquipmentSkill(game, attacker, qinggangSwordSkill);
        }

        var stack = new BasicResolutionStack();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        
        var action = new ActionDescriptor(
            ActionId: "UseSlash",
            DisplayKey: "action.useSlash",
            RequiresTargets: true,
            TargetConstraints: new TargetConstraints(1, 1, TargetFilterType.Enemies),
            CardCandidates: new[] { blackSlash }
        );

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: attacker.Seat,
            SelectedTargetSeats: new[] { defender.Seat },
            SelectedCardIds: new[] { blackSlash.Id },
            SelectedOptionId: null,
            Confirmed: null
        );

        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: new List<int>(),
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var context = new ResolutionContext(
            game,
            attacker,
            action,
            choice,
            stack,
            cardMoveService,
            ruleService,
            GetPlayerChoice: getPlayerChoice,
            SkillManager: skillManager,
            EquipmentSkillRegistry: equipmentSkillRegistry
        );

        var resolver = new SlashResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);
        // Response window should be created (armor ignored, effect is effective)
        Assert.IsFalse(stack.IsEmpty);
    }

    #endregion
}

