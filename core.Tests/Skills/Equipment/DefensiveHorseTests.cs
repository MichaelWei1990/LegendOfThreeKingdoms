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
public sealed class DefensiveHorseTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Card CreateDefensiveHorseCard(int id = 1, string definitionId = "dilu", string name = "的卢")
    {
        return new Card
        {
            Id = id,
            DefinitionId = definitionId,
            Name = name,
            CardType = CardType.Equip,
            CardSubType = CardSubType.DefensiveHorse,
            Suit = Suit.Spade,
            Rank = 5
        };
    }

    #region Equipment Skill Registry Tests

    /// <summary>
    /// Tests that EquipmentSkillRegistry can register and retrieve equipment skills by CardSubType.
    /// Input: Empty registry, DefensiveHorseSkillFactory, CardSubType.DefensiveHorse.
    /// Expected: After registration, GetSkillForEquipmentBySubType returns a skill with correct Id and SkillType.Locked.
    /// </summary>
    [TestMethod]
    public void EquipmentSkillRegistryRegisterEquipmentSkillBySubTypeCanRetrieveSkill()
    {
        // Arrange
        var registry = new EquipmentSkillRegistry();
        var factory = new DefensiveHorseSkillFactory();

        // Act
        registry.RegisterEquipmentSkillBySubType(CardSubType.DefensiveHorse, factory);
        var skill = registry.GetSkillForEquipmentBySubType(CardSubType.DefensiveHorse);

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("defensive_horse", skill.Id);
        Assert.AreEqual(SkillType.Locked, skill.Type);
    }

    /// <summary>
    /// Tests that EquipmentSkillRegistry prevents duplicate equipment skill registrations by CardSubType.
    /// Input: Registry with CardSubType.DefensiveHorse already registered, attempting to register same subtype again.
    /// Expected: ArgumentException is thrown when trying to register duplicate card subtype.
    /// </summary>
    [TestMethod]
    public void EquipmentSkillRegistryRegisterEquipmentSkillBySubTypeWithDuplicateSubTypeThrowsException()
    {
        // Arrange
        var registry = new EquipmentSkillRegistry();
        var factory1 = new DefensiveHorseSkillFactory();
        var factory2 = new DefensiveHorseSkillFactory();

        // Act
        registry.RegisterEquipmentSkillBySubType(CardSubType.DefensiveHorse, factory1);

        // Assert
        Assert.ThrowsException<ArgumentException>(() =>
            registry.RegisterEquipmentSkillBySubType(CardSubType.DefensiveHorse, factory2));
    }

    /// <summary>
    /// Tests that EquipmentSkillRegistry returns null for unregistered card subtypes.
    /// Input: Empty registry, querying for CardSubType.DefensiveHorse.
    /// Expected: GetSkillForEquipmentBySubType returns null when card subtype is not registered.
    /// </summary>
    [TestMethod]
    public void EquipmentSkillRegistryGetSkillForEquipmentBySubTypeWithUnregisteredSubTypeReturnsNull()
    {
        // Arrange
        var registry = new EquipmentSkillRegistry();

        // Act
        var skill = registry.GetSkillForEquipmentBySubType(CardSubType.DefensiveHorse);

        // Assert
        Assert.IsNull(skill);
    }

    #endregion

    #region Equip Resolver Tests

    /// <summary>
    /// Tests that EquipResolver successfully moves an equipment card from hand to equipment zone.
    /// Input: 2-player game, player has defensive horse card in hand, ChoiceResult selecting the card.
    /// Expected: Resolution succeeds, card is removed from hand zone and added to equipment zone.
    /// </summary>
    [TestMethod]
    public void EquipResolverEquipDefensiveHorseMovesCardToEquipmentZone()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        var defensiveHorse = CreateDefensiveHorseCard();
        
        // Add card to hand
        if (player.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(defensiveHorse);
        }

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();
        var context = new ResolutionContext(
            game,
            player,
            null,
            new ChoiceResult("test", player.Seat, null, new[] { defensiveHorse.Id }, null, null),
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
        Assert.IsFalse(player.HandZone.Cards.Contains(defensiveHorse));
        Assert.IsTrue(player.EquipmentZone.Cards.Contains(defensiveHorse));
    }

    /// <summary>
    /// Tests that EquipResolver replaces existing equipment of the same type when equipping new equipment.
    /// Input: 2-player game, player has old defensive horse in equipment zone, new defensive horse in hand.
    /// Expected: Resolution succeeds, old horse is moved to discard pile, new horse is in equipment zone.
    /// </summary>
    [TestMethod]
    public void EquipResolverEquipDefensiveHorseWhenAlreadyEquippedReplacesOldEquipment()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        var oldHorse = CreateDefensiveHorseCard(1, "dilu_old", "的卢(旧)");
        var newHorse = CreateDefensiveHorseCard(2, "dilu_new", "的卢(新)");
        
        // Equip old horse
        if (player.EquipmentZone is Zone equipmentZone)
        {
            equipmentZone.MutableCards.Add(oldHorse);
        }

        // Add new horse to hand
        if (player.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(newHorse);
        }

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();
        var context = new ResolutionContext(
            game,
            player,
            null,
            new ChoiceResult("test", player.Seat, null, new[] { newHorse.Id }, null, null),
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
        Assert.IsFalse(player.EquipmentZone.Cards.Contains(oldHorse));
        Assert.IsTrue(player.EquipmentZone.Cards.Contains(newHorse));
        Assert.IsTrue(game.DiscardPile.Cards.Contains(oldHorse));
    }

    #endregion

    #region Defensive Horse Skill Tests

    /// <summary>
    /// Tests that DefensiveHorseSkill increases seat distance by 1 when active.
    /// Input: 3-player game, attacker and defender (adjacent, seat distance = 1), active defensive horse skill.
    /// Expected: ModifySeatDistance returns 2 (1 + 1), making it harder to attack the defender.
    /// </summary>
    [TestMethod]
    public void DefensiveHorseSkillModifySeatDistanceIncreasesDistanceByOne()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        var attacker = game.Players[0];
        var defender = game.Players[1];
        var skill = new DefensiveHorseSkill();

        // Act
        var modified = skill.ModifySeatDistance(1, game, attacker, defender);

        // Assert
        Assert.IsNotNull(modified);
        Assert.AreEqual(2, modified.Value);
    }

    /// <summary>
    /// Tests that DefensiveHorseSkill does not modify distance when the owner (defender) is not active.
    /// Input: 2-player game, defender is dead (IsAlive = false), defensive horse skill.
    /// Expected: ModifySeatDistance returns null (no modification) when owner is not active.
    /// </summary>
    [TestMethod]
    public void DefensiveHorseSkillModifySeatDistanceWhenOwnerIsNotActiveReturnsNull()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var defender = game.Players[1];
        defender.IsAlive = false; // Owner is not active
        var skill = new DefensiveHorseSkill();

        // Act
        var modified = skill.ModifySeatDistance(1, game, attacker, defender);

        // Assert
        Assert.IsNull(modified);
    }

    #endregion

    #region Range Rule Service with Equipment Tests

    /// <summary>
    /// Tests that RangeRuleService correctly applies defensive horse skill to increase attack distance requirement.
    /// Input: 3-player game, adjacent attacker and defender, defender has defensive horse equipped and skill active.
    /// Expected: Base seat distance = 1, base attack distance = 1, but IsWithinAttackRange returns false 
    /// because defensive horse increases effective seat distance to 2 (2 > 1, so out of range).
    /// </summary>
    [TestMethod]
    public void RangeRuleServiceWithDefensiveHorseIncreasesAttackDistanceRequirement()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        var attacker = game.Players[0];
        var defender = game.Players[1];
        
        // Equip defensive horse to defender
        var defensiveHorse = CreateDefensiveHorseCard();
        if (defender.EquipmentZone is Zone equipmentZone)
        {
            equipmentZone.MutableCards.Add(defensiveHorse);
        }

        // Setup skill manager and equipment skill registry
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        // Register skill by CardSubType so all defensive horse cards share the same skill
        equipmentSkillRegistry.RegisterEquipmentSkillBySubType(CardSubType.DefensiveHorse, new DefensiveHorseSkillFactory());
        
        var skillManager = new SkillManager(skillRegistry, eventBus);
        
        // Load player skills first (if any)
        skillManager.LoadSkillsForPlayer(game, defender);
        
        // Add defensive horse skill to defender using AddEquipmentSkill
        // EquipResolver will automatically find the skill by CardSubType
        var defensiveHorseSkill = equipmentSkillRegistry.GetSkillForEquipmentBySubType(CardSubType.DefensiveHorse);
        if (defensiveHorseSkill is not null)
        {
            skillManager.AddEquipmentSkill(game, defender, defensiveHorseSkill);
        }

        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var rangeRuleService = new RangeRuleService(modifierProvider);

        // Act
        var seatDistance = rangeRuleService.GetSeatDistance(game, attacker, defender);
        var attackDistance = rangeRuleService.GetAttackDistance(game, attacker, defender);
        var isWithinRange = rangeRuleService.IsWithinAttackRange(game, attacker, defender);

        // Assert
        // Base seat distance should be 1 (adjacent players)
        Assert.AreEqual(1, seatDistance);
        // Base attack distance should be 1
        Assert.AreEqual(1, attackDistance);
        // With defensive horse, seat distance requirement is increased by 1
        // So seatDistance (1) should NOT be <= attackDistance (1) after modification
        // Actually, the modification happens in IsWithinAttackRange, so let's check that
        // The defensive horse increases the seat distance to 2, so 2 <= 1 is false
        Assert.IsFalse(isWithinRange);
    }

    /// <summary>
    /// Tests that RangeRuleService allows normal attack range calculation when no defensive equipment is present.
    /// Input: 3-player game, adjacent attacker and defender, defender has no equipment.
    /// Expected: IsWithinAttackRange returns true for adjacent players (seat distance 1 <= attack distance 1).
    /// </summary>
    [TestMethod]
    public void RangeRuleServiceWithoutDefensiveHorseAllowsNormalAttack()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        var attacker = game.Players[0];
        var defender = game.Players[1];
        
        // No equipment on defender

        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var skillManager = new SkillManager(skillRegistry, eventBus);
        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var rangeRuleService = new RangeRuleService(modifierProvider);

        // Act
        var isWithinRange = rangeRuleService.IsWithinAttackRange(game, attacker, defender);

        // Assert
        // Adjacent players should be within attack range
        Assert.IsTrue(isWithinRange);
    }

    #endregion
}
