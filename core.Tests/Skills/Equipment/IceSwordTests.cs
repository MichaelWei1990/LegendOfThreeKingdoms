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
public sealed class IceSwordTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Card CreateIceSwordCard(int id = 1, string definitionId = "ice_sword", string name = "寒冰剑")
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
    /// Tests that IceSwordSkillFactory creates correct skill instance.
    /// Input: IceSwordSkillFactory instance.
    /// Expected: Created skill has correct Id, Name, and SkillType.Trigger.
    /// </summary>
    [TestMethod]
    public void IceSwordSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new IceSwordSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("ice_sword", skill.Id);
        Assert.AreEqual("寒冰", skill.Name);
        Assert.AreEqual(SkillType.Trigger, skill.Type);
    }

    /// <summary>
    /// Tests that EquipmentSkillRegistry can register and retrieve Ice Sword skill by DefinitionId.
    /// Input: Empty registry, IceSwordSkillFactory, DefinitionId "ice_sword".
    /// Expected: After registration, GetSkillForEquipment returns a skill with correct Id.
    /// </summary>
    [TestMethod]
    public void EquipmentSkillRegistryRegisterIceSwordSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new EquipmentSkillRegistry();
        var factory = new IceSwordSkillFactory();

        // Act
        registry.RegisterEquipmentSkill("ice_sword", factory);
        var skill = registry.GetSkillForEquipment("ice_sword");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("ice_sword", skill.Id);
        Assert.AreEqual("寒冰", skill.Name);
    }

    #endregion

    #region BeforeDamageEvent Tests

    /// <summary>
    /// Tests that Ice Sword skill prevents Slash damage and discards target's cards.
    /// Input: Game, attacker with Ice Sword skill uses Slash, target has cards.
    /// Expected: BeforeDamageEvent triggers skill, damage is prevented, target's cards are discarded.
    /// </summary>
    [TestMethod]
    public void IceSwordSkillPreventsSlashDamageAndDiscardsTargetCards()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var target = game.Players[1];

        var eventBus = new BasicEventBus();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("ice_sword", new IceSwordSkillFactory());
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);

        var iceSwordSkill = equipmentSkillRegistry.GetSkillForEquipment("ice_sword");
        var cardMoveService = new BasicCardMoveService(eventBus);

        // Set services on skill
        if (iceSwordSkill is IceSwordSkill iceSword)
        {
            iceSword.SetCardMoveService(cardMoveService);
        }

        skillManager.AddEquipmentSkill(game, attacker, iceSwordSkill);

        // Add cards to target's hand (at least 2 for discarding)
        var card1 = CreateTestCard(100);
        var card2 = CreateTestCard(101);
        if (target.HandZone is Zone targetHand)
        {
            targetHand.MutableCards.Add(card1);
            targetHand.MutableCards.Add(card2);
        }

        var initialTargetHandCount = target.HandZone.Cards.Count;
        var initialTargetHealth = target.CurrentHealth;

        // Create Slash card
        var slash = CreateSlashCard(1);

        // Create damage descriptor for Slash
        var damage = new DamageDescriptor(
            SourceSeat: attacker.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash",
            CausingCard: slash
        );

        // Act: Publish BeforeDamageEvent
        var beforeDamageEvent = new BeforeDamageEvent(game, damage);
        eventBus.Publish(beforeDamageEvent);

        // Assert: Damage should be prevented
        Assert.IsTrue(beforeDamageEvent.IsPrevented,
            "Damage should be prevented by Ice Sword skill");

        // Assert: Target's cards should be discarded
        Assert.IsTrue(target.HandZone.Cards.Count < initialTargetHandCount,
            "Target should have discarded cards");

        // Assert: Target's health should not change (damage was prevented)
        Assert.AreEqual(initialTargetHealth, target.CurrentHealth,
            "Target health should not change (damage was prevented)");
    }

    /// <summary>
    /// Tests that Ice Sword skill does not trigger when damage is not from Slash.
    /// Input: Game, attacker with Ice Sword skill, damage from non-Slash source.
    /// Expected: Skill does not trigger, damage is not prevented.
    /// </summary>
    [TestMethod]
    public void IceSwordSkillDoesNotTriggerWhenDamageNotFromSlash()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var target = game.Players[1];

        var eventBus = new BasicEventBus();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("ice_sword", new IceSwordSkillFactory());
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);

        var iceSwordSkill = equipmentSkillRegistry.GetSkillForEquipment("ice_sword");
        var cardMoveService = new BasicCardMoveService(eventBus);

        // Set services on skill
        if (iceSwordSkill is IceSwordSkill iceSword)
        {
            iceSword.SetCardMoveService(cardMoveService);
        }

        skillManager.AddEquipmentSkill(game, attacker, iceSwordSkill);

        // Add cards to target's hand
        var card1 = CreateTestCard(100);
        if (target.HandZone is Zone targetHand)
        {
            targetHand.MutableCards.Add(card1);
        }

        var initialTargetHandCount = target.HandZone.Cards.Count;
        var initialTargetHealth = target.CurrentHealth;

        // Create damage descriptor from non-Slash source (e.g., skill damage)
        var damage = new DamageDescriptor(
            SourceSeat: attacker.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Skill", // Not "Slash"
            CausingCard: null // No causing card
        );

        // Act: Publish BeforeDamageEvent
        var beforeDamageEvent = new BeforeDamageEvent(game, damage);
        eventBus.Publish(beforeDamageEvent);

        // Assert: Damage should NOT be prevented
        Assert.IsFalse(beforeDamageEvent.IsPrevented,
            "Damage should not be prevented (not from Slash)");

        // Assert: Target's cards should NOT be discarded
        Assert.AreEqual(initialTargetHandCount, target.HandZone.Cards.Count,
            "Target should not have discarded cards");

        // Assert: Target's health should not change (we only published BeforeDamageEvent, didn't apply damage)
        Assert.AreEqual(initialTargetHealth, target.CurrentHealth,
            "Target health should not change");
    }

    /// <summary>
    /// Tests that Ice Sword skill does not trigger when attacker is not the owner.
    /// Input: Game, player A with Ice Sword skill, player B deals Slash damage.
    /// Expected: Skill does not trigger for player A (not the damage source).
    /// </summary>
    [TestMethod]
    public void IceSwordSkillDoesNotTriggerWhenNotDamageSource()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        var playerA = game.Players[0]; // Has Ice Sword skill
        var playerB = game.Players[1]; // Uses Slash
        var target = game.Players[2];

        var eventBus = new BasicEventBus();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("ice_sword", new IceSwordSkillFactory());
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);

        var iceSwordSkill = equipmentSkillRegistry.GetSkillForEquipment("ice_sword");
        var cardMoveService = new BasicCardMoveService(eventBus);

        // Set services on skill
        if (iceSwordSkill is IceSwordSkill iceSword)
        {
            iceSword.SetCardMoveService(cardMoveService);
        }

        // Add skill to player A (not the damage source)
        skillManager.AddEquipmentSkill(game, playerA, iceSwordSkill);

        // Add cards to target's hand
        var card1 = CreateTestCard(100);
        var card2 = CreateTestCard(101);
        if (target.HandZone is Zone targetHand)
        {
            targetHand.MutableCards.Add(card1);
            targetHand.MutableCards.Add(card2);
        }

        var initialTargetHandCount = target.HandZone.Cards.Count;

        // Create Slash card used by player B
        var slash = CreateSlashCard(1);

        // Create damage descriptor with player B as source
        var damage = new DamageDescriptor(
            SourceSeat: playerB.Seat, // Player B is the damage source, not player A
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash",
            CausingCard: slash
        );

        // Act: Publish BeforeDamageEvent
        var beforeDamageEvent = new BeforeDamageEvent(game, damage);
        eventBus.Publish(beforeDamageEvent);

        // Assert: Damage should NOT be prevented (player A's skill should not trigger)
        Assert.IsFalse(beforeDamageEvent.IsPrevented,
            "Damage should not be prevented (player A is not the damage source)");

        // Assert: Target's cards should NOT be discarded
        Assert.AreEqual(initialTargetHandCount, target.HandZone.Cards.Count,
            "Target should not have discarded cards");
    }

    /// <summary>
    /// Tests that Ice Sword skill allows player to choose cards to discard.
    /// Input: Game, attacker with Ice Sword skill uses Slash, target has cards, getPlayerChoice provided.
    /// Expected: Player can choose which cards to discard, then damage is prevented.
    /// </summary>
    [TestMethod]
    public void IceSwordSkillAllowsPlayerToChooseCardsToDiscard()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var target = game.Players[1];

        var eventBus = new BasicEventBus();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("ice_sword", new IceSwordSkillFactory());
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);

        var iceSwordSkill = equipmentSkillRegistry.GetSkillForEquipment("ice_sword");
        var cardMoveService = new BasicCardMoveService(eventBus);

        // Add cards to target's hand
        var card1 = CreateTestCard(100);
        var card2 = CreateTestCard(101);
        var card3 = CreateTestCard(102);
        if (target.HandZone is Zone targetHand)
        {
            targetHand.MutableCards.Add(card1);
            targetHand.MutableCards.Add(card2);
            targetHand.MutableCards.Add(card3);
        }

        var initialTargetHandCount = target.HandZone.Cards.Count;
        var initialTargetHealth = target.CurrentHealth;

        // Create Slash card
        var slash = CreateSlashCard(1);

        // Setup getPlayerChoice to confirm and select cards
        ChoiceRequest? lastConfirmRequest = null;
        ChoiceRequest? lastSelectRequest = null;
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            if (request.ChoiceType == ChoiceType.Confirm)
            {
                lastConfirmRequest = request;
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: true // Confirm activation
                );
            }
            else if (request.ChoiceType == ChoiceType.SelectCards)
            {
                lastSelectRequest = request;
                // Select first 2 cards from allowed cards
                var selectedIds = request.AllowedCards?.Take(2).Select(c => c.Id).ToList() ?? new List<int>();
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: selectedIds,
                    SelectedOptionId: null,
                    Confirmed: null
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

        // Set services on skill
        if (iceSwordSkill is IceSwordSkill iceSword)
        {
            iceSword.SetCardMoveService(cardMoveService);
            iceSword.SetGetPlayerChoice(getPlayerChoice);
        }

        skillManager.AddEquipmentSkill(game, attacker, iceSwordSkill);

        // Create damage descriptor for Slash
        var damage = new DamageDescriptor(
            SourceSeat: attacker.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash",
            CausingCard: slash
        );

        // Act: Publish BeforeDamageEvent
        var beforeDamageEvent = new BeforeDamageEvent(game, damage);
        eventBus.Publish(beforeDamageEvent);

        // Assert: Confirm request should be made
        Assert.IsNotNull(lastConfirmRequest, "Confirm request should be made");
        Assert.AreEqual(attacker.Seat, lastConfirmRequest.PlayerSeat);

        // Assert: Select cards request should be made
        Assert.IsNotNull(lastSelectRequest, "Select cards request should be made");
        Assert.AreEqual(ChoiceType.SelectCards, lastSelectRequest.ChoiceType);
        Assert.IsTrue(lastSelectRequest.AllowedCards?.Count >= 2, "At least 2 cards should be available for selection");

        // Assert: Damage should be prevented
        Assert.IsTrue(beforeDamageEvent.IsPrevented,
            "Damage should be prevented by Ice Sword skill");

        // Assert: Cards should be discarded and damage should be prevented
        Assert.IsTrue(target.HandZone.Cards.Count < initialTargetHandCount,
            "Target should have discarded cards");
        Assert.AreEqual(initialTargetHealth, target.CurrentHealth,
            "Target health should not change (damage was prevented)");
    }

    /// <summary>
    /// Tests that Ice Sword skill does not trigger when player chooses not to activate.
    /// Input: Game, attacker with Ice Sword skill uses Slash, player chooses not to activate.
    /// Expected: No cards discarded, damage not prevented.
    /// </summary>
    [TestMethod]
    public void IceSwordSkillDoesNotTriggerWhenPlayerChoosesNotToActivate()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var target = game.Players[1];

        var eventBus = new BasicEventBus();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("ice_sword", new IceSwordSkillFactory());
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);

        var iceSwordSkill = equipmentSkillRegistry.GetSkillForEquipment("ice_sword");
        var cardMoveService = new BasicCardMoveService(eventBus);

        // Add cards to target's hand
        var card1 = CreateTestCard(100);
        var card2 = CreateTestCard(101);
        if (target.HandZone is Zone targetHand)
        {
            targetHand.MutableCards.Add(card1);
            targetHand.MutableCards.Add(card2);
        }

        var initialTargetHandCount = target.HandZone.Cards.Count;

        // Create Slash card
        var slash = CreateSlashCard(1);

        // Setup getPlayerChoice to decline activation
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            if (request.ChoiceType == ChoiceType.Confirm)
            {
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: false // Decline activation
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

        // Set services on skill
        if (iceSwordSkill is IceSwordSkill iceSword)
        {
            iceSword.SetCardMoveService(cardMoveService);
            iceSword.SetGetPlayerChoice(getPlayerChoice);
        }

        skillManager.AddEquipmentSkill(game, attacker, iceSwordSkill);

        // Create damage descriptor for Slash
        var damage = new DamageDescriptor(
            SourceSeat: attacker.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash",
            CausingCard: slash
        );

        // Act: Publish BeforeDamageEvent
        var beforeDamageEvent = new BeforeDamageEvent(game, damage);
        eventBus.Publish(beforeDamageEvent);

        // Assert: Damage should NOT be prevented
        Assert.IsFalse(beforeDamageEvent.IsPrevented,
            "Damage should not be prevented (player declined)");

        // Assert: Target's cards should NOT be discarded
        Assert.AreEqual(initialTargetHandCount, target.HandZone.Cards.Count,
            "Target should not have discarded cards (skill not activated)");
    }

    /// <summary>
    /// Tests that Ice Sword skill can discard equipment cards.
    /// Input: Game, attacker with Ice Sword skill uses Slash, target has equipment cards.
    /// Expected: Player can discard equipment cards, damage is prevented.
    /// </summary>
    [TestMethod]
    public void IceSwordSkillCanDiscardEquipmentCards()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var target = game.Players[1];

        var eventBus = new BasicEventBus();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("ice_sword", new IceSwordSkillFactory());
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);

        var iceSwordSkill = equipmentSkillRegistry.GetSkillForEquipment("ice_sword");
        var cardMoveService = new BasicCardMoveService(eventBus);

        // Add equipment cards to target
        var equipment1 = new Card
        {
            Id = 100,
            DefinitionId = "test_equipment_1",
            CardType = CardType.Equip,
            CardSubType = CardSubType.Armor,
            Suit = Suit.Spade,
            Rank = 5
        };
        var equipment2 = new Card
        {
            Id = 101,
            DefinitionId = "test_equipment_2",
            CardType = CardType.Equip,
            CardSubType = CardSubType.OffensiveHorse,
            Suit = Suit.Heart,
            Rank = 5
        };

        if (target.EquipmentZone is Zone equipmentZone)
        {
            equipmentZone.MutableCards.Add(equipment1);
            equipmentZone.MutableCards.Add(equipment2);
        }

        var initialEquipmentCount = target.EquipmentZone.Cards.Count;
        var initialTargetHealth = target.CurrentHealth;

        // Create Slash card
        var slash = CreateSlashCard(1);

        // Setup getPlayerChoice to select equipment cards
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
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
                // Select equipment cards
                var equipmentCards = request.AllowedCards?
                    .Where(c => c.CardType == CardType.Equip)
                    .Take(2)
                    .Select(c => c.Id)
                    .ToList() ?? new List<int>();
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: equipmentCards,
                    SelectedOptionId: null,
                    Confirmed: null
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

        // Set services on skill
        if (iceSwordSkill is IceSwordSkill iceSword)
        {
            iceSword.SetCardMoveService(cardMoveService);
            iceSword.SetGetPlayerChoice(getPlayerChoice);
        }

        skillManager.AddEquipmentSkill(game, attacker, iceSwordSkill);

        // Create damage descriptor for Slash
        var damage = new DamageDescriptor(
            SourceSeat: attacker.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash",
            CausingCard: slash
        );

        // Act: Publish BeforeDamageEvent
        var beforeDamageEvent = new BeforeDamageEvent(game, damage);
        eventBus.Publish(beforeDamageEvent);

        // Assert: Equipment cards should be discarded and damage should be prevented
        Assert.IsTrue(target.EquipmentZone.Cards.Count < initialEquipmentCount,
            "Target should have discarded equipment cards");
        Assert.IsTrue(beforeDamageEvent.IsPrevented,
            "Damage should be prevented by Ice Sword skill");
        Assert.AreEqual(initialTargetHealth, target.CurrentHealth,
            "Target health should not change (damage was prevented)");
    }

    /// <summary>
    /// Tests that Ice Sword skill discards all available cards if target has less than 2 cards.
    /// Input: Game, attacker with Ice Sword skill uses Slash, target has only 1 card.
    /// Expected: All available cards are discarded, damage is prevented.
    /// </summary>
    [TestMethod]
    public void IceSwordSkillDiscardsAllCardsWhenTargetHasLessThanTwo()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var target = game.Players[1];

        var eventBus = new BasicEventBus();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("ice_sword", new IceSwordSkillFactory());
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);

        var iceSwordSkill = equipmentSkillRegistry.GetSkillForEquipment("ice_sword");
        var cardMoveService = new BasicCardMoveService(eventBus);

        // Add only 1 card to target's hand
        var card1 = CreateTestCard(100);
        if (target.HandZone is Zone targetHand)
        {
            targetHand.MutableCards.Add(card1);
        }

        var initialTargetHandCount = target.HandZone.Cards.Count;
        var initialTargetHealth = target.CurrentHealth;

        // Create Slash card
        var slash = CreateSlashCard(1);

        // Set services on skill (auto-trigger)
        if (iceSwordSkill is IceSwordSkill iceSword)
        {
            iceSword.SetCardMoveService(cardMoveService);
        }

        skillManager.AddEquipmentSkill(game, attacker, iceSwordSkill);

        // Create damage descriptor for Slash
        var damage = new DamageDescriptor(
            SourceSeat: attacker.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash",
            CausingCard: slash
        );

        // Act: Publish BeforeDamageEvent
        var beforeDamageEvent = new BeforeDamageEvent(game, damage);
        eventBus.Publish(beforeDamageEvent);

        // Assert: Damage should be prevented
        Assert.IsTrue(beforeDamageEvent.IsPrevented,
            "Damage should be prevented by Ice Sword skill");

        // Assert: All available cards should be discarded
        Assert.IsTrue(target.HandZone.Cards.Count < initialTargetHandCount,
            "Target should have discarded all available cards");
        Assert.AreEqual(initialTargetHealth, target.CurrentHealth,
            "Target health should not change (damage was prevented)");
    }

    /// <summary>
    /// Tests that Ice Sword skill does not trigger when damage has already been prevented.
    /// Input: Game, attacker with Ice Sword skill uses Slash, damage already prevented.
    /// Expected: Skill does not trigger again, no additional cards discarded.
    /// </summary>
    [TestMethod]
    public void IceSwordSkillDoesNotTriggerWhenDamageAlreadyPrevented()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var target = game.Players[1];

        var eventBus = new BasicEventBus();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("ice_sword", new IceSwordSkillFactory());
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);

        var iceSwordSkill = equipmentSkillRegistry.GetSkillForEquipment("ice_sword");
        var cardMoveService = new BasicCardMoveService(eventBus);

        // Add cards to target's hand
        var card1 = CreateTestCard(100);
        var card2 = CreateTestCard(101);
        if (target.HandZone is Zone targetHand)
        {
            targetHand.MutableCards.Add(card1);
            targetHand.MutableCards.Add(card2);
        }

        var initialTargetHandCount = target.HandZone.Cards.Count;

        // Create Slash card
        var slash = CreateSlashCard(1);

        // Set services on skill
        if (iceSwordSkill is IceSwordSkill iceSword)
        {
            iceSword.SetCardMoveService(cardMoveService);
        }

        skillManager.AddEquipmentSkill(game, attacker, iceSwordSkill);

        // Create damage descriptor for Slash
        var damage = new DamageDescriptor(
            SourceSeat: attacker.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash",
            CausingCard: slash
        );

        // Act: Publish BeforeDamageEvent with damage already prevented
        var beforeDamageEvent = new BeforeDamageEvent(game, damage);
        beforeDamageEvent.IsPrevented = true; // Simulate damage already prevented by another skill
        eventBus.Publish(beforeDamageEvent);

        // Assert: Target's cards should NOT be discarded (skill should not trigger)
        Assert.AreEqual(initialTargetHandCount, target.HandZone.Cards.Count,
            "Target should not have discarded cards (damage already prevented)");
    }

    /// <summary>
    /// Tests that Ice Sword skill works with DamageResolver to actually prevent damage.
    /// Input: Game, attacker with Ice Sword skill uses Slash, full resolution flow.
    /// Expected: Damage is prevented, target's health does not decrease.
    /// </summary>
    [TestMethod]
    public void IceSwordSkillPreventsDamageInFullResolutionFlow()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var target = game.Players[1];

        var eventBus = new BasicEventBus();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("ice_sword", new IceSwordSkillFactory());
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);

        var iceSwordSkill = equipmentSkillRegistry.GetSkillForEquipment("ice_sword");
        var cardMoveService = new BasicCardMoveService(eventBus);

        // Add cards to target's hand
        var card1 = CreateTestCard(100);
        var card2 = CreateTestCard(101);
        if (target.HandZone is Zone targetHand)
        {
            targetHand.MutableCards.Add(card1);
            targetHand.MutableCards.Add(card2);
        }

        var initialTargetHandCount = target.HandZone.Cards.Count;
        var initialTargetHealth = target.CurrentHealth;

        // Create Slash card
        var slash = CreateSlashCard(1);

        // Set services on skill (auto-trigger)
        if (iceSwordSkill is IceSwordSkill iceSword)
        {
            iceSword.SetCardMoveService(cardMoveService);
        }

        skillManager.AddEquipmentSkill(game, attacker, iceSwordSkill);

        // Create damage descriptor for Slash
        var damage = new DamageDescriptor(
            SourceSeat: attacker.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash",
            CausingCard: slash
        );

        // Create resolution stack and context
        var stack = new BasicResolutionStack();
        var ruleService = new RuleService();

        var damageContext = new ResolutionContext(
            game,
            attacker,
            null,
            null,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: damage,
            LogSink: null,
            GetPlayerChoice: null,
            IntermediateResults: null,
            EventBus: eventBus,
            LogCollector: null,
            SkillManager: skillManager,
            EquipmentSkillRegistry: equipmentSkillRegistry,
            JudgementService: null
        );

        // Act: Execute DamageResolver
        var damageResolver = new DamageResolver();
        var result = damageResolver.Resolve(damageContext);

        // Assert: Resolution should succeed
        Assert.IsTrue(result.Success, "Damage resolution should succeed");

        // Assert: Target's cards should be discarded
        Assert.IsTrue(target.HandZone.Cards.Count < initialTargetHandCount,
            "Target should have discarded cards");

        // Assert: Target's health should NOT decrease (damage was prevented)
        Assert.AreEqual(initialTargetHealth, target.CurrentHealth,
            "Target health should not change (damage was prevented)");
    }

    #endregion

    #region Attack Distance Tests

    /// <summary>
    /// Tests that IceSwordSkill sets attack distance to 2 when active.
    /// Input: 2-player game, attacker and defender, active ice sword skill.
    /// Expected: ModifyAttackDistance returns 2 (weapon's fixed range).
    /// </summary>
    [TestMethod]
    public void IceSwordSkillModifyAttackDistanceSetsDistanceToTwo()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var defender = game.Players[1];
        var skill = new IceSwordSkill();

        // Act
        var modified = skill.ModifyAttackDistance(1, game, attacker, defender);

        // Assert
        Assert.IsNotNull(modified);
        Assert.AreEqual(2, modified.Value);
    }

    /// <summary>
    /// Tests that IceSwordSkill does not modify distance when the owner (attacker) is not active.
    /// Input: 2-player game, attacker is dead (IsAlive = false), ice sword skill.
    /// Expected: ModifyAttackDistance returns null (no modification) when owner is not active.
    /// </summary>
    [TestMethod]
    public void IceSwordSkillModifyAttackDistanceWhenOwnerIsNotActiveReturnsNull()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var defender = game.Players[1];
        attacker.IsAlive = false; // Owner is not active
        var skill = new IceSwordSkill();

        // Act
        var modified = skill.ModifyAttackDistance(1, game, attacker, defender);

        // Assert
        Assert.IsNull(modified);
    }

    /// <summary>
    /// Tests that RangeRuleService correctly applies ice sword skill to set attack distance to 2.
    /// Input: 4-player game, attacker and defender with seat distance = 2, attacker has ice sword equipped and skill active.
    /// Expected: Base seat distance = 2, base attack distance = 1, but with ice sword attack distance = 2,
    /// so IsWithinAttackRange returns true (because 2 <= 2, so within range).
    /// </summary>
    [TestMethod]
    public void RangeRuleServiceWithIceSwordSetsAttackRangeToTwo()
    {
        // Arrange
        var game = CreateDefaultGame(4);
        var attacker = game.Players[0];
        var defender = game.Players[2]; // Player 2 is at distance 2 from player 0 in 4-player game
        
        // Equip ice sword to attacker
        var iceSword = CreateIceSwordCard();
        if (attacker.EquipmentZone is Zone equipmentZone)
        {
            equipmentZone.MutableCards.Add(iceSword);
        }

        // Setup skill manager and equipment skill registry
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("ice_sword", new IceSwordSkillFactory());
        
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.LoadSkillsForPlayer(game, attacker);
        
        var iceSwordSkill = equipmentSkillRegistry.GetSkillForEquipment("ice_sword");
        if (iceSwordSkill is not null)
        {
            skillManager.AddEquipmentSkill(game, attacker, iceSwordSkill);
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
        // Base attack distance should be 1, but with ice sword it becomes 2
        Assert.AreEqual(2, attackDistance);
        // With ice sword, attack distance is set to 2, so 2 <= 2 is true
        Assert.IsTrue(isWithinRange);
    }

    #endregion
}

