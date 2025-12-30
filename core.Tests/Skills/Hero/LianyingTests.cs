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
/// Tests for Lianying (连营) skill.
/// </summary>
[TestClass]
public class LianyingTests
{
    private Game CreateDefaultGame(int playerCount)
    {
        var baseConfig = CoreApi.CreateDefaultConfig(playerCount);
        var playerConfigs = Enumerable.Range(0, playerCount)
            .Select(i => new PlayerConfig
            {
                Seat = i,
                HeroId = i == 0 ? "luxun" : null,
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
    /// Tests that LianyingSkill can be created and has correct properties.
    /// </summary>
    [TestMethod]
    public void LianyingSkill_HasCorrectProperties()
    {
        // Arrange
        var skill = new LianyingSkill();

        // Assert
        Assert.AreEqual("lianying", skill.Id);
        Assert.AreEqual("连营", skill.Name);
        Assert.AreEqual(SkillType.Trigger, skill.Type);
        Assert.AreEqual(SkillCapability.None, skill.Capabilities);
    }

    /// <summary>
    /// Tests that LianyingSkill triggers when player loses last hand card.
    /// Input: Player with 1 hand card, card is discarded.
    /// Expected: Skill triggers and player draws 1 card.
    /// </summary>
    [TestMethod]
    public void LianyingSkill_TriggersWhenLosingLastHandCard()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = true;

        // Add 1 card to player's hand
        var card = CreateTestCard(1, Suit.Heart, 5);
        if (player.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(card);
        }

        // Add cards to draw pile
        var drawCard = CreateTestCard(2, Suit.Spade, 6);
        if (game.DrawPile is Zone drawZone)
        {
            drawZone.MutableCards.Add(drawCard);
        }

        var eventBus = new BasicEventBus();
        var cardMoveService = new BasicCardMoveService(null, null, eventBus);
        var skill = new LianyingSkill();
        skill.SetCardMoveService(cardMoveService);

        // Mock getPlayerChoice: player chooses to use Lianying
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = request =>
        {
            if (request.ChoiceType == ChoiceType.Confirm)
            {
                return new ChoiceResult(
                    request.RequestId,
                    player.Seat,
                    null,
                    null,
                    null,
                    true); // Player chooses to use Lianying
            }
            return new ChoiceResult(request.RequestId, player.Seat, null, null, null, null);
        };
        skill.SetPlayerChoice(getPlayerChoice);

        skill.Attach(game, player, eventBus);

        // Act: Discard the card from hand
        cardMoveService.DiscardFromHand(game, player, new[] { card });

        // Assert: Player should have drawn 1 card (the drawCard)
        Assert.AreEqual(1, player.HandZone.Cards.Count, "Player should have 1 card after Lianying triggers.");
        Assert.AreEqual(drawCard.Id, player.HandZone.Cards[0].Id, "Player should have drawn the card from draw pile.");
    }

    /// <summary>
    /// Tests that LianyingSkill does not trigger when player has multiple hand cards.
    /// Input: Player with 2 hand cards, 1 card is discarded.
    /// Expected: Skill does not trigger.
    /// </summary>
    [TestMethod]
    public void LianyingSkill_DoesNotTriggerWhenMultipleHandCards()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = true;

        // Add 2 cards to player's hand
        var card1 = CreateTestCard(1, Suit.Heart, 5);
        var card2 = CreateTestCard(2, Suit.Spade, 6);
        if (player.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(card1);
            handZone.MutableCards.Add(card2);
        }

        var eventBus = new BasicEventBus();
        var cardMoveService = new BasicCardMoveService(null, null, eventBus);
        var skill = new LianyingSkill();
        skill.SetCardMoveService(cardMoveService);

        skill.Attach(game, player, eventBus);

        var initialHandCount = player.HandZone.Cards.Count;

        // Act: Discard 1 card from hand
        cardMoveService.DiscardFromHand(game, player, new[] { card1 });

        // Assert: Player should still have 1 card, skill should not trigger
        Assert.AreEqual(initialHandCount - 1, player.HandZone.Cards.Count, "Player should have 1 card after discarding.");
        Assert.AreEqual(card2.Id, player.HandZone.Cards[0].Id, "Player should still have the second card.");
    }

    /// <summary>
    /// Tests that LianyingSkill does not trigger when player already has 0 hand cards.
    /// Input: Player with 0 hand cards, card is added then removed (simulating edge case).
    /// Expected: Skill does not trigger.
    /// </summary>
    [TestMethod]
    public void LianyingSkill_DoesNotTriggerWhenAlreadyZeroHandCards()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = true;

        // Player has 0 hand cards
        Assert.AreEqual(0, player.HandZone.Cards.Count);

        var eventBus = new BasicEventBus();
        var cardMoveService = new BasicCardMoveService(null, null, eventBus);
        var skill = new LianyingSkill();
        skill.SetCardMoveService(cardMoveService);

        skill.Attach(game, player, eventBus);

        // Act: Try to discard (should be no-op since no cards)
        cardMoveService.DiscardFromHand(game, player, Array.Empty<Card>());

        // Assert: Player should still have 0 cards, skill should not trigger
        Assert.AreEqual(0, player.HandZone.Cards.Count, "Player should still have 0 cards.");
    }

