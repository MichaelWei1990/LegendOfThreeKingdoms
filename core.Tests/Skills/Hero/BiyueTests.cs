using System;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Configuration;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Skills.Hero;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests.Skills.Hero;

/// <summary>
/// Tests for Biyue (闭月) skill.
/// </summary>
[TestClass]
public class BiyueTests
{
    private Game CreateDefaultGame(int playerCount)
    {
        var baseConfig = CoreApi.CreateDefaultConfig(playerCount);
        var playerConfigs = Enumerable.Range(0, playerCount)
            .Select(i => new PlayerConfig
            {
                Seat = i,
                HeroId = i == 0 ? "diaochan" : null,
                MaxHealth = 4,
                InitialHealth = 4
            })
            .ToArray();

        var config = new GameConfig
        {
            PlayerConfigs = playerConfigs,
            DeckConfig = baseConfig.DeckConfig,
            Seed = baseConfig.Seed,
            GameModeId = baseConfig.GameModeId,
            GameVariantOptions = baseConfig.GameVariantOptions
        };

        return Game.FromConfig(config);
    }

    private Card CreateTestCard(int id, Suit suit, int rank)
    {
        return new Card
        {
            Id = id,
            Suit = suit,
            Rank = rank,
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash
        };
    }

    /// <summary>
    /// Tests that BiyueSkill can be created and has correct properties.
    /// </summary>
    [TestMethod]
    public void BiyueSkill_HasCorrectProperties()
    {
        // Arrange
        var skill = new BiyueSkill();

        // Assert
        Assert.AreEqual("biyue", skill.Id);
        Assert.AreEqual("闭月", skill.Name);
        Assert.AreEqual(SkillType.Trigger, skill.Type);
        Assert.AreEqual(SkillCapability.None, skill.Capabilities);
    }

    /// <summary>
    /// Acceptance Test 1: A owns Biyue, enters own End Phase, chooses to activate → A hand count +1, draw pile -1.
    /// </summary>
    [TestMethod]
    public void BiyueSkill_PlayerCanActivateInOwnEndPhase_DrawsOneCard()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = true;

        // Add cards to draw pile
        var drawCard = CreateTestCard(1, Suit.Heart, 5);
        if (game.DrawPile is Zone drawZone)
        {
            drawZone.MutableCards.Add(drawCard);
        }

        var initialHandCount = player.HandZone.Cards.Count;
        var initialDrawPileCount = game.DrawPile.Cards.Count;

        var eventBus = new BasicEventBus();
        var cardMoveService = new BasicCardMoveService(eventBus);
        var skill = new BiyueSkill();
        skill.SetCardMoveService(cardMoveService);

