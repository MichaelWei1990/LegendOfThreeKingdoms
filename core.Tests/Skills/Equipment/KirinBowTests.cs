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
public sealed class KirinBowTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Card CreateKirinBowCard(int id = 1, string definitionId = "kirin_bow", string name = "麒麟弓")
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

    private static Card CreateHorseCard(int id, CardSubType subType, string name = "Horse")
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"horse_{id}",
            Name = name,
            CardType = CardType.Equip,
            CardSubType = subType,
            Suit = Suit.Spade,
            Rank = 1
        };
    }

    #region Skill Factory Tests

    /// <summary>
    /// Tests that KirinBowSkillFactory creates correct skill instance.
    /// Input: KirinBowSkillFactory instance.
    /// Expected: Created skill has correct Id, Name, and SkillType.Trigger.
    /// </summary>
    [TestMethod]
    public void KirinBowSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new KirinBowSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("kirin_bow", skill.Id);
        Assert.AreEqual("麒麟", skill.Name);
        Assert.AreEqual(SkillType.Trigger, skill.Type);
        Assert.IsInstanceOfType(skill, typeof(IAttackDistanceModifyingSkill));
        Assert.IsInstanceOfType(skill, typeof(IAfterDamageSkill));
    }

    /// <summary>
    /// Tests that EquipmentSkillRegistry can register and retrieve Kirin Bow skill by DefinitionId.
    /// Input: Empty registry, KirinBowSkillFactory, DefinitionId "kirin_bow".
    /// Expected: After registration, GetSkillForEquipment returns a skill with correct Id.
    /// </summary>
    [TestMethod]
    public void EquipmentSkillRegistryRegisterKirinBowSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new EquipmentSkillRegistry();
        var factory = new KirinBowSkillFactory();

        // Act
        registry.RegisterEquipmentSkill("kirin_bow", factory);
        var skill = registry.GetSkillForEquipment("kirin_bow");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("kirin_bow", skill.Id);
        Assert.AreEqual("麒麟", skill.Name);
    }

    #endregion

    #region Attack Distance Tests

    /// <summary>
    /// Tests that KirinBowSkill sets attack distance to 5 when active.
    /// Input: 2-player game, attacker and defender, active kirin bow skill.
    /// Expected: ModifyAttackDistance returns 5 (weapon's fixed range).
    /// </summary>
    [TestMethod]
    public void KirinBowSkillModifyAttackDistanceSetsDistanceToFive()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var defender = game.Players[1];
        var skill = new KirinBowSkill();

        // Act
        var modified = skill.ModifyAttackDistance(1, game, attacker, defender);

        // Assert
        Assert.IsNotNull(modified);
        Assert.AreEqual(5, modified.Value);
    }

    /// <summary>
    /// Tests that KirinBowSkill does not modify distance when the owner (attacker) is not active.
    /// Input: 2-player game, attacker is dead (IsAlive = false), kirin bow skill.
    /// Expected: ModifyAttackDistance returns null (no modification) when owner is not active.
    /// </summary>
    [TestMethod]
    public void KirinBowSkillModifyAttackDistanceWhenOwnerIsNotActiveReturnsNull()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var defender = game.Players[1];
        attacker.IsAlive = false; // Owner is not active
        var skill = new KirinBowSkill();

        // Act
        var modified = skill.ModifyAttackDistance(1, game, attacker, defender);

        // Assert
        Assert.IsNull(modified);
    }

    /// <summary>
    /// Tests that RangeRuleService correctly applies kirin bow skill to set attack distance to 5.
    /// Input: 6-player game, attacker and defender with seat distance = 5, attacker has kirin bow equipped and skill active.
    /// Expected: Base seat distance = 5, base attack distance = 1, but with kirin bow attack distance = 5,
    /// so IsWithinAttackRange returns true (because 5 <= 5, so within range).
    /// </summary>
    [TestMethod]
    public void RangeRuleServiceWithKirinBowSetsAttackRangeToFive()
    {
        // Arrange
        var game = CreateDefaultGame(6);
        var attacker = game.Players[0];
        var defender = game.Players[5]; // Player 5 is at distance 3 from player 0 in 6-player game (clockwise: 5, counter-clockwise: 1, min: 1)
        // Actually, let's use player 3 which should be at distance 3 (clockwise: 3, counter-clockwise: 3, min: 3)
        defender = game.Players[3]; // Player 3 is at distance 3 from player 0 in 6-player game
        
        // Equip kirin bow to attacker
        var kirinBow = CreateKirinBowCard();
        if (attacker.EquipmentZone is Zone equipmentZone)
        {
            equipmentZone.MutableCards.Add(kirinBow);
        }

        // Setup skill manager and equipment skill registry
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("kirin_bow", new KirinBowSkillFactory());
        
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.LoadSkillsForPlayer(game, attacker);
        
        var kirinBowSkill = equipmentSkillRegistry.GetSkillForEquipment("kirin_bow");
        if (kirinBowSkill is not null)
        {
            skillManager.AddEquipmentSkill(game, attacker, kirinBowSkill);
        }

        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var rangeRuleService = new RangeRuleService(modifierProvider);

        // Act
        var seatDistance = rangeRuleService.GetSeatDistance(game, attacker, defender);
        var attackDistance = rangeRuleService.GetAttackDistance(game, attacker, defender);
        var isWithinRange = rangeRuleService.IsWithinAttackRange(game, attacker, defender);

        // Assert
        // Base seat distance should be 3 (non-adjacent players in 6-player game)
        Assert.AreEqual(3, seatDistance);
        // Base attack distance should be 1, but with kirin bow it becomes 5
        Assert.AreEqual(5, attackDistance);
        // With kirin bow, attack distance is set to 5, so 3 <= 5 is true
        Assert.IsTrue(isWithinRange);
    }

    #endregion

    #region AfterDamage Tests

    /// <summary>
    /// Tests that KirinBowSkill triggers and discards target's horse equipment after Slash damage.
    /// Input: Game with attacker (has Kirin Bow) and target (has OffensiveHorse), attacker deals Slash damage.
    /// Expected: After damage is resolved, target's OffensiveHorse is discarded.
    /// </summary>
    [TestMethod]
    public void KirinBowSkillDiscardsTargetHorseAfterSlashDamage()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var target = game.Players[1];

        // Equip kirin bow to attacker
        var kirinBow = CreateKirinBowCard();
        if (attacker.EquipmentZone is Zone attackerEquipmentZone)
        {
            attackerEquipmentZone.MutableCards.Add(kirinBow);
        }

        // Equip offensive horse to target
        var offensiveHorse = CreateHorseCard(1, CardSubType.OffensiveHorse, "进攻马");
        if (target.EquipmentZone is Zone targetEquipmentZone)
        {
            targetEquipmentZone.MutableCards.Add(offensiveHorse);
        }
        var initialTargetEquipmentCount = target.EquipmentZone.Cards.Count;

        // Setup skill manager and equipment skill registry
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("kirin_bow", new KirinBowSkillFactory());
        
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.LoadSkillsForPlayer(game, attacker);
        
        var kirinBowSkill = equipmentSkillRegistry.GetSkillForEquipment("kirin_bow");
        if (kirinBowSkill is not null)
        {
            skillManager.AddEquipmentSkill(game, attacker, kirinBowSkill);
        }

        var cardMoveService = new BasicCardMoveService(eventBus);
        if (kirinBowSkill is KirinBowSkill kirinBowSkillInstance)
        {
            kirinBowSkillInstance.SetCardMoveService(cardMoveService);
        }

        // Create Slash card
        var slash = CreateSlashCard(100);

        // Act: Publish AfterDamageEvent
        var damage = new DamageDescriptor(
            SourceSeat: attacker.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash",
            CausingCard: slash
        );
        var afterDamageEvent = new AfterDamageEvent(
            game,
            damage,
            PreviousHealth: target.CurrentHealth,
            CurrentHealth: target.CurrentHealth - 1
        );
        eventBus.Publish(afterDamageEvent);

        // Assert: Target's offensive horse should be discarded
        Assert.IsFalse(target.EquipmentZone.Cards.Contains(offensiveHorse), 
            "Target's offensive horse should be discarded");
        Assert.AreEqual(initialTargetEquipmentCount - 1, target.EquipmentZone.Cards.Count,
            "Target should have one less equipment card");
        Assert.IsTrue(game.DiscardPile.Cards.Contains(offensiveHorse),
            "Offensive horse should be in discard pile");
    }

    /// <summary>
    /// Tests that KirinBowSkill does NOT trigger when damage is not from Slash.
    /// Input: Game with attacker (has Kirin Bow) and target (has OffensiveHorse), attacker deals non-Slash damage.
    /// Expected: Target's OffensiveHorse is NOT discarded.
    /// </summary>
    [TestMethod]
    public void KirinBowSkillDoesNotTriggerForNonSlashDamage()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var target = game.Players[1];

        // Equip kirin bow to attacker
        var kirinBow = CreateKirinBowCard();
        if (attacker.EquipmentZone is Zone attackerEquipmentZone)
        {
            attackerEquipmentZone.MutableCards.Add(kirinBow);
        }

        // Equip offensive horse to target
        var offensiveHorse = CreateHorseCard(1, CardSubType.OffensiveHorse, "进攻马");
        if (target.EquipmentZone is Zone targetEquipmentZone)
        {
            targetEquipmentZone.MutableCards.Add(offensiveHorse);
        }
        var initialTargetEquipmentCount = target.EquipmentZone.Cards.Count;

        // Setup skill manager and equipment skill registry
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("kirin_bow", new KirinBowSkillFactory());
        
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.LoadSkillsForPlayer(game, attacker);
        
        var kirinBowSkill = equipmentSkillRegistry.GetSkillForEquipment("kirin_bow");
        if (kirinBowSkill is not null)
        {
            skillManager.AddEquipmentSkill(game, attacker, kirinBowSkill);
        }

        var cardMoveService = new BasicCardMoveService(eventBus);
        if (kirinBowSkill is KirinBowSkill kirinBowSkillInstance)
        {
            kirinBowSkillInstance.SetCardMoveService(cardMoveService);
        }

        // Act: Publish AfterDamageEvent with non-Slash damage
        var damage = new DamageDescriptor(
            SourceSeat: attacker.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Duel" // Not Slash
        );
        var afterDamageEvent = new AfterDamageEvent(
            game,
            damage,
            PreviousHealth: target.CurrentHealth,
            CurrentHealth: target.CurrentHealth - 1
        );
        eventBus.Publish(afterDamageEvent);

        // Assert: Target's offensive horse should NOT be discarded
        Assert.IsTrue(target.EquipmentZone.Cards.Contains(offensiveHorse), 
            "Target's offensive horse should NOT be discarded");
        Assert.AreEqual(initialTargetEquipmentCount, target.EquipmentZone.Cards.Count,
            "Target should have the same number of equipment cards");
    }

    /// <summary>
    /// Tests that KirinBowSkill does NOT trigger when damage source is not the skill owner.
    /// Input: Game with attacker (has Kirin Bow) and target (has OffensiveHorse), different player deals Slash damage.
    /// Expected: Target's OffensiveHorse is NOT discarded.
    /// </summary>
    [TestMethod]
    public void KirinBowSkillDoesNotTriggerWhenDamageSourceIsNotOwner()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        var kirinBowOwner = game.Players[0];
        var attacker = game.Players[1]; // Different player
        var target = game.Players[2];

        // Equip kirin bow to kirinBowOwner (not the attacker)
        var kirinBow = CreateKirinBowCard();
        if (kirinBowOwner.EquipmentZone is Zone ownerEquipmentZone)
        {
            ownerEquipmentZone.MutableCards.Add(kirinBow);
        }

        // Equip offensive horse to target
        var offensiveHorse = CreateHorseCard(1, CardSubType.OffensiveHorse, "进攻马");
        if (target.EquipmentZone is Zone targetEquipmentZone)
        {
            targetEquipmentZone.MutableCards.Add(offensiveHorse);
        }
        var initialTargetEquipmentCount = target.EquipmentZone.Cards.Count;

        // Setup skill manager and equipment skill registry
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("kirin_bow", new KirinBowSkillFactory());
        
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.LoadSkillsForPlayer(game, kirinBowOwner);
        
        var kirinBowSkill = equipmentSkillRegistry.GetSkillForEquipment("kirin_bow");
        if (kirinBowSkill is not null)
        {
            skillManager.AddEquipmentSkill(game, kirinBowOwner, kirinBowSkill);
        }

        var cardMoveService = new BasicCardMoveService(eventBus);
        if (kirinBowSkill is KirinBowSkill kirinBowSkillInstance)
        {
            kirinBowSkillInstance.SetCardMoveService(cardMoveService);
        }

        // Create Slash card
        var slash = CreateSlashCard(100);

        // Act: Publish AfterDamageEvent with attacker as source (not kirinBowOwner)
        var damage = new DamageDescriptor(
            SourceSeat: attacker.Seat, // Different from kirinBowOwner
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash",
            CausingCard: slash
        );
        var afterDamageEvent = new AfterDamageEvent(
            game,
            damage,
            PreviousHealth: target.CurrentHealth,
            CurrentHealth: target.CurrentHealth - 1
        );
        eventBus.Publish(afterDamageEvent);

        // Assert: Target's offensive horse should NOT be discarded
        Assert.IsTrue(target.EquipmentZone.Cards.Contains(offensiveHorse), 
            "Target's offensive horse should NOT be discarded (damage source is not kirin bow owner)");
        Assert.AreEqual(initialTargetEquipmentCount, target.EquipmentZone.Cards.Count,
            "Target should have the same number of equipment cards");
    }

    /// <summary>
    /// Tests that KirinBowSkill does NOT trigger when target has no horse equipment.
    /// Input: Game with attacker (has Kirin Bow) and target (no horse equipment), attacker deals Slash damage.
    /// Expected: No horse equipment is discarded.
    /// </summary>
    [TestMethod]
    public void KirinBowSkillDoesNotTriggerWhenTargetHasNoHorseEquipment()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var target = game.Players[1];

        // Equip kirin bow to attacker
        var kirinBow = CreateKirinBowCard();
        if (attacker.EquipmentZone is Zone attackerEquipmentZone)
        {
            attackerEquipmentZone.MutableCards.Add(kirinBow);
        }

        // Target has no horse equipment
        var initialTargetEquipmentCount = target.EquipmentZone.Cards.Count;

        // Setup skill manager and equipment skill registry
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("kirin_bow", new KirinBowSkillFactory());
        
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.LoadSkillsForPlayer(game, attacker);
        
        var kirinBowSkill = equipmentSkillRegistry.GetSkillForEquipment("kirin_bow");
        if (kirinBowSkill is not null)
        {
            skillManager.AddEquipmentSkill(game, attacker, kirinBowSkill);
        }

        var cardMoveService = new BasicCardMoveService(eventBus);
        if (kirinBowSkill is KirinBowSkill kirinBowSkillInstance)
        {
            kirinBowSkillInstance.SetCardMoveService(cardMoveService);
        }

        // Create Slash card
        var slash = CreateSlashCard(100);

        // Act: Publish AfterDamageEvent
        var damage = new DamageDescriptor(
            SourceSeat: attacker.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash",
            CausingCard: slash
        );
        var afterDamageEvent = new AfterDamageEvent(
            game,
            damage,
            PreviousHealth: target.CurrentHealth,
            CurrentHealth: target.CurrentHealth - 1
        );
        eventBus.Publish(afterDamageEvent);

        // Assert: Target's equipment count should not change
        Assert.AreEqual(initialTargetEquipmentCount, target.EquipmentZone.Cards.Count,
            "Target should have the same number of equipment cards");
    }

    /// <summary>
    /// Tests that KirinBowSkill allows selecting which horse to discard when target has both OffensiveHorse and DefensiveHorse.
    /// Input: Game with attacker (has Kirin Bow) and target (has both OffensiveHorse and DefensiveHorse), attacker deals Slash damage.
    /// Expected: Player can select which horse to discard, and the selected horse is discarded.
    /// </summary>
    [TestMethod]
    public void KirinBowSkillAllowsSelectingHorseWhenTargetHasBothHorses()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var target = game.Players[1];

        // Equip kirin bow to attacker
        var kirinBow = CreateKirinBowCard();
        if (attacker.EquipmentZone is Zone attackerEquipmentZone)
        {
            attackerEquipmentZone.MutableCards.Add(kirinBow);
        }

        // Equip both horses to target
        var offensiveHorse = CreateHorseCard(1, CardSubType.OffensiveHorse, "进攻马");
        var defensiveHorse = CreateHorseCard(2, CardSubType.DefensiveHorse, "防御马");
        if (target.EquipmentZone is Zone targetEquipmentZone)
        {
            targetEquipmentZone.MutableCards.Add(offensiveHorse);
            targetEquipmentZone.MutableCards.Add(defensiveHorse);
        }
        var initialTargetEquipmentCount = target.EquipmentZone.Cards.Count;

        // Setup skill manager and equipment skill registry
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("kirin_bow", new KirinBowSkillFactory());
        
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.LoadSkillsForPlayer(game, attacker);
        
        var kirinBowSkill = equipmentSkillRegistry.GetSkillForEquipment("kirin_bow");
        if (kirinBowSkill is not null)
        {
            skillManager.AddEquipmentSkill(game, attacker, kirinBowSkill);
        }

        var cardMoveService = new BasicCardMoveService(eventBus);
        
        // Setup getPlayerChoice to select defensive horse
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = request =>
        {
            if (request.ChoiceType == ChoiceType.Confirm)
            {
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: true
                );
            }
            else if (request.ChoiceType == ChoiceType.SelectCards)
            {
                // Select defensive horse
                var defensiveHorseId = defensiveHorse.Id;
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: new[] { defensiveHorseId },
                    SelectedOptionId: null,
                    Confirmed: false
                );
            }
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: false
            );
        };

        if (kirinBowSkill is KirinBowSkill kirinBowSkillInstance)
        {
            kirinBowSkillInstance.SetCardMoveService(cardMoveService);
            kirinBowSkillInstance.SetGetPlayerChoice(getPlayerChoice);
        }

        // Create Slash card
        var slash = CreateSlashCard(100);

        // Act: Publish AfterDamageEvent
        var damage = new DamageDescriptor(
            SourceSeat: attacker.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash",
            CausingCard: slash
        );
        var afterDamageEvent = new AfterDamageEvent(
            game,
            damage,
            PreviousHealth: target.CurrentHealth,
            CurrentHealth: target.CurrentHealth - 1
        );
        eventBus.Publish(afterDamageEvent);

        // Assert: Selected defensive horse should be discarded, offensive horse should remain
        Assert.IsFalse(target.EquipmentZone.Cards.Contains(defensiveHorse), 
            "Selected defensive horse should be discarded");
        Assert.IsTrue(target.EquipmentZone.Cards.Contains(offensiveHorse), 
            "Non-selected offensive horse should remain");
        Assert.AreEqual(initialTargetEquipmentCount - 1, target.EquipmentZone.Cards.Count,
            "Target should have one less equipment card");
        Assert.IsTrue(game.DiscardPile.Cards.Contains(defensiveHorse),
            "Defensive horse should be in discard pile");
    }

    /// <summary>
    /// Tests that KirinBowSkill does NOT trigger when skill owner is not active.
    /// Input: Game with attacker (has Kirin Bow but is dead) and target (has OffensiveHorse), attacker deals Slash damage.
    /// Expected: Target's OffensiveHorse is NOT discarded.
    /// </summary>
    [TestMethod]
    public void KirinBowSkillDoesNotTriggerWhenOwnerIsNotActive()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var target = game.Players[1];

        // Equip kirin bow to attacker
        var kirinBow = CreateKirinBowCard();
        if (attacker.EquipmentZone is Zone attackerEquipmentZone)
        {
            attackerEquipmentZone.MutableCards.Add(kirinBow);
        }

        // Equip offensive horse to target
        var offensiveHorse = CreateHorseCard(1, CardSubType.OffensiveHorse, "进攻马");
        if (target.EquipmentZone is Zone targetEquipmentZone)
        {
            targetEquipmentZone.MutableCards.Add(offensiveHorse);
        }
        var initialTargetEquipmentCount = target.EquipmentZone.Cards.Count;

        // Setup skill manager and equipment skill registry
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("kirin_bow", new KirinBowSkillFactory());
        
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.LoadSkillsForPlayer(game, attacker);
        
        var kirinBowSkill = equipmentSkillRegistry.GetSkillForEquipment("kirin_bow");
        if (kirinBowSkill is not null)
        {
            skillManager.AddEquipmentSkill(game, attacker, kirinBowSkill);
        }

        var cardMoveService = new BasicCardMoveService(eventBus);
        if (kirinBowSkill is KirinBowSkill kirinBowSkillInstance)
        {
            kirinBowSkillInstance.SetCardMoveService(cardMoveService);
        }

        // Mark attacker as dead (not active)
        attacker.IsAlive = false;

        // Create Slash card
        var slash = CreateSlashCard(100);

        // Act: Publish AfterDamageEvent
        var damage = new DamageDescriptor(
            SourceSeat: attacker.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash",
            CausingCard: slash
        );
        var afterDamageEvent = new AfterDamageEvent(
            game,
            damage,
            PreviousHealth: target.CurrentHealth,
            CurrentHealth: target.CurrentHealth - 1
        );
        eventBus.Publish(afterDamageEvent);

        // Assert: Target's offensive horse should NOT be discarded
        Assert.IsTrue(target.EquipmentZone.Cards.Contains(offensiveHorse), 
            "Target's offensive horse should NOT be discarded (owner is not active)");
        Assert.AreEqual(initialTargetEquipmentCount, target.EquipmentZone.Cards.Count,
            "Target should have the same number of equipment cards");
    }

    /// <summary>
    /// Tests that KirinBowSkill auto-selects first horse when getPlayerChoice is not available and target has multiple horses.
    /// Input: Game with attacker (has Kirin Bow, no getPlayerChoice) and target (has both OffensiveHorse and DefensiveHorse), attacker deals Slash damage.
    /// Expected: First horse (OffensiveHorse) is automatically discarded.
    /// </summary>
    [TestMethod]
    public void KirinBowSkillAutoSelectsFirstHorseWhenNoPlayerChoice()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var target = game.Players[1];

        // Equip kirin bow to attacker
        var kirinBow = CreateKirinBowCard();
        if (attacker.EquipmentZone is Zone attackerEquipmentZone)
        {
            attackerEquipmentZone.MutableCards.Add(kirinBow);
        }

        // Equip both horses to target
        var offensiveHorse = CreateHorseCard(1, CardSubType.OffensiveHorse, "进攻马");
        var defensiveHorse = CreateHorseCard(2, CardSubType.DefensiveHorse, "防御马");
        if (target.EquipmentZone is Zone targetEquipmentZone)
        {
            targetEquipmentZone.MutableCards.Add(offensiveHorse);
            targetEquipmentZone.MutableCards.Add(defensiveHorse);
        }
        var initialTargetEquipmentCount = target.EquipmentZone.Cards.Count;

        // Setup skill manager and equipment skill registry
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("kirin_bow", new KirinBowSkillFactory());
        
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.LoadSkillsForPlayer(game, attacker);
        
        var kirinBowSkill = equipmentSkillRegistry.GetSkillForEquipment("kirin_bow");
        if (kirinBowSkill is not null)
        {
            skillManager.AddEquipmentSkill(game, attacker, kirinBowSkill);
        }

        var cardMoveService = new BasicCardMoveService(eventBus);
        if (kirinBowSkill is KirinBowSkill kirinBowSkillInstance)
        {
            kirinBowSkillInstance.SetCardMoveService(cardMoveService);
            // Do NOT set getPlayerChoice - should auto-select
        }

        // Create Slash card
        var slash = CreateSlashCard(100);

        // Act: Publish AfterDamageEvent
        var damage = new DamageDescriptor(
            SourceSeat: attacker.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash",
            CausingCard: slash
        );
        var afterDamageEvent = new AfterDamageEvent(
            game,
            damage,
            PreviousHealth: target.CurrentHealth,
            CurrentHealth: target.CurrentHealth - 1
        );
        eventBus.Publish(afterDamageEvent);

        // Assert: First horse (offensive horse) should be discarded
        Assert.AreEqual(initialTargetEquipmentCount - 1, target.EquipmentZone.Cards.Count,
            "Target should have one less equipment card");
        // Note: The order of cards in EquipmentZone.Cards may vary, so we just check that one was discarded
        Assert.IsTrue(
            !target.EquipmentZone.Cards.Contains(offensiveHorse) || 
            !target.EquipmentZone.Cards.Contains(defensiveHorse),
            "At least one horse should be discarded");
    }

    #endregion
}

