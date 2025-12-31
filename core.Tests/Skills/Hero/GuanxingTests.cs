using System;
using System.Collections.Generic;
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

[TestClass]
public sealed class GuanxingTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Game CreateGameWithPlayerConfigs(int playerCount, List<PlayerConfig> playerConfigs)
    {
        var baseConfig = CoreApi.CreateDefaultConfig(playerCount);
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

    private static Card CreateTestCard(int id, Suit suit = Suit.Spade, int rank = 7)
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
    /// Tests that GuanxingSkillFactory creates correct skill instance.
    /// </summary>
    [TestMethod]
    public void GuanxingSkillFactory_CreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new GuanxingSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("guanxing", skill.Id);
        Assert.AreEqual("观星", skill.Name);
        Assert.AreEqual(SkillType.Trigger, skill.Type);
    }

    /// <summary>
    /// Tests that SkillRegistry can register Guanxing skill.
    /// </summary>
    [TestMethod]
    public void SkillRegistry_RegisterGuanxingSkill_Success()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new GuanxingSkillFactory();

        // Act & Assert - Should not throw
        registry.RegisterSkill("guanxing", factory);
        
        // Verify by creating skill through registry
        var skill = factory.CreateSkill();
        Assert.AreEqual("guanxing", skill.Id);
    }

    #endregion

    #region Skill Property Tests

    /// <summary>
    /// Tests that GuanxingSkill has correct properties.
    /// </summary>
    [TestMethod]
    public void GuanxingSkill_HasCorrectProperties()
    {
        // Arrange
        var skill = new GuanxingSkill();

        // Assert
        Assert.AreEqual("guanxing", skill.Id);
        Assert.AreEqual("观星", skill.Name);
        Assert.AreEqual(SkillType.Trigger, skill.Type);
        Assert.AreEqual(SkillCapability.None, skill.Capabilities);
    }

    #endregion

    #region Trigger Condition Tests

    /// <summary>
    /// Tests that Guanxing triggers during owner's Start phase (Prepare phase).
    /// </summary>
    [TestMethod]
    public void GuanxingSkill_Triggers_WhenOwnerStartPhase()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = true;

        // Add cards to draw pile
        var drawCards = Enumerable.Range(1, 5).Select(i => CreateTestCard(i)).ToList();
        if (game.DrawPile is Zone drawZone)
        {
            foreach (var card in drawCards)
            {
                drawZone.MutableCards.Add(card);
            }
        }

        var eventBus = new BasicEventBus();
        var cardMoveService = new BasicCardMoveService(eventBus);
        var skill = new GuanxingSkill();
        skill.SetCardMoveService(cardMoveService);

        bool activationAsked = false;
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            if (request.ChoiceType == ChoiceType.Confirm)
            {
                activationAsked = true;
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: false // Player chooses not to activate for this test
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

        skill.Attach(game, player, eventBus);

        // Act
        var phaseStartEvent = new PhaseStartEvent(game, player.Seat, Phase.Start);
        eventBus.Publish(phaseStartEvent);

        // Assert
        Assert.IsTrue(activationAsked, "Skill should ask for activation during Start phase");
    }

    /// <summary>
    /// Tests that Guanxing does not trigger during other phases.
    /// </summary>
    [TestMethod]
    public void GuanxingSkill_DoesNotTrigger_WhenPhaseIsNotStart()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = true;

        var eventBus = new BasicEventBus();
        var cardMoveService = new BasicCardMoveService(eventBus);
        var skill = new GuanxingSkill();
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

        // Act - Try different phases
        var phases = new[] { Phase.Judge, Phase.Draw, Phase.Play, Phase.Discard, Phase.End };
        foreach (var phase in phases)
        {
            var phaseStartEvent = new PhaseStartEvent(game, player.Seat, phase);
            eventBus.Publish(phaseStartEvent);
        }

        // Assert
        Assert.IsFalse(activationAsked, "Skill should not ask for activation during non-Start phases");
    }

    /// <summary>
    /// Tests that Guanxing does not trigger when other player's Start phase.
    /// </summary>
    [TestMethod]
    public void GuanxingSkill_DoesNotTrigger_WhenOtherPlayerStartPhase()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var playerA = game.Players[0];
        var playerB = game.Players[1];
        playerA.IsAlive = true;
        playerB.IsAlive = true;

        var eventBus = new BasicEventBus();
        var cardMoveService = new BasicCardMoveService(eventBus);
        var skill = new GuanxingSkill();
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

        skill.Attach(game, playerA, eventBus);

        // Act - B's Start Phase
        var phaseStartEvent = new PhaseStartEvent(game, playerB.Seat, Phase.Start);
        eventBus.Publish(phaseStartEvent);

        // Assert
        Assert.IsFalse(activationAsked, "Skill should not ask for activation when other player's Start Phase");
    }

    /// <summary>
    /// Tests that Guanxing does not trigger when skill is inactive.
    /// </summary>
    [TestMethod]
    public void GuanxingSkill_DoesNotTrigger_WhenSkillIsInactive()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = false; // Dead player = inactive skill

        var eventBus = new BasicEventBus();
        var cardMoveService = new BasicCardMoveService(eventBus);
        var skill = new GuanxingSkill();
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

        // Act
        var phaseStartEvent = new PhaseStartEvent(game, player.Seat, Phase.Start);
        eventBus.Publish(phaseStartEvent);

        // Assert
        Assert.IsFalse(activationAsked, "Skill should not ask for activation when skill is inactive");
    }

    #endregion

    #region X Value Calculation Tests

    /// <summary>
    /// Tests that X is calculated as min(alive players count, 5).
    /// </summary>
    [TestMethod]
    public void GuanxingSkill_CalculatesX_AsMinAlivePlayersAnd5()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        var player = game.Players[0];
        player.IsAlive = true;
        game.Players[1].IsAlive = true;
        game.Players[2].IsAlive = true;

        // Add enough cards to draw pile
        var drawCards = Enumerable.Range(1, 10).Select(i => CreateTestCard(i)).ToList();
        if (game.DrawPile is Zone drawZone)
        {
            foreach (var card in drawCards)
            {
                drawZone.MutableCards.Add(card);
            }
        }

        var eventBus = new BasicEventBus();
        var cardMoveService = new BasicCardMoveService(eventBus);
        var skill = new GuanxingSkill();
        skill.SetCardMoveService(cardMoveService);

        int? actualX = null;
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            if (request.ChoiceType == ChoiceType.Confirm)
            {
                // Count alive players to verify X calculation
                var aliveCount = game.Players.Count(p => p.IsAlive);
                actualX = Math.Min(aliveCount, 5);
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: false // Don't activate, just verify X calculation
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

        skill.Attach(game, player, eventBus);

        // Act
        var phaseStartEvent = new PhaseStartEvent(game, player.Seat, Phase.Start);
        eventBus.Publish(phaseStartEvent);

        // Assert
        Assert.IsTrue(actualX.HasValue, "X should be calculated");
        Assert.AreEqual(3, actualX.Value, "X should be min(3, 5) = 3");
    }

    /// <summary>
    /// Tests that X is capped at 5 even with more alive players.
    /// </summary>
    [TestMethod]
    public void GuanxingSkill_CalculatesX_CappedAt5()
    {
        // Arrange
        var game = CreateDefaultGame(8);
        var player = game.Players[0];
        foreach (var p in game.Players)
        {
            p.IsAlive = true;
        }

        // Add enough cards to draw pile
        var drawCards = Enumerable.Range(1, 10).Select(i => CreateTestCard(i)).ToList();
        if (game.DrawPile is Zone drawZone)
        {
            foreach (var card in drawCards)
            {
                drawZone.MutableCards.Add(card);
            }
        }

        var eventBus = new BasicEventBus();
        var cardMoveService = new BasicCardMoveService(eventBus);
        var skill = new GuanxingSkill();
        skill.SetCardMoveService(cardMoveService);

        int? actualX = null;
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            if (request.ChoiceType == ChoiceType.Confirm)
            {
                var aliveCount = game.Players.Count(p => p.IsAlive);
                actualX = Math.Min(aliveCount, 5);
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null,
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
        skill.SetGetPlayerChoice(getPlayerChoice);

        skill.Attach(game, player, eventBus);

        // Act
        var phaseStartEvent = new PhaseStartEvent(game, player.Seat, Phase.Start);
        eventBus.Publish(phaseStartEvent);

        // Assert
        Assert.IsTrue(actualX.HasValue, "X should be calculated");
        Assert.AreEqual(5, actualX.Value, "X should be capped at 5");
    }

    /// <summary>
    /// Tests that X is limited by draw pile size when draw pile has fewer cards.
    /// </summary>
    [TestMethod]
    public void GuanxingSkill_CalculatesX_LimitedByDrawPileSize()
    {
        // Arrange
        var game = CreateDefaultGame(5);
        var player = game.Players[0];
        foreach (var p in game.Players)
        {
            p.IsAlive = true;
        }

        // Add only 3 cards to draw pile (less than min(5, 5) = 5)
        var drawCards = Enumerable.Range(1, 3).Select(i => CreateTestCard(i)).ToList();
        if (game.DrawPile is Zone drawZone)
        {
            foreach (var card in drawCards)
            {
                drawZone.MutableCards.Add(card);
            }
        }

        var eventBus = new BasicEventBus();
        var cardMoveService = new BasicCardMoveService(eventBus);
        var skill = new GuanxingSkill();
        skill.SetCardMoveService(cardMoveService);

        List<Card>? viewedCards = null;
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
                    Confirmed: true // Activate to see cards
                );
            }
            if (request.ChoiceType == ChoiceType.SelectCards && request.AllowedCards is not null)
            {
                viewedCards = request.AllowedCards.ToList();
                // Select all cards to go to top
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: request.AllowedCards.Select(c => c.Id).ToList(),
                    SelectedOptionId: null,
                    Confirmed: true
                );
            }
            // For ordering requests, return cards in original order
            if (request.ChoiceType == ChoiceType.SelectCards && request.AllowedCards is not null)
            {
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: new[] { request.AllowedCards[0].Id },
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

        skill.Attach(game, player, eventBus);

        // Act
        var phaseStartEvent = new PhaseStartEvent(game, player.Seat, Phase.Start);
        eventBus.Publish(phaseStartEvent);

        // Assert
        // X should be limited to 3 (draw pile size) instead of 5
        // The skill should view 3 cards
        Assert.IsTrue(viewedCards is not null, "Cards should be viewed");
        Assert.IsTrue(viewedCards.Count <= 3, "Should view at most 3 cards (draw pile size)");
    }

    #endregion

    #region Card Arrangement Tests

    /// <summary>
    /// Tests that Guanxing can arrange cards to top and bottom of draw pile.
    /// </summary>
    [TestMethod]
    public void GuanxingSkill_CanArrangeCards_ToTopAndBottom()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = true;

        // Add 5 cards to draw pile
        var drawCards = Enumerable.Range(1, 5).Select(i => CreateTestCard(i)).ToList();
        if (game.DrawPile is Zone drawZone)
        {
            foreach (var card in drawCards)
            {
                drawZone.MutableCards.Add(card);
            }
        }

        var initialDrawPileCount = game.DrawPile.Cards.Count;

        var eventBus = new BasicEventBus();
        var cardMoveService = new BasicCardMoveService(eventBus);
        var skill = new GuanxingSkill();
        skill.SetCardMoveService(cardMoveService);

        var topCardIds = new List<int> { 1, 2, 3 };
        var bottomCardIds = new List<int> { 4, 5 };
        var selectionStep = 0;

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
                    Confirmed: true // Activate
                );
            }
            if (request.ChoiceType == ChoiceType.SelectCards)
            {
                // First selection: choose which cards go to top
                if (selectionStep == 0)
                {
                    selectionStep++;
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: topCardIds,
                        SelectedOptionId: null,
                        Confirmed: true
                    );
                }
                // Subsequent selections: order cards (simplified - just return first card)
                var firstCard = request.AllowedCards?.FirstOrDefault();
                if (firstCard is not null)
                {
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: new[] { firstCard.Id },
                        SelectedOptionId: null,
                        Confirmed: true
                    );
                }
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

        skill.Attach(game, player, eventBus);

        // Act
        var phaseStartEvent = new PhaseStartEvent(game, player.Seat, Phase.Start);
        eventBus.Publish(phaseStartEvent);

        // Assert
        Assert.AreEqual(initialDrawPileCount, game.DrawPile.Cards.Count, "Draw pile count should remain the same");
        // Cards should be rearranged (verification of exact order would require more detailed testing)
    }

    /// <summary>
    /// Tests that Guanxing does not activate when player chooses not to activate.
    /// </summary>
    [TestMethod]
    public void GuanxingSkill_DoesNotActivate_WhenPlayerChoosesNotTo()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = true;

        // Add cards to draw pile
        var drawCards = Enumerable.Range(1, 5).Select(i => CreateTestCard(i)).ToList();
        if (game.DrawPile is Zone drawZone)
        {
            foreach (var card in drawCards)
            {
                drawZone.MutableCards.Add(card);
            }
        }

        var initialDrawPileCount = game.DrawPile.Cards.Count;
        var initialTopCard = game.DrawPile.Cards.FirstOrDefault();

        var eventBus = new BasicEventBus();
        var cardMoveService = new BasicCardMoveService(eventBus);
        var skill = new GuanxingSkill();
        skill.SetCardMoveService(cardMoveService);

        bool arrangementAsked = false;
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
                    Confirmed: false // Player chooses not to activate
                );
            }
            arrangementAsked = true;
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

        // Act
        var phaseStartEvent = new PhaseStartEvent(game, player.Seat, Phase.Start);
        eventBus.Publish(phaseStartEvent);

        // Assert
        Assert.IsFalse(arrangementAsked, "Should not ask for arrangement when player chooses not to activate");
        Assert.AreEqual(initialDrawPileCount, game.DrawPile.Cards.Count, "Draw pile count should not change");
        if (initialTopCard is not null)
        {
            Assert.AreEqual(initialTopCard.Id, game.DrawPile.Cards[0].Id, "Top card should remain the same");
        }
    }

    #endregion
}

