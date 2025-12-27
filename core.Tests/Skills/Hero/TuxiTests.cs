using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Phases;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Skills.Hero;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests;

[TestClass]
public sealed class TuxiTests
{
    private static Game CreateDefaultGame(int playerCount = 3)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Game CreateGameWithCardsInDrawPile(int playerCount = 3, int cardCount = 10)
    {
        var game = CreateDefaultGame(playerCount);

        // Add cards to draw pile
        if (game.DrawPile is Zone drawZone)
        {
            for (int i = 0; i < cardCount; i++)
            {
                var card = new Card
                {
                    Id = i + 1,
                    DefinitionId = $"test_card_{i}",
                    CardType = CardType.Basic,
                    CardSubType = CardSubType.Slash,
                    Suit = Suit.Spade,
                    Rank = 5
                };
                drawZone.MutableCards.Add(card);
            }
        }

        return game;
    }

    private static Card CreateTestCard(int id, Suit suit, int rank)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"card_{id}",
            Name = $"Card {id}",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = suit,
            Rank = rank
        };
    }

    #region Skill Factory Tests

    /// <summary>
    /// Tests that TuxiSkillFactory creates correct skill instance.
    /// Input: TuxiSkillFactory instance.
    /// Expected: Created skill has correct Id, Name, and SkillType.Trigger.
    /// </summary>
    [TestMethod]
    public void TuxiSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new TuxiSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("tuxi", skill.Id);
        Assert.AreEqual("突袭", skill.Name);
        Assert.AreEqual(SkillType.Trigger, skill.Type);
    }

    /// <summary>
    /// Tests that SkillRegistry can register and retrieve Tuxi skill.
    /// Input: Empty registry, TuxiSkillFactory.
    /// Expected: After registration, GetSkill returns a skill with correct Id.
    /// </summary>
    [TestMethod]
    public void SkillRegistryRegisterTuxiSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new TuxiSkillFactory();

        // Act
        registry.RegisterSkill("tuxi", factory);
        var skill = registry.GetSkill("tuxi");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("tuxi", skill.Id);
        Assert.AreEqual("突袭", skill.Name);
    }

    #endregion

    #region Skill Properties Tests

    /// <summary>
    /// Tests that TuxiSkill has correct properties.
    /// Input: TuxiSkill instance.
    /// Expected: Skill has correct Id, Name, Type, and Capabilities.
    /// </summary>
    [TestMethod]
    public void TuxiSkillHasCorrectProperties()
    {
        // Arrange
        var skill = new TuxiSkill();

        // Act & Assert
        Assert.AreEqual("tuxi", skill.Id);
        Assert.AreEqual("突袭", skill.Name);
        Assert.AreEqual(SkillType.Trigger, skill.Type);
        Assert.AreEqual(SkillCapability.None, skill.Capabilities);
    }

    #endregion

    #region Draw Phase Replacement Tests

    /// <summary>
    /// Tests that TuxiSkill can replace draw phase when owner is alive.
    /// Input: Game, alive player with Tuxi skill.
    /// Expected: CanReplaceDrawPhase returns true.
    /// </summary>
    [TestMethod]
    public void TuxiSkillCanReplaceDrawPhaseWhenOwnerIsAlive()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        var player = game.Players[0];
        player.IsAlive = true;
        var skill = new TuxiSkill();

        // Act
        var canReplace = skill.CanReplaceDrawPhase(game, player);

        // Assert
        Assert.IsTrue(canReplace);
    }

    /// <summary>
    /// Tests that TuxiSkill cannot replace draw phase when owner is dead.
    /// Input: Game, dead player with Tuxi skill.
    /// Expected: CanReplaceDrawPhase returns false.
    /// </summary>
    [TestMethod]
    public void TuxiSkillCannotReplaceDrawPhaseWhenOwnerIsDead()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        var player = game.Players[0];
        player.IsAlive = false;
        var skill = new TuxiSkill();

        // Act
        var canReplace = skill.CanReplaceDrawPhase(game, player);

        // Assert
        Assert.IsFalse(canReplace);
    }

    /// <summary>
    /// Tests that TuxiSkill replaces draw phase and obtains cards from targets.
    /// Input: Game with 3 players, player1 with Tuxi skill, player2 and player3 with hand cards.
    /// Expected: After draw phase, player1 obtains one card from each target, normal draw is skipped.
    /// </summary>
    [TestMethod]
    public void TuxiSkillReplacesDrawPhaseAndObtainsCardsFromTargets()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(3, 10);
        var tuxiPlayer = game.Players[0];
        var target1 = game.Players[1];
        var target2 = game.Players[2];
        tuxiPlayer.IsAlive = true;
        target1.IsAlive = true;
        target2.IsAlive = true;

        // Add hand cards to targets
        var card1 = CreateTestCard(100, Suit.Heart, 5);
        var card2 = CreateTestCard(101, Suit.Diamond, 6);
        var card3 = CreateTestCard(102, Suit.Club, 7);
        if (target1.HandZone is Zone handZone1)
        {
            handZone1.MutableCards.Add(card1);
            handZone1.MutableCards.Add(card2);
        }
        if (target2.HandZone is Zone handZone2)
        {
            handZone2.MutableCards.Add(card3);
        }

        var initialHandCount = tuxiPlayer.HandZone.Cards.Count;
        var target1InitialCount = target1.HandZone.Cards.Count;
        var target2InitialCount = target2.HandZone.Cards.Count;

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var eventBus = new BasicEventBus();
        var stack = new BasicResolutionStack();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.AddEquipmentSkill(game, tuxiPlayer, new TuxiSkill());

        // Setup getPlayerChoice to confirm and select targets
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = request =>
        {
            if (request.ChoiceType == ChoiceType.Confirm && request.PlayerSeat == tuxiPlayer.Seat)
            {
                return new ChoiceResult(request.RequestId, request.PlayerSeat, null, null, null, true);
            }
            if (request.ChoiceType == ChoiceType.SelectTargets && request.PlayerSeat == tuxiPlayer.Seat)
            {
                return new ChoiceResult(request.RequestId, request.PlayerSeat, new[] { target1.Seat, target2.Seat }, null, null, null);
            }
            return new ChoiceResult(request.RequestId, request.PlayerSeat, null, null, null, null);
        };

        var drawPhaseService = new DrawPhaseService(cardMoveService, ruleService, eventBus, stack, skillManager, getPlayerChoice);

        // Act - Trigger draw phase
        var phaseStartEvent = new PhaseStartEvent(game, tuxiPlayer.Seat, Phase.Draw);
        eventBus.Publish(phaseStartEvent);

        // Execute resolution stack
        while (!stack.IsEmpty)
        {
            var result = stack.Pop();
            Assert.IsTrue(result.Success, "Resolution should succeed.");
        }

        // Assert
        // Tuxi player should have obtained 2 cards (one from each target)
        Assert.AreEqual(initialHandCount + 2, tuxiPlayer.HandZone.Cards.Count, "Tuxi player should have 2 more cards.");
        // Target1 should have lost 1 card
        Assert.AreEqual(target1InitialCount - 1, target1.HandZone.Cards.Count, "Target1 should have lost 1 card.");
        // Target2 should have lost 1 card
        Assert.AreEqual(target2InitialCount - 1, target2.HandZone.Cards.Count, "Target2 should have lost 1 card.");
        // Tuxi player should not have drawn from draw pile (normal draw is replaced)
        // We verify this by checking that the draw pile still has cards (if normal draw happened, it would have drawn 2)
        // Actually, we can't easily verify this without tracking draw pile size, so we just verify the hand count
    }

    /// <summary>
    /// Tests that TuxiSkill does not replace draw phase when player chooses not to activate.
    /// Input: Game with 3 players, player1 with Tuxi skill, player chooses not to activate.
    /// Expected: Normal draw phase executes (player draws 2 cards).
    /// </summary>
    [TestMethod]
    public void TuxiSkillDoesNotReplaceDrawPhaseWhenPlayerChoosesNotToActivate()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(3, 10);
        var tuxiPlayer = game.Players[0];
        tuxiPlayer.IsAlive = true;

        var initialHandCount = tuxiPlayer.HandZone.Cards.Count;

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var eventBus = new BasicEventBus();
        var stack = new BasicResolutionStack();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.AddEquipmentSkill(game, tuxiPlayer, new TuxiSkill());

        // Setup getPlayerChoice to decline activation
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = request =>
        {
            if (request.ChoiceType == ChoiceType.Confirm && request.PlayerSeat == tuxiPlayer.Seat)
            {
                return new ChoiceResult(request.RequestId, request.PlayerSeat, null, null, null, false);
            }
            return new ChoiceResult(request.RequestId, request.PlayerSeat, null, null, null, null);
        };

        var drawPhaseService = new DrawPhaseService(cardMoveService, ruleService, eventBus, stack, skillManager, getPlayerChoice);

        // Act - Trigger draw phase
        var phaseStartEvent = new PhaseStartEvent(game, tuxiPlayer.Seat, Phase.Draw);
        eventBus.Publish(phaseStartEvent);

        // Execute resolution stack
        while (!stack.IsEmpty)
        {
            var result = stack.Pop();
            Assert.IsTrue(result.Success, "Resolution should succeed.");
        }

        // Assert - Normal draw should have happened (2 cards)
        Assert.AreEqual(initialHandCount + 2, tuxiPlayer.HandZone.Cards.Count, "Player should have drawn 2 cards normally.");
    }

    /// <summary>
    /// Tests that TuxiSkill skips targets with no hand cards.
    /// Input: Game with 3 players, player1 with Tuxi skill, player2 has hand cards, player3 has no hand cards.
    /// Expected: Tuxi obtains card only from player2, player3 is skipped.
    /// </summary>
    [TestMethod]
    public void TuxiSkillSkipsTargetsWithNoHandCards()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(3, 10);
        var tuxiPlayer = game.Players[0];
        var target1 = game.Players[1];
        var target2 = game.Players[2];
        tuxiPlayer.IsAlive = true;
        target1.IsAlive = true;
        target2.IsAlive = true;

        // Add hand card only to target1
        var card1 = CreateTestCard(100, Suit.Heart, 5);
        if (target1.HandZone is Zone handZone1)
        {
            handZone1.MutableCards.Add(card1);
        }
        // target2 has no hand cards

        var initialHandCount = tuxiPlayer.HandZone.Cards.Count;
        var target1InitialCount = target1.HandZone.Cards.Count;

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var eventBus = new BasicEventBus();
        var stack = new BasicResolutionStack();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.AddEquipmentSkill(game, tuxiPlayer, new TuxiSkill());

        // Setup getPlayerChoice to select both targets (but target2 has no cards)
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = request =>
        {
            if (request.ChoiceType == ChoiceType.Confirm && request.PlayerSeat == tuxiPlayer.Seat)
            {
                return new ChoiceResult(request.RequestId, request.PlayerSeat, null, null, null, true);
            }
            if (request.ChoiceType == ChoiceType.SelectTargets && request.PlayerSeat == tuxiPlayer.Seat)
            {
                return new ChoiceResult(request.RequestId, request.PlayerSeat, new[] { target1.Seat, target2.Seat }, null, null, null);
            }
            return new ChoiceResult(request.RequestId, request.PlayerSeat, null, null, null, null);
        };

        var drawPhaseService = new DrawPhaseService(cardMoveService, ruleService, eventBus, stack, skillManager, getPlayerChoice);

        // Act - Trigger draw phase
        var phaseStartEvent = new PhaseStartEvent(game, tuxiPlayer.Seat, Phase.Draw);
        eventBus.Publish(phaseStartEvent);

        // Execute resolution stack
        while (!stack.IsEmpty)
        {
            var result = stack.Pop();
            Assert.IsTrue(result.Success, "Resolution should succeed.");
        }

        // Assert
        // Tuxi player should have obtained 1 card (only from target1, target2 skipped)
        Assert.AreEqual(initialHandCount + 1, tuxiPlayer.HandZone.Cards.Count, "Tuxi player should have 1 more card.");
        // Target1 should have lost 1 card
        Assert.AreEqual(target1InitialCount - 1, target1.HandZone.Cards.Count, "Target1 should have lost 1 card.");
        // Target2 should still have 0 cards (was skipped)
        Assert.AreEqual(0, target2.HandZone.Cards.Count, "Target2 should still have 0 cards.");
    }

    /// <summary>
    /// Tests that TuxiSkill allows selecting 0 targets (equivalent to not using Tuxi).
    /// Input: Game with 3 players, player1 with Tuxi skill, player chooses to activate but selects 0 targets.
    /// Expected: Normal draw phase executes (player draws 2 cards).
    /// </summary>
    [TestMethod]
    public void TuxiSkillAllowsSelectingZeroTargets()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(3, 10);
        var tuxiPlayer = game.Players[0];
        tuxiPlayer.IsAlive = true;

        var initialHandCount = tuxiPlayer.HandZone.Cards.Count;

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var eventBus = new BasicEventBus();
        var stack = new BasicResolutionStack();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.AddEquipmentSkill(game, tuxiPlayer, new TuxiSkill());

        // Setup getPlayerChoice to confirm but select 0 targets
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = request =>
        {
            if (request.ChoiceType == ChoiceType.Confirm && request.PlayerSeat == tuxiPlayer.Seat)
            {
                return new ChoiceResult(request.RequestId, request.PlayerSeat, null, null, null, true);
            }
            if (request.ChoiceType == ChoiceType.SelectTargets && request.PlayerSeat == tuxiPlayer.Seat)
            {
                return new ChoiceResult(request.RequestId, request.PlayerSeat, Array.Empty<int>(), null, null, null);
            }
            return new ChoiceResult(request.RequestId, request.PlayerSeat, null, null, null, null);
        };

        var drawPhaseService = new DrawPhaseService(cardMoveService, ruleService, eventBus, stack, skillManager, getPlayerChoice);

        // Act - Trigger draw phase
        var phaseStartEvent = new PhaseStartEvent(game, tuxiPlayer.Seat, Phase.Draw);
        eventBus.Publish(phaseStartEvent);

        // Execute resolution stack
        while (!stack.IsEmpty)
        {
            var result = stack.Pop();
            Assert.IsTrue(result.Success, "Resolution should succeed.");
        }

        // Assert - Normal draw should have happened (2 cards)
        Assert.AreEqual(initialHandCount + 2, tuxiPlayer.HandZone.Cards.Count, "Player should have drawn 2 cards normally.");
    }

    /// <summary>
    /// Tests that TuxiSkill publishes DrawPhaseReplacedEvent when activated.
    /// Input: Game with 3 players, player1 with Tuxi skill, player activates Tuxi.
    /// Expected: DrawPhaseReplacedEvent is published with correct information.
    /// </summary>
    [TestMethod]
    public void TuxiSkillPublishesDrawPhaseReplacedEvent()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(3, 10);
        var tuxiPlayer = game.Players[0];
        var target1 = game.Players[1];
        var target2 = game.Players[2];
        tuxiPlayer.IsAlive = true;
        target1.IsAlive = true;
        target2.IsAlive = true;

        // Add hand cards to targets
        var card1 = CreateTestCard(100, Suit.Heart, 5);
        var card2 = CreateTestCard(101, Suit.Diamond, 6);
        if (target1.HandZone is Zone handZone1)
        {
            handZone1.MutableCards.Add(card1);
        }
        if (target2.HandZone is Zone handZone2)
        {
            handZone2.MutableCards.Add(card2);
        }

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var eventBus = new BasicEventBus();
        var stack = new BasicResolutionStack();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.AddEquipmentSkill(game, tuxiPlayer, new TuxiSkill());

        DrawPhaseReplacedEvent? publishedEvent = null;
        eventBus.Subscribe<DrawPhaseReplacedEvent>(evt => publishedEvent = evt);

        // Setup getPlayerChoice to confirm and select targets
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = request =>
        {
            if (request.ChoiceType == ChoiceType.Confirm && request.PlayerSeat == tuxiPlayer.Seat)
            {
                return new ChoiceResult(request.RequestId, request.PlayerSeat, null, null, null, true);
            }
            if (request.ChoiceType == ChoiceType.SelectTargets && request.PlayerSeat == tuxiPlayer.Seat)
            {
                return new ChoiceResult(request.RequestId, request.PlayerSeat, new[] { target1.Seat, target2.Seat }, null, null, null);
            }
            return new ChoiceResult(request.RequestId, request.PlayerSeat, null, null, null, null);
        };

        var drawPhaseService = new DrawPhaseService(cardMoveService, ruleService, eventBus, stack, skillManager, getPlayerChoice);

        // Act - Trigger draw phase
        var phaseStartEvent = new PhaseStartEvent(game, tuxiPlayer.Seat, Phase.Draw);
        eventBus.Publish(phaseStartEvent);

        // Execute resolution stack
        while (!stack.IsEmpty)
        {
            var result = stack.Pop();
            Assert.IsTrue(result.Success, "Resolution should succeed.");
        }

        // Assert
        Assert.IsNotNull(publishedEvent, "DrawPhaseReplacedEvent should be published.");
        Assert.AreEqual(tuxiPlayer.Seat, publishedEvent.PlayerSeat);
        Assert.AreEqual("突袭", publishedEvent.ReplacementReason);
        Assert.AreEqual(2, publishedEvent.TargetSeats.Count);
        Assert.IsTrue(publishedEvent.TargetSeats.Contains(target1.Seat));
        Assert.IsTrue(publishedEvent.TargetSeats.Contains(target2.Seat));
    }

    #endregion
}

