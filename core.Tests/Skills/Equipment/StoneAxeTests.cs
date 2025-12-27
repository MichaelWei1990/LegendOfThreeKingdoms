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
public sealed class StoneAxeTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Card CreateStoneAxeCard(int id = 1, string definitionId = "stone_axe", string name = "贯石斧")
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

    private static Card CreateDodgeCard(int id, Suit suit = Suit.Heart, int rank = 2)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"dodge_{id}",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Dodge,
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
    /// Tests that StoneAxeSkillFactory creates correct skill instance.
    /// Input: StoneAxeSkillFactory instance.
    /// Expected: Created skill has correct Id, Name, and SkillType.Trigger.
    /// </summary>
    [TestMethod]
    public void StoneAxeSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new StoneAxeSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("stone_axe", skill.Id);
        Assert.AreEqual("贯石", skill.Name);
        Assert.AreEqual(SkillType.Trigger, skill.Type);
    }

    /// <summary>
    /// Tests that EquipmentSkillRegistry can register and retrieve Stone Axe skill by DefinitionId.
    /// Input: Empty registry, StoneAxeSkillFactory, DefinitionId "stone_axe".
    /// Expected: After registration, GetSkillForEquipment returns a skill with correct Id.
    /// </summary>
    [TestMethod]
    public void EquipmentSkillRegistryRegisterStoneAxeSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new EquipmentSkillRegistry();
        var factory = new StoneAxeSkillFactory();

        // Act
        registry.RegisterEquipmentSkill("stone_axe", factory);
        var skill = registry.GetSkillForEquipment("stone_axe");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("stone_axe", skill.Id);
        Assert.AreEqual("贯石", skill.Name);
    }

    #endregion

    #region AfterSlashDodgedEvent Tests

    /// <summary>
    /// Tests that Stone Axe skill triggers when Slash is dodged and player has enough cards.
    /// Input: Game, attacker with Stone Axe skill uses Slash, target dodges, attacker has 2+ cards.
    /// Expected: AfterSlashDodgedEvent triggers skill, player can discard 2 cards to force 1 damage.
    /// </summary>
    [TestMethod]
    public void StoneAxeSkillTriggersWhenSlashDodgedWithEnoughCards()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var target = game.Players[1];

        var eventBus = new BasicEventBus();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("stone_axe", new StoneAxeSkillFactory());
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);

        var stoneAxeSkill = equipmentSkillRegistry.GetSkillForEquipment("stone_axe");
        var cardMoveService = new BasicCardMoveService(eventBus);

        // Set services on skill
        if (stoneAxeSkill is StoneAxeSkill stoneAxe)
        {
            stoneAxe.SetCardMoveService(cardMoveService);
        }

        skillManager.AddEquipmentSkill(game, attacker, stoneAxeSkill);

        // Add cards to attacker's hand (at least 2 for discarding)
        var card1 = CreateTestCard(100);
        var card2 = CreateTestCard(101);
        if (attacker.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(card1);
            handZone.MutableCards.Add(card2);
        }

        var initialAttackerHandCount = attacker.HandZone.Cards.Count;
        var initialTargetHealth = target.CurrentHealth;

        // Create Slash card
        var slash = CreateSlashCard(1);
        if (attacker.HandZone is Zone attackerHand)
        {
            attackerHand.MutableCards.Add(slash);
        }

        // Create Dodge card for target
        var dodge = CreateDodgeCard(2);
        if (target.HandZone is Zone targetHand)
        {
            targetHand.MutableCards.Add(dodge);
        }

        // Act: Publish AfterSlashDodgedEvent
        var damage = new DamageDescriptor(
            SourceSeat: attacker.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash",
            CausingCard: slash
        );
        var afterSlashDodgedEvent = new AfterSlashDodgedEvent(
            game,
            AttackerSeat: attacker.Seat,
            TargetSeat: target.Seat,
            SlashCard: slash,
            OriginalDamage: damage
        );

        // Auto-trigger: skill will automatically activate if getPlayerChoice is null
        eventBus.Publish(afterSlashDodgedEvent);

        // Assert: Cards should be discarded and damage should be applied
        // Note: Since we don't have getPlayerChoice, the skill will auto-select first 2 cards
        Assert.IsTrue(attacker.HandZone.Cards.Count < initialAttackerHandCount,
            "Attacker should have discarded cards");
        Assert.AreEqual(initialTargetHealth - 1, target.CurrentHealth,
            "Target should take 1 damage from forced Stone Axe effect");
    }

    /// <summary>
    /// Tests that Stone Axe skill does not trigger when attacker has less than 2 cards.
    /// Input: Game, attacker with Stone Axe skill uses Slash, target dodges, attacker has < 2 cards.
    /// Expected: Skill does not trigger, no damage is applied.
    /// </summary>
    [TestMethod]
    public void StoneAxeSkillDoesNotTriggerWhenInsufficientCards()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var target = game.Players[1];

        var eventBus = new BasicEventBus();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("stone_axe", new StoneAxeSkillFactory());
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);

        var stoneAxeSkill = equipmentSkillRegistry.GetSkillForEquipment("stone_axe");
        var cardMoveService = new BasicCardMoveService(eventBus);

        // Set services on skill
        if (stoneAxeSkill is StoneAxeSkill stoneAxe)
        {
            stoneAxe.SetCardMoveService(cardMoveService);
        }

        skillManager.AddEquipmentSkill(game, attacker, stoneAxeSkill);

        // Add only 1 card to attacker's hand (not enough for Stone Axe)
        var card1 = CreateTestCard(100);
        if (attacker.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(card1);
        }

        // Create Slash card (in real game, this would be in discard pile after use)
        var slash = CreateSlashCard(1);
        // Move slash to discard pile to simulate it being used
        if (game.DiscardPile is Zone discardPile)
        {
            discardPile.MutableCards.Add(slash);
        }

        // Record initial counts (only card1 should be in hand, slash is in discard pile)
        var initialAttackerHandCount = attacker.HandZone.Cards.Count;
        var initialTargetHealth = target.CurrentHealth;

        // Act: Publish AfterSlashDodgedEvent
        var damage = new DamageDescriptor(
            SourceSeat: attacker.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash",
            CausingCard: slash
        );
        var afterSlashDodgedEvent = new AfterSlashDodgedEvent(
            game,
            AttackerSeat: attacker.Seat,
            TargetSeat: target.Seat,
            SlashCard: slash,
            OriginalDamage: damage
        );

        eventBus.Publish(afterSlashDodgedEvent);

        // Assert: No cards should be discarded and no damage should be applied
        Assert.AreEqual(initialAttackerHandCount, attacker.HandZone.Cards.Count,
            "Attacker should not have discarded cards (insufficient cards)");
        Assert.AreEqual(initialTargetHealth, target.CurrentHealth,
            "Target should not take damage (skill did not trigger)");
    }

    /// <summary>
    /// Tests that Stone Axe skill does not trigger when attacker is not the owner.
    /// Input: Game, player A with Stone Axe skill, player B uses Slash, target dodges.
    /// Expected: Skill does not trigger for player A (not the attacker).
    /// </summary>
    [TestMethod]
    public void StoneAxeSkillDoesNotTriggerWhenNotAttacker()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        var playerA = game.Players[0]; // Has Stone Axe skill
        var playerB = game.Players[1]; // Uses Slash
        var target = game.Players[2];

        var eventBus = new BasicEventBus();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("stone_axe", new StoneAxeSkillFactory());
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);

        var stoneAxeSkill = equipmentSkillRegistry.GetSkillForEquipment("stone_axe");
        var cardMoveService = new BasicCardMoveService(eventBus);

        // Set services on skill
        if (stoneAxeSkill is StoneAxeSkill stoneAxe)
        {
            stoneAxe.SetCardMoveService(cardMoveService);
        }

        // Add skill to player A (not the attacker)
        skillManager.AddEquipmentSkill(game, playerA, stoneAxeSkill);

        // Add cards to player A's hand
        var card1 = CreateTestCard(100);
        var card2 = CreateTestCard(101);
        if (playerA.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(card1);
            handZone.MutableCards.Add(card2);
        }

        var initialPlayerAHandCount = playerA.HandZone.Cards.Count;
        var initialTargetHealth = target.CurrentHealth;

        // Create Slash card used by player B
        var slash = CreateSlashCard(1);
        if (playerB.HandZone is Zone playerBHand)
        {
            playerBHand.MutableCards.Add(slash);
        }

        // Act: Publish AfterSlashDodgedEvent with player B as attacker
        var damage = new DamageDescriptor(
            SourceSeat: playerB.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash",
            CausingCard: slash
        );
        var afterSlashDodgedEvent = new AfterSlashDodgedEvent(
            game,
            AttackerSeat: playerB.Seat, // Player B is the attacker, not player A
            TargetSeat: target.Seat,
            SlashCard: slash,
            OriginalDamage: damage
        );

        eventBus.Publish(afterSlashDodgedEvent);

        // Assert: Player A's skill should not trigger
        Assert.AreEqual(initialPlayerAHandCount, playerA.HandZone.Cards.Count,
            "Player A should not have discarded cards (not the attacker)");
        Assert.AreEqual(initialTargetHealth, target.CurrentHealth,
            "Target should not take damage (skill did not trigger)");
    }

    /// <summary>
    /// Tests that Stone Axe skill allows player to choose cards to discard.
    /// Input: Game, attacker with Stone Axe skill uses Slash, target dodges, getPlayerChoice provided.
    /// Expected: Player can choose which 2 cards to discard, then damage is applied.
    /// </summary>
    [TestMethod]
    public void StoneAxeSkillAllowsPlayerToChooseCardsToDiscard()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var target = game.Players[1];

        var eventBus = new BasicEventBus();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("stone_axe", new StoneAxeSkillFactory());
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);

        var stoneAxeSkill = equipmentSkillRegistry.GetSkillForEquipment("stone_axe");
        var cardMoveService = new BasicCardMoveService(eventBus);

        // Add cards to attacker's hand
        var card1 = CreateTestCard(100);
        var card2 = CreateTestCard(101);
        var card3 = CreateTestCard(102);
        if (attacker.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(card1);
            handZone.MutableCards.Add(card2);
            handZone.MutableCards.Add(card3);
        }

        var initialAttackerHandCount = attacker.HandZone.Cards.Count;
        var initialTargetHealth = target.CurrentHealth;

        // Create Slash card
        var slash = CreateSlashCard(1);
        if (attacker.HandZone is Zone attackerHand)
        {
            attackerHand.MutableCards.Add(slash);
        }

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
        if (stoneAxeSkill is StoneAxeSkill stoneAxe)
        {
            stoneAxe.SetCardMoveService(cardMoveService);
            stoneAxe.SetGetPlayerChoice(getPlayerChoice);
        }

        skillManager.AddEquipmentSkill(game, attacker, stoneAxeSkill);

        // Act: Publish AfterSlashDodgedEvent
        var damage = new DamageDescriptor(
            SourceSeat: attacker.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash",
            CausingCard: slash
        );
        var afterSlashDodgedEvent = new AfterSlashDodgedEvent(
            game,
            AttackerSeat: attacker.Seat,
            TargetSeat: target.Seat,
            SlashCard: slash,
            OriginalDamage: damage
        );

        eventBus.Publish(afterSlashDodgedEvent);

        // Assert: Confirm request should be made
        Assert.IsNotNull(lastConfirmRequest, "Confirm request should be made");
        Assert.AreEqual(attacker.Seat, lastConfirmRequest.PlayerSeat);

        // Assert: Select cards request should be made
        Assert.IsNotNull(lastSelectRequest, "Select cards request should be made");
        Assert.AreEqual(ChoiceType.SelectCards, lastSelectRequest.ChoiceType);
        Assert.IsTrue(lastSelectRequest.AllowedCards?.Count >= 2, "At least 2 cards should be available for selection");

        // Assert: Cards should be discarded and damage should be applied
        Assert.IsTrue(attacker.HandZone.Cards.Count < initialAttackerHandCount,
            "Attacker should have discarded cards");
        Assert.AreEqual(initialTargetHealth - 1, target.CurrentHealth,
            "Target should take 1 damage from forced Stone Axe effect");
    }

    /// <summary>
    /// Tests that Stone Axe skill does not trigger when player chooses not to activate.
    /// Input: Game, attacker with Stone Axe skill uses Slash, target dodges, player chooses not to activate.
    /// Expected: No cards discarded, no damage applied.
    /// </summary>
    [TestMethod]
    public void StoneAxeSkillDoesNotTriggerWhenPlayerChoosesNotToActivate()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var target = game.Players[1];

        var eventBus = new BasicEventBus();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("stone_axe", new StoneAxeSkillFactory());
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);

        var stoneAxeSkill = equipmentSkillRegistry.GetSkillForEquipment("stone_axe");
        var cardMoveService = new BasicCardMoveService(eventBus);

        // Add cards to attacker's hand
        var card1 = CreateTestCard(100);
        var card2 = CreateTestCard(101);
        if (attacker.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(card1);
            handZone.MutableCards.Add(card2);
        }

        // Create Slash card
        var slash = CreateSlashCard(1);
        if (attacker.HandZone is Zone attackerHand)
        {
            attackerHand.MutableCards.Add(slash);
        }

        // Record initial counts after adding all cards
        var initialAttackerHandCount = attacker.HandZone.Cards.Count;
        var initialTargetHealth = target.CurrentHealth;

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
        if (stoneAxeSkill is StoneAxeSkill stoneAxe)
        {
            stoneAxe.SetCardMoveService(cardMoveService);
            stoneAxe.SetGetPlayerChoice(getPlayerChoice);
        }

        skillManager.AddEquipmentSkill(game, attacker, stoneAxeSkill);

        // Act: Publish AfterSlashDodgedEvent
        var damage = new DamageDescriptor(
            SourceSeat: attacker.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash",
            CausingCard: slash
        );
        var afterSlashDodgedEvent = new AfterSlashDodgedEvent(
            game,
            AttackerSeat: attacker.Seat,
            TargetSeat: target.Seat,
            SlashCard: slash,
            OriginalDamage: damage
        );

        eventBus.Publish(afterSlashDodgedEvent);

        // Assert: No cards should be discarded and no damage should be applied
        Assert.AreEqual(initialAttackerHandCount, attacker.HandZone.Cards.Count,
            "Attacker should not have discarded cards (player declined)");
        Assert.AreEqual(initialTargetHealth, target.CurrentHealth,
            "Target should not take damage (skill not activated)");
    }

    /// <summary>
    /// Tests that Stone Axe skill can discard equipment cards.
    /// Input: Game, attacker with Stone Axe skill uses Slash, target dodges, attacker has equipment cards.
    /// Expected: Player can discard equipment cards, damage is applied.
    /// </summary>
    [TestMethod]
    public void StoneAxeSkillCanDiscardEquipmentCards()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var target = game.Players[1];

        var eventBus = new BasicEventBus();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("stone_axe", new StoneAxeSkillFactory());
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);

        var stoneAxeSkill = equipmentSkillRegistry.GetSkillForEquipment("stone_axe");
        var cardMoveService = new BasicCardMoveService(eventBus);

        // Add equipment cards to attacker
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

        if (attacker.EquipmentZone is Zone equipmentZone)
        {
            equipmentZone.MutableCards.Add(equipment1);
            equipmentZone.MutableCards.Add(equipment2);
        }

        var initialEquipmentCount = attacker.EquipmentZone.Cards.Count;
        var initialTargetHealth = target.CurrentHealth;

        // Create Slash card
        var slash = CreateSlashCard(1);
        if (attacker.HandZone is Zone attackerHand)
        {
            attackerHand.MutableCards.Add(slash);
        }

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
        if (stoneAxeSkill is StoneAxeSkill stoneAxe)
        {
            stoneAxe.SetCardMoveService(cardMoveService);
            stoneAxe.SetGetPlayerChoice(getPlayerChoice);
        }

        skillManager.AddEquipmentSkill(game, attacker, stoneAxeSkill);

        // Act: Publish AfterSlashDodgedEvent
        var damage = new DamageDescriptor(
            SourceSeat: attacker.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash",
            CausingCard: slash
        );
        var afterSlashDodgedEvent = new AfterSlashDodgedEvent(
            game,
            AttackerSeat: attacker.Seat,
            TargetSeat: target.Seat,
            SlashCard: slash,
            OriginalDamage: damage
        );

        eventBus.Publish(afterSlashDodgedEvent);

        // Assert: Equipment cards should be discarded and damage should be applied
        Assert.IsTrue(attacker.EquipmentZone.Cards.Count < initialEquipmentCount,
            "Attacker should have discarded equipment cards");
        Assert.AreEqual(initialTargetHealth - 1, target.CurrentHealth,
            "Target should take 1 damage from forced Stone Axe effect");
    }

    /// <summary>
    /// Tests that Stone Axe forced damage has correct CausingCard reference.
    /// Input: Game, attacker with Stone Axe skill uses Slash, target dodges, skill activates.
    /// Expected: Forced damage has CausingCard set to the original Slash card.
    /// </summary>
    [TestMethod]
    public void StoneAxeForcedDamageHasCorrectCausingCard()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var target = game.Players[1];

        var eventBus = new BasicEventBus();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("stone_axe", new StoneAxeSkillFactory());
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);

        var stoneAxeSkill = equipmentSkillRegistry.GetSkillForEquipment("stone_axe");
        var cardMoveService = new BasicCardMoveService(eventBus);

        // Add cards to attacker's hand
        var card1 = CreateTestCard(100);
        var card2 = CreateTestCard(101);
        if (attacker.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(card1);
            handZone.MutableCards.Add(card2);
        }

        // Create Slash card
        var slash = CreateSlashCard(1);
        if (attacker.HandZone is Zone attackerHand)
        {
            attackerHand.MutableCards.Add(slash);
        }

        // Track damage events to verify CausingCard
        DamageDescriptor? lastDamage = null;
        eventBus.Subscribe<DamageCreatedEvent>(evt =>
        {
            lastDamage = evt.Damage;
        });

        // Set services on skill (auto-trigger)
        if (stoneAxeSkill is StoneAxeSkill stoneAxe)
        {
            stoneAxe.SetCardMoveService(cardMoveService);
        }

        skillManager.AddEquipmentSkill(game, attacker, stoneAxeSkill);

        // Act: Publish AfterSlashDodgedEvent
        var damage = new DamageDescriptor(
            SourceSeat: attacker.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash",
            CausingCard: slash
        );
        var afterSlashDodgedEvent = new AfterSlashDodgedEvent(
            game,
            AttackerSeat: attacker.Seat,
            TargetSeat: target.Seat,
            SlashCard: slash,
            OriginalDamage: damage
        );

        eventBus.Publish(afterSlashDodgedEvent);

        // Assert: Forced damage should have CausingCard set to the original Slash
        Assert.IsNotNull(lastDamage, "Damage should be created");
        Assert.AreEqual(slash.Id, lastDamage.CausingCard?.Id,
            "Forced damage should have CausingCard set to the original Slash card");
        Assert.AreEqual("StoneAxe", lastDamage.Reason,
            "Damage reason should be 'StoneAxe'");
        Assert.AreEqual(1, lastDamage.Amount,
            "Damage amount should be 1");
    }

    #endregion

    #region Attack Distance Tests

    /// <summary>
    /// Tests that StoneAxeSkill sets attack distance to 3 when active.
    /// Input: 2-player game, attacker and defender, active stone axe skill.
    /// Expected: ModifyAttackDistance returns 3 (weapon's fixed range).
    /// </summary>
    [TestMethod]
    public void StoneAxeSkillModifyAttackDistanceSetsDistanceToThree()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var defender = game.Players[1];
        var skill = new StoneAxeSkill();

        // Act
        var modified = skill.ModifyAttackDistance(1, game, attacker, defender);

        // Assert
        Assert.IsNotNull(modified);
        Assert.AreEqual(3, modified.Value);
    }

    /// <summary>
    /// Tests that StoneAxeSkill does not modify distance when the owner (attacker) is not active.
    /// Input: 2-player game, attacker is dead (IsAlive = false), stone axe skill.
    /// Expected: ModifyAttackDistance returns null (no modification) when owner is not active.
    /// </summary>
    [TestMethod]
    public void StoneAxeSkillModifyAttackDistanceWhenOwnerIsNotActiveReturnsNull()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var defender = game.Players[1];
        attacker.IsAlive = false; // Owner is not active
        var skill = new StoneAxeSkill();

        // Act
        var modified = skill.ModifyAttackDistance(1, game, attacker, defender);

        // Assert
        Assert.IsNull(modified);
    }

    /// <summary>
    /// Tests that RangeRuleService correctly applies stone axe skill to set attack distance to 3.
    /// Input: 4-player game, attacker and defender with seat distance = 3, attacker has stone axe equipped and skill active.
    /// Expected: Base seat distance = 3, base attack distance = 1, but with stone axe attack distance = 3,
    /// so IsWithinAttackRange returns true (because 3 <= 3, so within range).
    /// </summary>
    [TestMethod]
    public void RangeRuleServiceWithStoneAxeSetsAttackRangeToThree()
    {
        // Arrange
        var game = CreateDefaultGame(5);
        var attacker = game.Players[0];
        var defender = game.Players[3]; // Player 3 is at distance 2 from player 0 in 5-player game (clockwise: 3, counter-clockwise: 2, min: 2)
        
        // Equip stone axe to attacker
        var stoneAxe = CreateStoneAxeCard();
        if (attacker.EquipmentZone is Zone equipmentZone)
        {
            equipmentZone.MutableCards.Add(stoneAxe);
        }

        // Setup skill manager and equipment skill registry
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("stone_axe", new StoneAxeSkillFactory());
        
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.LoadSkillsForPlayer(game, attacker);
        
        var stoneAxeSkill = equipmentSkillRegistry.GetSkillForEquipment("stone_axe");
        if (stoneAxeSkill is not null)
        {
            skillManager.AddEquipmentSkill(game, attacker, stoneAxeSkill);
        }

        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var rangeRuleService = new RangeRuleService(modifierProvider);

        // Act
        var seatDistance = rangeRuleService.GetSeatDistance(game, attacker, defender);
        var attackDistance = rangeRuleService.GetAttackDistance(game, attacker, defender);
        var isWithinRange = rangeRuleService.IsWithinAttackRange(game, attacker, defender);

        // Assert
        // Base seat distance should be 2 (non-adjacent players in 5-player game)
        Assert.AreEqual(2, seatDistance);
        // Base attack distance should be 1, but with stone axe it becomes 3
        Assert.AreEqual(3, attackDistance);
        // With stone axe, attack distance is set to 3, so 2 <= 3 is true
        Assert.IsTrue(isWithinRange);
    }

    #endregion
}