        bool activationAsked = false;
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            activationAsked = true;
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: true // Player chooses to activate
            );
        };
        skill.SetGetPlayerChoice(getPlayerChoice);

        skill.Attach(game, player, eventBus);

        // Act
        var phaseStartEvent = new PhaseStartEvent(game, player.Seat, Phase.End);
        eventBus.Publish(phaseStartEvent);

        // Assert
        Assert.IsTrue(activationAsked, "Skill should ask for activation confirmation");
        Assert.AreEqual(initialHandCount + 1, player.HandZone.Cards.Count, "Player should have 1 more card");
        Assert.AreEqual(initialDrawPileCount - 1, game.DrawPile.Cards.Count, "Draw pile should have 1 less card");
    }

    /// <summary>
    /// Acceptance Test 2: A owns Biyue, enters own End Phase, chooses not to activate → no change.
    /// </summary>
    [TestMethod]
    public void BiyueSkill_PlayerCanChooseNotToActivate_NoChange()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = true;

        // Add cards to draw pile
        var drawCard = CreateTestCard(1, Suit.Heart, 5);
        if (game.DrawPile is Zone drawZone)
        {
            drawZone.MutableCards.Add(drawCard);
        }

        var initialHandCount = player.HandZone.Cards.Count;
        var initialDrawPileCount = game.DrawPile.Cards.Count;

        var eventBus = new BasicEventBus();
        var cardMoveService = new BasicCardMoveService(eventBus);
        var skill = new BiyueSkill();
        skill.SetCardMoveService(cardMoveService);

        bool activationAsked = false;
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            activationAsked = true;
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: false // Player chooses not to activate
            );
        };
        skill.SetGetPlayerChoice(getPlayerChoice);

        skill.Attach(game, player, eventBus);

        // Act
        var phaseStartEvent = new PhaseStartEvent(game, player.Seat, Phase.End);
        eventBus.Publish(phaseStartEvent);

        // Assert
        Assert.IsTrue(activationAsked, "Skill should ask for activation confirmation");
        Assert.AreEqual(initialHandCount, player.HandZone.Cards.Count, "Player hand count should not change");
        Assert.AreEqual(initialDrawPileCount, game.DrawPile.Cards.Count, "Draw pile count should not change");
    }

    /// <summary>
    /// Acceptance Test 3: When B's End Phase starts, A's Biyue does not trigger → no choice popup/no draw.
    /// </summary>
    [TestMethod]
    public void BiyueSkill_DoesNotTrigger_WhenOtherPlayerEndPhase()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var playerA = game.Players[0];
        var playerB = game.Players[1];
        playerA.IsAlive = true;
        playerB.IsAlive = true;

        // Add cards to draw pile
        var drawCard = CreateTestCard(1, Suit.Heart, 5);
        if (game.DrawPile is Zone drawZone)
        {
            drawZone.MutableCards.Add(drawCard);
        }

        var initialHandCountA = playerA.HandZone.Cards.Count;
        var initialHandCountB = playerB.HandZone.Cards.Count;

        var eventBus = new BasicEventBus();
        var cardMoveService = new BasicCardMoveService(eventBus);
        var skill = new BiyueSkill();
        skill.SetCardMoveService(cardMoveService);

        bool activationAsked = false;
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            activationAsked = true;
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: true
            );
        };
        skill.SetGetPlayerChoice(getPlayerChoice);

        skill.Attach(game, playerA, eventBus);

        // Act - B's End Phase
        var phaseStartEvent = new PhaseStartEvent(game, playerB.Seat, Phase.End);
        eventBus.Publish(phaseStartEvent);

        // Assert
        Assert.IsFalse(activationAsked, "Skill should not ask for activation when other player's End Phase");
        Assert.AreEqual(initialHandCountA, playerA.HandZone.Cards.Count, "Player A hand count should not change");
        Assert.AreEqual(initialHandCountB, playerB.HandZone.Cards.Count, "Player B hand count should not change");
    }

    /// <summary>
    /// Acceptance Test 4: If skill is inactive (e.g., skill disabled), A's End Phase starts but Biyue is inactive → no activation option provided.
    /// </summary>
    [TestMethod]
    public void BiyueSkill_DoesNotTrigger_WhenSkillIsInactive()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = false; // Dead player = inactive skill

        // Add cards to draw pile
        var drawCard = CreateTestCard(1, Suit.Heart, 5);
        if (game.DrawPile is Zone drawZone)
        {
            drawZone.MutableCards.Add(drawCard);
        }

        var initialHandCount = player.HandZone.Cards.Count;

        var eventBus = new BasicEventBus();
        var cardMoveService = new BasicCardMoveService(eventBus);
        var skill = new BiyueSkill();
        skill.SetCardMoveService(cardMoveService);

        bool activationAsked = false;
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            activationAsked = true;
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: true
            );
        };
        skill.SetGetPlayerChoice(getPlayerChoice);

        skill.Attach(game, player, eventBus);

        // Act
        var phaseStartEvent = new PhaseStartEvent(game, player.Seat, Phase.End);
        eventBus.Publish(phaseStartEvent);

        // Assert
        Assert.IsFalse(activationAsked, "Skill should not ask for activation when skill is inactive");
        Assert.AreEqual(initialHandCount, player.HandZone.Cards.Count, "Player hand count should not change");
    }

    /// <summary>
    /// Tests that BiyueSkill does not trigger on PhaseStartEvent when phase is not End.
    /// </summary>
    [TestMethod]
    public void BiyueSkill_DoesNotTrigger_WhenPhaseIsNotEnd()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = true;

        var eventBus = new BasicEventBus();
        var cardMoveService = new BasicCardMoveService(eventBus);
        var skill = new BiyueSkill();
        skill.SetCardMoveService(cardMoveService);

        bool activationAsked = false;
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            activationAsked = true;
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

        skill.Attach(game, player, eventBus);

        // Act - Play phase (not End phase)
        var phaseStartEvent = new PhaseStartEvent(game, player.Seat, Phase.Play);
        eventBus.Publish(phaseStartEvent);

        // Assert
        Assert.IsFalse(activationAsked, "Skill should not ask for activation when phase is not End");
    }

    /// <summary>
    /// Tests that BiyueSkill can be registered for Diao Chan.
    /// </summary>
    [TestMethod]
    public void BiyueSkill_CanBeRegisteredForDiaoChan()
    {
        // Arrange
        var registry = new SkillRegistry();
        QunHeroRegistration.RegisterAll(registry);

        // Act
        var skills = registry.GetSkillsForHero("diaochan").ToList();

        // Assert
        Assert.IsTrue(skills.Any(s => s.Id == "biyue"), "Diao Chan should have Biyue skill");
    }
}

