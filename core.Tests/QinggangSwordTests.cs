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
public sealed class QinggangSwordTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Card CreateQinggangSwordCard(int id = 1, string definitionId = "qinggang_sword", string name = "青釭剑")
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

    #region Equipment Skill Registry Tests

    /// <summary>
    /// Tests that EquipmentSkillRegistry can register and retrieve equipment skills by CardSubType.
    /// Input: Empty registry, QinggangSwordSkillFactory, CardSubType.Weapon.
    /// Expected: After registration, GetSkillForEquipmentBySubType returns a skill with correct Id and SkillType.Locked.
    /// </summary>
    [TestMethod]
    public void EquipmentSkillRegistryRegisterEquipmentSkillBySubTypeCanRetrieveSkill()
    {
        // Arrange
        var registry = new EquipmentSkillRegistry();
        var factory = new QinggangSwordSkillFactory();

        // Act
        registry.RegisterEquipmentSkillBySubType(CardSubType.Weapon, factory);
        var skill = registry.GetSkillForEquipmentBySubType(CardSubType.Weapon);

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("qinggang_sword", skill.Id);
        Assert.AreEqual(SkillType.Locked, skill.Type);
    }

    /// <summary>
    /// Tests that EquipmentSkillRegistry prevents duplicate equipment skill registrations by CardSubType.
    /// Input: Registry with CardSubType.Weapon already registered, attempting to register same subtype again.
    /// Expected: ArgumentException is thrown when trying to register duplicate card subtype.
    /// </summary>
    [TestMethod]
    public void EquipmentSkillRegistryRegisterEquipmentSkillBySubTypeWithDuplicateSubTypeThrowsException()
    {
        // Arrange
        var registry = new EquipmentSkillRegistry();
        var factory1 = new QinggangSwordSkillFactory();
        var factory2 = new QinggangSwordSkillFactory();

        // Act
        registry.RegisterEquipmentSkillBySubType(CardSubType.Weapon, factory1);

        // Assert
        Assert.ThrowsException<ArgumentException>(() =>
            registry.RegisterEquipmentSkillBySubType(CardSubType.Weapon, factory2));
    }

    /// <summary>
    /// Tests that EquipmentSkillRegistry returns null for unregistered card subtypes.
    /// Input: Empty registry, querying for CardSubType.Weapon.
    /// Expected: GetSkillForEquipmentBySubType returns null when card subtype is not registered.
    /// </summary>
    [TestMethod]
    public void EquipmentSkillRegistryGetSkillForEquipmentBySubTypeWithUnregisteredSubTypeReturnsNull()
    {
        // Arrange
        var registry = new EquipmentSkillRegistry();

        // Act
        var skill = registry.GetSkillForEquipmentBySubType(CardSubType.Weapon);

        // Assert
        Assert.IsNull(skill);
    }

    #endregion

    #region Equip Resolver Tests

    /// <summary>
    /// Tests that EquipResolver successfully moves an equipment card from hand to equipment zone.
    /// Input: 2-player game, player has qinggang sword card in hand, ChoiceResult selecting the card.
    /// Expected: Resolution succeeds, card is removed from hand zone and added to equipment zone.
    /// </summary>
    [TestMethod]
    public void EquipResolverEquipQinggangSwordMovesCardToEquipmentZone()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        var qinggangSword = CreateQinggangSwordCard();
        
        // Add card to hand
        if (player.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(qinggangSword);
        }

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();
        var context = new ResolutionContext(
            game,
            player,
            null,
            new ChoiceResult("test", player.Seat, null, new[] { qinggangSword.Id }, null, null),
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
        Assert.IsFalse(player.HandZone.Cards.Contains(qinggangSword));
        Assert.IsTrue(player.EquipmentZone.Cards.Contains(qinggangSword));
    }

    /// <summary>
    /// Tests that EquipResolver replaces existing equipment of the same type when equipping new equipment.
    /// Input: 2-player game, player has old weapon in equipment zone, new weapon in hand.
    /// Expected: Resolution succeeds, old weapon is moved to discard pile, new weapon is in equipment zone.
    /// </summary>
    [TestMethod]
    public void EquipResolverEquipQinggangSwordWhenAlreadyEquippedReplacesOldEquipment()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        var oldWeapon = CreateQinggangSwordCard(1, "old_weapon", "旧武器");
        var newWeapon = CreateQinggangSwordCard(2, "new_weapon", "新武器");
        
        // Equip old weapon
        if (player.EquipmentZone is Zone equipmentZone)
        {
            equipmentZone.MutableCards.Add(oldWeapon);
        }

        // Add new weapon to hand
        if (player.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(newWeapon);
        }

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();
        var context = new ResolutionContext(
            game,
            player,
            null,
            new ChoiceResult("test", player.Seat, null, new[] { newWeapon.Id }, null, null),
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
        Assert.IsFalse(player.EquipmentZone.Cards.Contains(oldWeapon));
        Assert.IsTrue(player.EquipmentZone.Cards.Contains(newWeapon));
        Assert.IsTrue(game.DiscardPile.Cards.Contains(oldWeapon));
    }

    #endregion

    #region Qinggang Sword Skill Tests

    /// <summary>
    /// Tests that QinggangSwordSkill increases attack distance by 1 when active.
    /// Input: 2-player game, attacker and defender, active qinggang sword skill.
    /// Expected: ModifyAttackDistance returns 2 (1 + 1), making it possible to attack from farther away.
    /// </summary>
    [TestMethod]
    public void QinggangSwordSkillModifyAttackDistanceIncreasesDistanceByOne()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var defender = game.Players[1];
        var skill = new QinggangSwordSkill();

        // Act
        var modified = skill.ModifyAttackDistance(1, game, attacker, defender);

        // Assert
        Assert.IsNotNull(modified);
        Assert.AreEqual(2, modified.Value);
    }

    /// <summary>
    /// Tests that QinggangSwordSkill does not modify distance when the owner (attacker) is not active.
    /// Input: 2-player game, attacker is dead (IsAlive = false), qinggang sword skill.
    /// Expected: ModifyAttackDistance returns null (no modification) when owner is not active.
    /// </summary>
    [TestMethod]
    public void QinggangSwordSkillModifyAttackDistanceWhenOwnerIsNotActiveReturnsNull()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var defender = game.Players[1];
        attacker.IsAlive = false; // Owner is not active
        var skill = new QinggangSwordSkill();

        // Act
        var modified = skill.ModifyAttackDistance(1, game, attacker, defender);

        // Assert
        Assert.IsNull(modified);
    }

    #endregion

    #region Armor Ignore Tests

    /// <summary>
    /// Tests that QinggangSwordSkill ignores armor when using Slash.
    /// Input: 2-player game, attacker has qinggang sword, uses Slash against defender with armor.
    /// Expected: ShouldIgnoreArmor returns true for Slash cards when skill is active.
    /// </summary>
    [TestMethod]
    public void QinggangSwordSkillShouldIgnoreArmorReturnsTrueForSlash()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var defender = game.Players[1];
        var slash = new Card
        {
            Id = 1,
            DefinitionId = "slash",
            Name = "杀",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = Suit.Spade,
            Rank = 5
        };
        var skill = new QinggangSwordSkill();
        var effectContext = new CardEffectContext(game, slash, attacker, defender);

        // Act
        var shouldIgnore = skill.ShouldIgnoreArmor(effectContext);

        // Assert
        Assert.IsTrue(shouldIgnore);
    }

    /// <summary>
    /// Tests that QinggangSwordSkill does not ignore armor for non-Slash cards.
    /// Input: 2-player game, attacker has qinggang sword, uses Peach card.
    /// Expected: ShouldIgnoreArmor returns false for non-Slash cards.
    /// </summary>
    [TestMethod]
    public void QinggangSwordSkillShouldIgnoreArmorReturnsFalseForNonSlash()
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
        var skill = new QinggangSwordSkill();
        var effectContext = new CardEffectContext(game, peach, attacker, defender);

        // Act
        var shouldIgnore = skill.ShouldIgnoreArmor(effectContext);

        // Assert
        Assert.IsFalse(shouldIgnore);
    }

    /// <summary>
    /// Tests that QinggangSwordSkill does not ignore armor when the owner is not active.
    /// Input: 2-player game, attacker is dead (IsAlive = false), qinggang sword skill.
    /// Expected: ShouldIgnoreArmor returns false when owner is not active.
    /// </summary>
    [TestMethod]
    public void QinggangSwordSkillShouldIgnoreArmorWhenOwnerIsNotActiveReturnsFalse()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var defender = game.Players[1];
        attacker.IsAlive = false; // Owner is not active
        var slash = new Card
        {
            Id = 1,
            DefinitionId = "slash",
            Name = "杀",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = Suit.Spade,
            Rank = 5
        };
        var skill = new QinggangSwordSkill();
        var effectContext = new CardEffectContext(game, slash, attacker, defender);

        // Act
        var shouldIgnore = skill.ShouldIgnoreArmor(effectContext);

        // Assert
        Assert.IsFalse(shouldIgnore);
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
        var renwangShield = new Card
        {
            Id = 2,
            DefinitionId = "renwang_shield",
            Name = "仁王盾",
            CardType = CardType.Equip,
            CardSubType = CardSubType.Armor,
            Suit = Suit.Spade,
            Rank = 5
        };
        if (defender.EquipmentZone is Zone defenderEquipmentZone)
        {
            defenderEquipmentZone.MutableCards.Add(renwangShield);
        }

        // Equip qinggang sword to attacker
        var qinggangSword = CreateQinggangSwordCard();
        if (attacker.EquipmentZone is Zone attackerEquipmentZone)
        {
            attackerEquipmentZone.MutableCards.Add(qinggangSword);
        }

        // Create black Slash card in attacker's hand
        var blackSlash = new Card
        {
            Id = 1,
            DefinitionId = "slash",
            Name = "杀",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = Suit.Spade, // Black suit
            Rank = 5
        };
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

    /// <summary>
    /// Tests that SlashResolver invalidates black Slash on Renwang Shield when attacker does not have Qinggang Sword.
    /// Input: 2-player game, defender has renwang shield, attacker has no qinggang sword, attacker uses black Slash.
    /// Expected: SlashResolver returns success without creating response window (effect invalidated).
    /// </summary>
    [TestMethod]
    public void SlashResolverWithoutQinggangSwordCannotIgnoreRenwangShield()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var attacker = game.Players[0];
        var defender = game.Players[1];
        
        // Equip renwang shield to defender
        var renwangShield = new Card
        {
            Id = 2,
            DefinitionId = "renwang_shield",
            Name = "仁王盾",
            CardType = CardType.Equip,
            CardSubType = CardSubType.Armor,
            Suit = Suit.Spade,
            Rank = 5
        };
        if (defender.EquipmentZone is Zone defenderEquipmentZone)
        {
            defenderEquipmentZone.MutableCards.Add(renwangShield);
        }

        // Attacker has no qinggang sword

        // Create black Slash card in attacker's hand
        var blackSlash = new Card
        {
            Id = 1,
            DefinitionId = "slash",
            Name = "杀",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = Suit.Spade, // Black suit
            Rank = 5
        };
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
        skillManager.LoadSkillsForPlayer(game, attacker);
        skillManager.LoadSkillsForPlayer(game, defender);
        
        // Add renwang shield skill to defender only
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

    #endregion

    #region Range Rule Service with Equipment Tests

    /// <summary>
    /// Tests that RangeRuleService correctly applies qinggang sword skill to increase attack distance.
    /// Input: 4-player game, attacker and defender with seat distance = 2, attacker has qinggang sword equipped and skill active.
    /// Expected: Base seat distance = 2, base attack distance = 1, but with qinggang sword attack distance = 2,
    /// so IsWithinAttackRange returns true (because 2 <= 2, so within range).
    /// </summary>
    [TestMethod]
    public void RangeRuleServiceWithQinggangSwordIncreasesAttackRange()
    {
        // Arrange
        var game = CreateDefaultGame(4);
        var attacker = game.Players[0];
        var defender = game.Players[2]; // Player 2 is at distance 2 from player 0 in 4-player game
        
        // Equip qinggang sword to attacker
        var qinggangSword = CreateQinggangSwordCard();
        if (attacker.EquipmentZone is Zone equipmentZone)
        {
            equipmentZone.MutableCards.Add(qinggangSword);
        }

        // Setup skill manager and equipment skill registry
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        // Register skill by CardSubType so all weapon cards share the same skill
        equipmentSkillRegistry.RegisterEquipmentSkillBySubType(CardSubType.Weapon, new QinggangSwordSkillFactory());
        
        var skillManager = new SkillManager(skillRegistry, eventBus);
        
        // Load player skills first (if any)
        skillManager.LoadSkillsForPlayer(game, attacker);
        
        // Add qinggang sword skill to attacker using AddEquipmentSkill
        // EquipResolver will automatically find the skill by CardSubType
        var qinggangSwordSkill = equipmentSkillRegistry.GetSkillForEquipmentBySubType(CardSubType.Weapon);
        if (qinggangSwordSkill is not null)
        {
            skillManager.AddEquipmentSkill(game, attacker, qinggangSwordSkill);
        }

        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var rangeRuleService = new RangeRuleService(modifierProvider);

        // Act
        var seatDistance = rangeRuleService.GetSeatDistance(game, attacker, defender);
        var attackDistance = rangeRuleService.GetAttackDistance(game, attacker, defender);
        var isWithinRange = rangeRuleService.IsWithinAttackRange(game, attacker, defender);

        // Assert
        // Base seat distance should be 2 (non-adjacent players in 4-player game)
        Assert.AreEqual(2, seatDistance);
        // Base attack distance should be 1, but with qinggang sword it becomes 2
        Assert.AreEqual(2, attackDistance);
        // With qinggang sword, attack distance is increased to 2, so 2 <= 2 is true
        Assert.IsTrue(isWithinRange);
    }

    /// <summary>
    /// Tests that RangeRuleService allows normal attack range calculation when no weapon is present.
    /// Input: 4-player game, attacker and defender with seat distance = 2, attacker has no equipment.
    /// Expected: IsWithinAttackRange returns false for non-adjacent players (seat distance 2 > attack distance 1).
    /// </summary>
    [TestMethod]
    public void RangeRuleServiceWithoutQinggangSwordAllowsNormalAttack()
    {
        // Arrange
        var game = CreateDefaultGame(4);
        var attacker = game.Players[0];
        var defender = game.Players[2]; // Player 2 is at distance 2 from player 0 in 4-player game
        
        // No equipment on attacker

        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var skillManager = new SkillManager(skillRegistry, eventBus);
        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var rangeRuleService = new RangeRuleService(modifierProvider);

        // Act
        var seatDistance = rangeRuleService.GetSeatDistance(game, attacker, defender);
        var attackDistance = rangeRuleService.GetAttackDistance(game, attacker, defender);
        var isWithinRange = rangeRuleService.IsWithinAttackRange(game, attacker, defender);

        // Assert
        // Base seat distance should be 2
        Assert.AreEqual(2, seatDistance);
        // Base attack distance should be 1
        Assert.AreEqual(1, attackDistance);
        // Non-adjacent players should not be within attack range without weapon
        Assert.IsFalse(isWithinRange);
    }

    #endregion
}