    /// <summary>
    /// Tests that LianyingSkill only triggers once when multiple cards are lost at once.
    /// Input: Player with 2 hand cards, both cards are discarded at once.
    /// Expected: Skill triggers only once (when hand count goes from 2 to 0).
    /// </summary>
    [TestMethod]
    public void LianyingSkill_TriggersOnlyOnceWhenMultipleCardsLostAtOnce()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = true;

        // Add 2 cards to player's hand
        var card1 = CreateTestCard(1, Suit.Heart, 5);
        var card2 = CreateTestCard(2, Suit.Spade, 6);
        if (player.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(card1);
            handZone.MutableCards.Add(card2);
        }

        // Add cards to draw pile
        var drawCard = CreateTestCard(3, Suit.Club, 7);
        if (game.DrawPile is Zone drawZone)
        {
            drawZone.MutableCards.Add(drawCard);
        }

        var eventBus = new BasicEventBus();
        var cardMoveService = new BasicCardMoveService(null, null, eventBus);
        var skill = new LianyingSkill();
        skill.SetCardMoveService(cardMoveService);

        var triggerCount = 0;
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = request =>
        {
            if (request.ChoiceType == ChoiceType.Confirm)
            {
                triggerCount++;
                return new ChoiceResult(
                    request.RequestId,
                    player.Seat,
                    null,
                    null,
                    null,
                    true); // Player chooses to use Lianying
            }
            return new ChoiceResult(request.RequestId, player.Seat, null, null, null, null);
        };
        skill.SetPlayerChoice(getPlayerChoice);

        skill.Attach(game, player, eventBus);

        // Act: Discard both cards at once
        cardMoveService.DiscardFromHand(game, player, new[] { card1, card2 });

        // Assert: Skill should trigger only once
        Assert.AreEqual(1, triggerCount, "Lianying should trigger only once when multiple cards are lost at once.");
        Assert.AreEqual(1, player.HandZone.Cards.Count, "Player should have drawn 1 card.");
    }

    /// <summary>
    /// Tests that LianyingSkill respects player choice (optional trigger).
    /// Input: Player loses last hand card, player chooses not to use skill.
    /// Expected: Skill does not draw card.
    /// </summary>
    [TestMethod]
    public void LianyingSkill_RespectsPlayerChoice()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = true;

        // Add 1 card to player's hand
        var card = CreateTestCard(1, Suit.Heart, 5);
        if (player.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(card);
        }

        var eventBus = new BasicEventBus();
        var cardMoveService = new BasicCardMoveService(null, null, eventBus);
        var skill = new LianyingSkill();
        skill.SetCardMoveService(cardMoveService);

        // Mock getPlayerChoice: player chooses NOT to use Lianying
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = request =>
        {
            if (request.ChoiceType == ChoiceType.Confirm)
            {
                return new ChoiceResult(
                    request.RequestId,
                    player.Seat,
                    null,
                    null,
                    null,
                    false); // Player chooses NOT to use Lianying
            }
            return new ChoiceResult(request.RequestId, player.Seat, null, null, null, null);
        };
        skill.SetPlayerChoice(getPlayerChoice);

        skill.Attach(game, player, eventBus);

        // Act: Discard the card from hand
        cardMoveService.DiscardFromHand(game, player, new[] { card });

        // Assert: Player should have 0 cards (skill did not trigger)
        Assert.AreEqual(0, player.HandZone.Cards.Count, "Player should have 0 cards after choosing not to use Lianying.");
    }

    /// <summary>
    /// Tests that LianyingSkill can be registered for Lu Xun.
    /// </summary>
    [TestMethod]
    public void LianyingSkill_CanBeRegisteredForLuXun()
    {
        // Arrange
        var registry = new SkillRegistry();
        WuHeroRegistration.RegisterAll(registry);

        // Act
        var skills = registry.GetSkillsForHero("luxun").ToList();

        // Assert
        // Lu Xun has 2 skills: "modesty" and "lianying"
        Assert.AreEqual(2, skills.Count);
        Assert.IsTrue(skills.Any(s => s.Id == "lianying"));
        Assert.IsTrue(skills.Any(s => s.Id == "modesty"));
    }
}

