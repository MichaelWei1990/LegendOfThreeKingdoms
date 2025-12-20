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
public sealed class OffensiveHorseTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Card CreateOffensiveHorseCard(int id = 1, string definitionId = "chitu", string name = "赤兔")
    {
        return new Card
        {
            Id = id,
            DefinitionId = definitionId,
            Name = name,
            CardType = CardType.Equip,
            CardSubType = CardSubType.OffensiveHorse,
            Suit = Suit.Spade,
            Rank = 5
        };
    }

    #region Equipment Skill Registry Tests

    /// <summary>
    /// Tests that EquipmentSkillRegistry can register and retrieve equipment skills by CardSubType.
    /// Input: Empty registry, OffensiveHorseSkillFactory, CardSubType.OffensiveHorse.
    /// Expected: After registration, GetSkillForEquipmentBySubType returns a skill with correct Id and SkillType.Locked.
    /// </summary>
    [TestMethod]
    public void EquipmentSkillRegistryRegisterEquipmentSkillBySubTypeCanRetrieveSkill()
    {
        // Arrange
        var registry = new EquipmentSkillRegistry();
        var factory = new OffensiveHorseSkillFactory();

        // Act
        registry.RegisterEquipmentSkillBySubType(CardSubType.OffensiveHorse, factory);
        var skill = registry.GetSkillForEquipmentBySubType(CardSubType.OffensiveHorse);

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("offensive_horse", skill.Id);
        Assert.AreEqual(SkillType.Locked, skill.Type);
    }

    /// <summary>
    /// Tests that EquipmentSkillRegistry prevents duplicate equipment skill registrations by CardSubType.
    /// Input: Registry with CardSubType.OffensiveHorse already registered, attempting to register same subtype again.
    /// Expected: ArgumentException is thrown when trying to register duplicate card subtype.
    /// </summary>
    [TestMethod]
    public void EquipmentSkillRegistryRegisterEquipmentSkillBySubTypeWithDuplicateSubTypeThrowsException()
    {
        // Arrange
        var registry = new EquipmentSkillRegistry();
        var factory1 = new OffensiveHorseSkillFactory();
        var factory2 = new OffensiveHorseSkillFactory();

        // Act
        registry.RegisterEquipmentSkillBySubType(CardSubType.OffensiveHorse, factory1);

        // Assert
        Assert.ThrowsException<ArgumentException>(() =>
            registry.RegisterEquipmentSkillBySubType(CardSubType.OffensiveHorse, factory2));
    }

    /// <summary>
    /// Tests that EquipmentSkillRegistry returns null for unregistered card subtypes.
    /// Input: Empty registry, querying for CardSubType.OffensiveHorse.
    /// Expected: GetSkillForEquipmentBySubType returns null when card subtype is not registered.
    /// </summary>
    [TestMethod]
    public void EquipmentSkillRegistryGetSkillForEquipmentBySubTypeWithUnregisteredSubTypeReturnsNull()
    {
        // Arrange
        var registry = new EquipmentSkillRegistry();

        // Act
        var skill = registry.GetSkillForEquipmentBySubType(CardSubType.OffensiveHorse);

        // Assert
        Assert.IsNull(skill);
    }

    #endregion

    #region Equip Resolver Tests

    /// <summary>
    /// Tests that EquipResolver successfully moves an equipment card from hand to equipment zone.
    /// Input: 2-player game, player has offensive horse card in hand, ChoiceResult selecting the card.
    /// Expected: Resolution succeeds, card is removed from hand zone and added to equipment zone.
    /// </summary>
    [TestMethod]
    public void EquipResolverEquipOffensiveHorseMovesCardToEquipmentZone()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        var offensiveHorse = CreateOffensiveHorseCard();
        
        // Add card to hand
        if (player.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(offensiveHorse);
        }

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();
        var context = new ResolutionContext(
            game,
            player,
            null,
            new ChoiceResult("test", player.Seat, null, new[] { offensiveHorse.Id }, null, null),
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
        Assert.IsFalse(player.HandZone.Cards.Contains(offensiveHorse));
        Assert.IsTrue(player.EquipmentZone.Cards.Contains(offensiveHorse));
    }

    /// <summary>
    /// Tests that EquipResolver replaces existing equipment of the same type when equipping new equipment.
    /// Input: 2-player game, player has old offensive horse in equipment zone, new offensive horse in hand.
    /// Expected: Resolution succeeds, old horse is moved to discard pile, new horse is in equipment zone.
    /// </summary>
    [TestMethod]
    public void EquipResolverEquipOffensiveHorseWhenAlreadyEquippedReplacesOldEquipment()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        var oldHorse = CreateOffensiveHorseCard(1, "chitu_old", "赤兔(旧)");
        var newHorse = CreateOffensiveHorseCard(2, "chitu_new", "赤兔(新)");
        
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

    #region Offensive Horse Skill Tests

    /// <summary>
    /// Tests that OffensiveHorseSkill decreases seat distance by 1 when active.
    /// Input: 3-player game, attacker and defender (seat distance = 2), active offensive horse skill.
    /// Expected: ModifySeatDistance returns 1 (2 - 1), making it easier to attack the defender.
    /// </summary>
    [TestMethod]
    public void OffensiveHorseSkillModifySeatDistanceDecreasesDistanceByOne()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        var attacker = game.Players[0];
        var defender = game.Players[1];
        var skill = new OffensiveHorseSkill();

        // Act
        var modified = skill.ModifySeatDistance(2, game, attacker, defender);

        // Assert
        Assert.IsNotNull(modified);
        Assert.AreEqual(1, modified.Value);
    }

    /// <summary>
    /// Tests that OffensiveHorseSkill does not decrease distance below 1.
    /// Input: 3-player game, attacker and defender (adjacent, seat distance = 1), active offensive horse skill.
    /// Expected: ModifySeatDistance returns 1 (minimum distance, cannot go below 1).
    /// </summary>
    [TestMethod]
    public void OffensiveHorseSkillModifySeatDistanceDoesNotGoBelowOne()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        var attacker = game.Players[0];
        var defender = game.Players[1];
        var skill = new OffensiveHorseSkill();

        // Act
        var modified = skill.ModifySeatDistance(1, game, attacker, defender);

        // Assert
        Assert.IsNotNull(modified);
        Assert.AreEqual(1, modified.Value);
    }

    /// <summary>
    /// Tests that OffensiveHorseSkill does not modify distance when the owner (attacker) is not active.
    /// Input: 2-player game, attacker is dead (IsAlive = false), offensive horse skill.
    /// Expected: ModifySeatDistance returns null (no modification) when owner is not active.
    /// </summary>
    [TestMethod]
    public void OffensiveHorseSkillModifySeatDistanceWhenOwnerIsNotActiveReturnsNull()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var defender = game.Players[1];
        attacker.IsAlive = false; // Owner is not active
        var skill = new OffensiveHorseSkill();

        // Act
        var modified = skill.ModifySeatDistance(2, game, attacker, defender);

        // Assert
        Assert.IsNull(modified);
    }

    #endregion

    #region Range Rule Service with Equipment Tests

    /// <summary>
    /// Tests that RangeRuleService correctly applies offensive horse skill to decrease attack distance requirement.
    /// Input: 4-player game, attacker and defender with seat distance = 2, attacker has offensive horse equipped and skill active.
    /// Expected: Base seat distance = 2, base attack distance = 1, but IsWithinAttackRange returns true 
    /// because offensive horse decreases effective seat distance to 1 (1 <= 1, so within range).
    /// </summary>
    [TestMethod]
    public void RangeRuleServiceWithOffensiveHorseDecreasesAttackDistanceRequirement()
    {
        // Arrange
        var game = CreateDefaultGame(4);
        var attacker = game.Players[0];
        var defender = game.Players[2]; // Player 2 is at distance 2 from player 0 in 4-player game
        
        // Equip offensive horse to attacker
        var offensiveHorse = CreateOffensiveHorseCard();
        if (attacker.EquipmentZone is Zone equipmentZone)
        {
            equipmentZone.MutableCards.Add(offensiveHorse);
        }

        // Setup skill manager and equipment skill registry
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        // Register skill by CardSubType so all offensive horse cards share the same skill
        equipmentSkillRegistry.RegisterEquipmentSkillBySubType(CardSubType.OffensiveHorse, new OffensiveHorseSkillFactory());
        
        var skillManager = new SkillManager(skillRegistry, eventBus);
        
        // Load player skills first (if any)
        skillManager.LoadSkillsForPlayer(game, attacker);
        
        // Add offensive horse skill to attacker using AddEquipmentSkill
        // EquipResolver will automatically find the skill by CardSubType
        var offensiveHorseSkill = equipmentSkillRegistry.GetSkillForEquipmentBySubType(CardSubType.OffensiveHorse);
        if (offensiveHorseSkill is not null)
        {
            skillManager.AddEquipmentSkill(game, attacker, offensiveHorseSkill);
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
        // Base attack distance should be 1
        Assert.AreEqual(1, attackDistance);
        // With offensive horse, seat distance requirement is decreased by 1
        // So seatDistance (2) becomes 1 after modification, and 1 <= 1 is true
        Assert.IsTrue(isWithinRange);
    }

    /// <summary>
    /// Tests that RangeRuleService allows normal attack range calculation when no offensive equipment is present.
    /// Input: 4-player game, attacker and defender with seat distance = 2, attacker has no equipment.
    /// Expected: IsWithinAttackRange returns false for non-adjacent players (seat distance 2 > attack distance 1).
    /// </summary>
    [TestMethod]
    public void RangeRuleServiceWithoutOffensiveHorseAllowsNormalAttack()
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
        var isWithinRange = rangeRuleService.IsWithinAttackRange(game, attacker, defender);

        // Assert
        // Non-adjacent players should not be within attack range without offensive equipment
        Assert.IsFalse(isWithinRange);
    }

    #endregion
}
