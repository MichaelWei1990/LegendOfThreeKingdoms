using System;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Skills.Hero;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests;

[TestClass]
public class YiJiTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
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

    [TestMethod]
    public void YiJiSkill_HasCorrectProperties()
    {
        // Arrange
        var skill = new YiJiSkill();

        // Assert
        Assert.AreEqual("yiji", skill.Id);
        Assert.AreEqual("遗计", skill.Name);
        Assert.AreEqual(SkillType.Trigger, skill.Type);
        Assert.AreEqual(SkillCapability.None, skill.Capabilities);
    }

    [TestMethod]
    public void YiJiSkill_TriggersOnAfterDamageEvent_ForOwner()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var owner = game.Players[0];
        var source = game.Players[1];

        var eventBus = new BasicEventBus();
        var skill = new YiJiSkill();
        skill.Attach(game, owner, eventBus);

        var cardMoveService = new BasicCardMoveService(eventBus);
        skill.SetCardMoveService(cardMoveService);

        // Add cards to draw pile
        var card1 = CreateTestCard(1);
        var card2 = CreateTestCard(2);
        ((Zone)game.DrawPile).MutableCards.Add(card1);
        ((Zone)game.DrawPile).MutableCards.Add(card2);

        // Setup getPlayerChoice to confirm activation
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
            if (request.ChoiceType == ChoiceType.SelectTargets)
            {
                // Select owner as target (1 target)
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: new[] { owner.Seat },
                    SelectedCardIds: null,
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
        skill.SetGetPlayerChoice(getPlayerChoice);

        var initialDrawPileCount = game.DrawPile.Cards.Count;
        var initialOwnerHandCount = owner.HandZone.Cards.Count;

        // Act
        var damage = new DamageDescriptor(
            SourceSeat: source.Seat,
            TargetSeat: owner.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Test"
        );
        var afterDamageEvent = new AfterDamageEvent(
            game,
            damage,
            PreviousHealth: owner.CurrentHealth,
            CurrentHealth: owner.CurrentHealth - 1
        );
        eventBus.Publish(afterDamageEvent);

        // Assert
        Assert.AreEqual(initialDrawPileCount - 2, game.DrawPile.Cards.Count, "2 cards should be drawn from draw pile");
        Assert.AreEqual(initialOwnerHandCount + 2, owner.HandZone.Cards.Count, "Owner should receive 2 cards");
    }

    [TestMethod]
    public void YiJiSkill_DoesNotTrigger_WhenTargetIsNotOwner()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var owner = game.Players[0];
        var target = game.Players[1];
        var source = game.Players[0];

        var eventBus = new BasicEventBus();
        var skill = new YiJiSkill();
        skill.Attach(game, owner, eventBus);

        var cardMoveService = new BasicCardMoveService(eventBus);
        skill.SetCardMoveService(cardMoveService);

        // Add cards to draw pile
        var card1 = CreateTestCard(1);
        var card2 = CreateTestCard(2);
        ((Zone)game.DrawPile).MutableCards.Add(card1);
        ((Zone)game.DrawPile).MutableCards.Add(card2);

        var initialDrawPileCount = game.DrawPile.Cards.Count;

        // Act
        var damage = new DamageDescriptor(
            SourceSeat: source.Seat,
            TargetSeat: target.Seat, // Target is NOT owner
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Test"
        );
        var afterDamageEvent = new AfterDamageEvent(
            game,
            damage,
            PreviousHealth: target.CurrentHealth,
            CurrentHealth: target.CurrentHealth - 1
        );
        eventBus.Publish(afterDamageEvent);

        // Assert
        Assert.AreEqual(initialDrawPileCount, game.DrawPile.Cards.Count, "No cards should be drawn (skill not triggered)");
    }

    [TestMethod]
    public void YiJiSkill_PlayerCanChooseNotToActivate()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var owner = game.Players[0];
        var source = game.Players[1];

        var eventBus = new BasicEventBus();
        var skill = new YiJiSkill();
        skill.Attach(game, owner, eventBus);

        var cardMoveService = new BasicCardMoveService(eventBus);
        skill.SetCardMoveService(cardMoveService);

        // Add cards to draw pile
        var card1 = CreateTestCard(1);
        var card2 = CreateTestCard(2);
        ((Zone)game.DrawPile).MutableCards.Add(card1);
        ((Zone)game.DrawPile).MutableCards.Add(card2);

        // Setup getPlayerChoice to decline activation
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: false // Decline
            );
        };
        skill.SetGetPlayerChoice(getPlayerChoice);

        var initialDrawPileCount = game.DrawPile.Cards.Count;

        // Act
        var damage = new DamageDescriptor(
            SourceSeat: source.Seat,
            TargetSeat: owner.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Test"
        );
        var afterDamageEvent = new AfterDamageEvent(
            game,
            damage,
            PreviousHealth: owner.CurrentHealth,
            CurrentHealth: owner.CurrentHealth - 1
        );
        eventBus.Publish(afterDamageEvent);

        // Assert
        Assert.AreEqual(initialDrawPileCount, game.DrawPile.Cards.Count, "No cards should be drawn (player declined)");
    }

    [TestMethod]
    public void YiJiSkill_TriggersMultipleTimes_ForMultipleDamagePoints()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var owner = game.Players[0];
        var source = game.Players[1];

        var eventBus = new BasicEventBus();
        var skill = new YiJiSkill();
        skill.Attach(game, owner, eventBus);

        var cardMoveService = new BasicCardMoveService(eventBus);
        skill.SetCardMoveService(cardMoveService);

        // Add enough cards to draw pile (2 cards per damage point, 2 damage = 4 cards)
        for (int i = 1; i <= 4; i++)
        {
            ((Zone)game.DrawPile).MutableCards.Add(CreateTestCard(i));
        }

        int activationCount = 0;
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            if (request.ChoiceType == ChoiceType.Confirm)
            {
                activationCount++;
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: true
                );
            }
            if (request.ChoiceType == ChoiceType.SelectTargets)
            {
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: new[] { owner.Seat },
                    SelectedCardIds: null,
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
        skill.SetGetPlayerChoice(getPlayerChoice);

        var initialDrawPileCount = game.DrawPile.Cards.Count;
        var initialOwnerHandCount = owner.HandZone.Cards.Count;

        // Act
        var damage = new DamageDescriptor(
            SourceSeat: source.Seat,
            TargetSeat: owner.Seat,
            Amount: 2, // 2 damage points
            Type: DamageType.Normal,
            Reason: "Test"
        );
        var afterDamageEvent = new AfterDamageEvent(
            game,
            damage,
            PreviousHealth: owner.CurrentHealth,
            CurrentHealth: owner.CurrentHealth - 2
        );
        eventBus.Publish(afterDamageEvent);

        // Assert
        Assert.AreEqual(2, activationCount, "YiJi should be activated twice (once per damage point)");
        Assert.AreEqual(initialDrawPileCount - 4, game.DrawPile.Cards.Count, "4 cards should be drawn (2 per damage point)");
        Assert.AreEqual(initialOwnerHandCount + 4, owner.HandZone.Cards.Count, "Owner should receive 4 cards (2 per damage point)");
    }

    [TestMethod]
    public void YiJiSkill_DistributesToTwoTargets()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        var owner = game.Players[0];
        var target1 = game.Players[1];
        var target2 = game.Players[2];
        var source = game.Players[1];

        var eventBus = new BasicEventBus();
        var skill = new YiJiSkill();
        skill.Attach(game, owner, eventBus);

        var cardMoveService = new BasicCardMoveService(eventBus);
        skill.SetCardMoveService(cardMoveService);

        // Add cards to draw pile
        var card1 = CreateTestCard(1);
        var card2 = CreateTestCard(2);
        ((Zone)game.DrawPile).MutableCards.Add(card1);
        ((Zone)game.DrawPile).MutableCards.Add(card2);

        int cardSelectionCount = 0;
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
            if (request.ChoiceType == ChoiceType.SelectTargets)
            {
                // Select 2 targets
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: new[] { target1.Seat, target2.Seat },
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: null
                );
            }
            if (request.ChoiceType == ChoiceType.SelectCards)
            {
                // Select first card to give to target1
                cardSelectionCount++;
                var selectedCard = request.AllowedCards?.FirstOrDefault();
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: selectedCard != null ? new[] { selectedCard.Id } : null,
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
        skill.SetGetPlayerChoice(getPlayerChoice);

        var initialTarget1HandCount = target1.HandZone.Cards.Count;
        var initialTarget2HandCount = target2.HandZone.Cards.Count;

        // Act
        var damage = new DamageDescriptor(
            SourceSeat: source.Seat,
            TargetSeat: owner.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Test"
        );
        var afterDamageEvent = new AfterDamageEvent(
            game,
            damage,
            PreviousHealth: owner.CurrentHealth,
            CurrentHealth: owner.CurrentHealth - 1
        );
        eventBus.Publish(afterDamageEvent);

        // Assert
        Assert.AreEqual(1, cardSelectionCount, "Player should be asked to select which card goes to which target");
        Assert.AreEqual(initialTarget1HandCount + 1, target1.HandZone.Cards.Count, "Target1 should receive 1 card");
        Assert.AreEqual(initialTarget2HandCount + 1, target2.HandZone.Cards.Count, "Target2 should receive 1 card");
        Assert.AreEqual(0, game.DrawPile.Cards.Count, "All cards should be distributed");
    }

    [TestMethod]
    public void YiJiSkill_DoesNotTrigger_WhenDrawPileHasLessThan2Cards()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var owner = game.Players[0];
        var source = game.Players[1];

        var eventBus = new BasicEventBus();
        var skill = new YiJiSkill();
        skill.Attach(game, owner, eventBus);

        var cardMoveService = new BasicCardMoveService(eventBus);
        skill.SetCardMoveService(cardMoveService);

        // Add only 1 card to draw pile
        var card1 = CreateTestCard(1);
        ((Zone)game.DrawPile).MutableCards.Add(card1);

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
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: false
            );
        };
        skill.SetGetPlayerChoice(getPlayerChoice);

        var initialDrawPileCount = game.DrawPile.Cards.Count;

        // Act
        var damage = new DamageDescriptor(
            SourceSeat: source.Seat,
            TargetSeat: owner.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Test"
        );
        var afterDamageEvent = new AfterDamageEvent(
            game,
            damage,
            PreviousHealth: owner.CurrentHealth,
            CurrentHealth: owner.CurrentHealth - 1
        );
        eventBus.Publish(afterDamageEvent);

        // Assert
        Assert.AreEqual(initialDrawPileCount, game.DrawPile.Cards.Count, "No cards should be drawn (not enough cards in draw pile)");
    }

    [TestMethod]
    public void YiJiSkill_CanGiveCardsToSelf()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var owner = game.Players[0];
        var source = game.Players[1];

        var eventBus = new BasicEventBus();
        var skill = new YiJiSkill();
        skill.Attach(game, owner, eventBus);

        var cardMoveService = new BasicCardMoveService(eventBus);
        skill.SetCardMoveService(cardMoveService);

        // Add cards to draw pile
        var card1 = CreateTestCard(1);
        var card2 = CreateTestCard(2);
        ((Zone)game.DrawPile).MutableCards.Add(card1);
        ((Zone)game.DrawPile).MutableCards.Add(card2);

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
            if (request.ChoiceType == ChoiceType.SelectTargets)
            {
                // Select owner (self) as target
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: new[] { owner.Seat },
                    SelectedCardIds: null,
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
        skill.SetGetPlayerChoice(getPlayerChoice);

        var initialOwnerHandCount = owner.HandZone.Cards.Count;

        // Act
        var damage = new DamageDescriptor(
            SourceSeat: source.Seat,
            TargetSeat: owner.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Test"
        );
        var afterDamageEvent = new AfterDamageEvent(
            game,
            damage,
            PreviousHealth: owner.CurrentHealth,
            CurrentHealth: owner.CurrentHealth - 1
        );
        eventBus.Publish(afterDamageEvent);

        // Assert
        Assert.AreEqual(initialOwnerHandCount + 2, owner.HandZone.Cards.Count, "Owner should receive 2 cards (can give to self)");
    }
}

