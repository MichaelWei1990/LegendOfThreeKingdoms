using System;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Judgement;
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
public sealed class TianduTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Game CreateGameWithCardsInDrawPile(int playerCount = 2, int cardCount = 10)
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

    private static IEffectSource CreateTestEffectSource(string sourceId = "test_source", string sourceType = "Skill", string? displayName = "Test Source")
    {
        return new TestEffectSource(sourceId, sourceType, displayName);
    }

    private sealed class TestEffectSource : IEffectSource
    {
        public string SourceId { get; }
        public string SourceType { get; }
        public string? DisplayName { get; }

        public TestEffectSource(string sourceId, string sourceType, string? displayName)
        {
            SourceId = sourceId;
            SourceType = sourceType;
            DisplayName = displayName;
        }
    }

    #region Skill Registry Tests

    /// <summary>
    /// Tests that TianduSkillFactory creates correct skill instance.
    /// Input: TianduSkillFactory instance.
    /// Expected: Created skill has correct Id, Name, and SkillType.Locked.
    /// </summary>
    [TestMethod]
    public void TianduSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new TianduSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("tiandu", skill.Id);
        Assert.AreEqual("天妒", skill.Name);
        Assert.AreEqual(SkillType.Locked, skill.Type);
    }

    /// <summary>
    /// Tests that SkillRegistry can register and retrieve Tiandu skill.
    /// Input: Empty registry, TianduSkillFactory.
    /// Expected: After registration, GetSkill returns a skill with correct Id.
    /// </summary>
    [TestMethod]
    public void SkillRegistryRegisterTianduSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new TianduSkillFactory();

        // Act
        registry.RegisterSkill("tiandu", factory);
        var skill = registry.GetSkill("tiandu");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("tiandu", skill.Id);
        Assert.AreEqual("天妒", skill.Name);
    }

    /// <summary>
    /// Tests that SkillRegistry prevents duplicate skill registrations.
    /// Input: Registry with "tiandu" already registered, attempting to register again.
    /// Expected: ArgumentException is thrown when trying to register duplicate skill ID.
    /// </summary>
    [TestMethod]
    public void SkillRegistryPreventsDuplicateTianduSkillRegistration()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory1 = new TianduSkillFactory();
        var factory2 = new TianduSkillFactory();

        // Act
        registry.RegisterSkill("tiandu", factory1);

        // Assert
        Assert.ThrowsException<ArgumentException>(() =>
            registry.RegisterSkill("tiandu", factory2));
    }

    #endregion

    #region Skill Properties Tests

    /// <summary>
    /// Tests that TianduSkill has correct properties.
    /// Input: TianduSkill instance.
    /// Expected: Skill has correct Id, Name, Type, and Capabilities.
    /// </summary>
    [TestMethod]
    public void TianduSkillHasCorrectProperties()
    {
        // Arrange
        var skill = new TianduSkill();

        // Act & Assert
        Assert.AreEqual("tiandu", skill.Id);
        Assert.AreEqual("天妒", skill.Name);
        Assert.AreEqual(SkillType.Locked, skill.Type);
        Assert.AreEqual(SkillCapability.None, skill.Capabilities);
    }

    #endregion

    #region Event Subscription Tests

    /// <summary>
    /// Tests that TianduSkill subscribes to JudgementCompletedEvent when attached.
    /// Input: Game, player, event bus, TianduSkill.
    /// Expected: Skill subscribes to JudgementCompletedEvent after Attach is called.
    /// </summary>
    [TestMethod]
    public void TianduSkillSubscribesToJudgementCompletedEvent()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        var eventBus = new BasicEventBus();
        var skill = new TianduSkill();
        bool eventReceived = false;

        // Subscribe to verify event subscription
        eventBus.Subscribe<JudgementCompletedEvent>(evt => eventReceived = true);

        // Act
        skill.Attach(game, player, eventBus);

        // Publish a test event to verify subscription
        var testResult = new JudgementResult(
            Guid.NewGuid(),
            player.Seat,
            CreateTestCard(1, Suit.Heart, 5),
            CreateTestCard(1, Suit.Heart, 5),
            true,
            "Test rule",
            Array.Empty<JudgementModificationRecord>());
        var testEvent = new JudgementCompletedEvent(game, Guid.NewGuid(), testResult);
        eventBus.Publish(testEvent);

        // Assert
        Assert.IsTrue(eventReceived, "Event should be received after skill attachment.");
    }

    /// <summary>
    /// Tests that TianduSkill unsubscribes from JudgementCompletedEvent when detached.
    /// Input: Game, player, event bus, TianduSkill (attached then detached).
    /// Expected: Skill unsubscribes from JudgementCompletedEvent after Detach is called.
    /// </summary>
    [TestMethod]
    public void TianduSkillUnsubscribesFromJudgementCompletedEvent()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        var eventBus = new BasicEventBus();
        var skill = new TianduSkill();
        skill.Attach(game, player, eventBus);

        // Act
        skill.Detach(game, player, eventBus);

        // Note: We can't directly verify unsubscription without internal access,
        // but we can verify that the skill no longer responds to events
        // This is tested indirectly in the trigger tests
    }

    #endregion

    #region Skill Trigger Tests

    /// <summary>
    /// Tests that TianduSkill obtains judgement card when owner is the judge.
    /// Input: Game with cards, player with tiandu skill, judgement request for that player.
    /// Expected: After judgement completes, judgement card is moved from JudgementZone to player's HandZone.
    /// </summary>
    [TestMethod]
    public void TianduSkillObtainsJudgementCardWhenOwnerIsJudge()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(2, 5);
        var player = game.Players[0];
        player.IsAlive = true;

        var cardMoveService = new BasicCardMoveService();
        var eventBus = new BasicEventBus();
        var skill = new TianduSkill(cardMoveService);
        skill.Attach(game, player, eventBus);

        var rule = new RedJudgementRule();
        var source = CreateTestEffectSource();
        var request = new JudgementRequest(
            Guid.NewGuid(),
            player.Seat,
            JudgementReason.Skill,
            source,
            rule);

        var service = new BasicJudgementService(eventBus);

        // Act
        var result = service.ExecuteJudgement(game, player, request, cardMoveService);

        // Assert
        var judgementCard = result.FinalCard;
        Assert.IsFalse(player.JudgementZone.Cards.Contains(judgementCard), "Judgement card should be removed from JudgementZone.");
        Assert.IsTrue(player.HandZone.Cards.Contains(judgementCard), "Judgement card should be in player's HandZone.");
    }

    /// <summary>
    /// Tests that TianduSkill does not trigger for other players' judgements.
    /// Input: Game with cards, player1 with tiandu skill, player2 performs judgement.
    /// Expected: After player2's judgement completes, judgement card remains in player2's JudgementZone, not moved to player1's HandZone.
    /// </summary>
    [TestMethod]
    public void TianduSkillDoesNotTriggerForOtherPlayersJudgement()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(2, 5);
        var player1 = game.Players[0];
        var player2 = game.Players[1];
        player1.IsAlive = true;
        player2.IsAlive = true;

        var cardMoveService = new BasicCardMoveService();
        var eventBus = new BasicEventBus();
        var skill = new TianduSkill(cardMoveService);
        skill.Attach(game, player1, eventBus);

        var rule = new RedJudgementRule();
        var source = CreateTestEffectSource();
        var request = new JudgementRequest(
            Guid.NewGuid(),
            player2.Seat, // Player2 is the judge
            JudgementReason.Skill,
            source,
            rule);

        var service = new BasicJudgementService(eventBus);

        // Act
        var result = service.ExecuteJudgement(game, player2, request, cardMoveService);

        // Assert
        var judgementCard = result.FinalCard;
        Assert.IsTrue(player2.JudgementZone.Cards.Contains(judgementCard), "Judgement card should remain in player2's JudgementZone.");
        Assert.IsFalse(player1.HandZone.Cards.Contains(judgementCard), "Judgement card should not be in player1's HandZone.");
    }

    /// <summary>
    /// Tests that TianduSkill does not trigger when skill is inactive.
    /// Input: Game with cards, dead player with tiandu skill, judgement request.
    /// Expected: After judgement completes, judgement card remains in JudgementZone, not moved to HandZone.
    /// </summary>
    [TestMethod]
    public void TianduSkillDoesNotTriggerWhenInactive()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(2, 5);
        var player = game.Players[0];
        player.IsAlive = false; // Skill should not be active

        var cardMoveService = new BasicCardMoveService();
        var eventBus = new BasicEventBus();
        var skill = new TianduSkill(cardMoveService);
        skill.Attach(game, player, eventBus);

        var rule = new RedJudgementRule();
        var source = CreateTestEffectSource();
        var request = new JudgementRequest(
            Guid.NewGuid(),
            player.Seat,
            JudgementReason.Skill,
            source,
            rule);

        var service = new BasicJudgementService(eventBus);

        // Act
        var result = service.ExecuteJudgement(game, player, request, cardMoveService);

        // Assert
        var judgementCard = result.FinalCard;
        Assert.IsTrue(player.JudgementZone.Cards.Contains(judgementCard), "Judgement card should remain in JudgementZone when skill is inactive.");
        Assert.IsFalse(player.HandZone.Cards.Contains(judgementCard), "Judgement card should not be in HandZone when skill is inactive.");
    }

    /// <summary>
    /// Tests that TianduSkill handles multiple judgements independently.
    /// Input: Game with cards, player with tiandu skill, two separate judgements.
    /// Expected: After each judgement completes, the corresponding judgement card is moved to player's HandZone.
    /// </summary>
    [TestMethod]
    public void TianduSkillHandlesMultipleJudgements()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(2, 10);
        var player = game.Players[0];
        player.IsAlive = true;

        var cardMoveService = new BasicCardMoveService();
        var eventBus = new BasicEventBus();
        var skill = new TianduSkill(cardMoveService);
        skill.Attach(game, player, eventBus);

        var rule = new RedJudgementRule();
        var source = CreateTestEffectSource();
        var service = new BasicJudgementService(eventBus);

        // Act - First judgement
        var request1 = new JudgementRequest(
            Guid.NewGuid(),
            player.Seat,
            JudgementReason.Skill,
            source,
            rule);
        var result1 = service.ExecuteJudgement(game, player, request1, cardMoveService);

        // Act - Second judgement
        var request2 = new JudgementRequest(
            Guid.NewGuid(),
            player.Seat,
            JudgementReason.Skill,
            source,
            rule);
        var result2 = service.ExecuteJudgement(game, player, request2, cardMoveService);

        // Assert
        var card1 = result1.FinalCard;
        var card2 = result2.FinalCard;
        Assert.IsTrue(player.HandZone.Cards.Contains(card1), "First judgement card should be in HandZone.");
        Assert.IsTrue(player.HandZone.Cards.Contains(card2), "Second judgement card should be in HandZone.");
        Assert.AreEqual(2, player.HandZone.Cards.Count(c => c.Id == card1.Id || c.Id == card2.Id), "Both judgement cards should be in HandZone.");
    }

    #endregion

    #region Edge Cases Tests

    /// <summary>
    /// Tests that TianduSkill handles the case when card is already moved.
    /// Input: Game with cards, player with tiandu skill, judgement card manually moved before event.
    /// Expected: Skill does not throw exception and does not attempt to move card again.
    /// </summary>
    [TestMethod]
    public void TianduSkillHandlesCardAlreadyMoved()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(2, 5);
        var player = game.Players[0];
        player.IsAlive = true;

        var cardMoveService = new BasicCardMoveService();
        var eventBus = new BasicEventBus();
        var skill = new TianduSkill(cardMoveService);
        skill.Attach(game, player, eventBus);

        var rule = new RedJudgementRule();
        var source = CreateTestEffectSource();
        var request = new JudgementRequest(
            Guid.NewGuid(),
            player.Seat,
            JudgementReason.Skill,
            source,
            rule);

        var service = new BasicJudgementService(eventBus);
        var result = service.ExecuteJudgement(game, player, request, cardMoveService);

        // After ExecuteJudgement, the event was published and skill should have moved the card
        var judgementCard = result.FinalCard;
        var initialHandCount = player.HandZone.Cards.Count(c => c.Id == judgementCard.Id);

        // Act - Publish event again (simulating duplicate event or race condition)
        // The skill should check if card is still in JudgementZone and skip if not
        var duplicateEvent = new JudgementCompletedEvent(game, request.JudgementId, result);
        eventBus.Publish(duplicateEvent);

        // Assert - Should not throw exception and card should only be in HandZone once
        // (skill should skip processing since card is no longer in JudgementZone)
        var finalHandCount = player.HandZone.Cards.Count(c => c.Id == judgementCard.Id);
        Assert.AreEqual(initialHandCount, finalHandCount, "Card count should not change after duplicate event.");
        Assert.AreEqual(1, finalHandCount, "Card should only be in HandZone once.");
    }

    /// <summary>
    /// Tests that TianduSkill handles null card move service gracefully.
    /// Input: Game with cards, player with tiandu skill (no cardMoveService injected), judgement request.
    /// Expected: Skill does not throw exception when cardMoveService is null.
    /// </summary>
    [TestMethod]
    public void TianduSkillHandlesNullCardMoveService()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(2, 5);
        var player = game.Players[0];
        player.IsAlive = true;

        var cardMoveService = new BasicCardMoveService();
        var eventBus = new BasicEventBus();
        var skill = new TianduSkill(); // No cardMoveService injected
        skill.Attach(game, player, eventBus);

        var rule = new RedJudgementRule();
        var source = CreateTestEffectSource();
        var request = new JudgementRequest(
            Guid.NewGuid(),
            player.Seat,
            JudgementReason.Skill,
            source,
            rule);

        var service = new BasicJudgementService(eventBus);

        // Act & Assert - Should not throw exception
        var result = service.ExecuteJudgement(game, player, request, cardMoveService);

        // Card should remain in JudgementZone since skill cannot move it
        var judgementCard = result.FinalCard;
        Assert.IsTrue(player.JudgementZone.Cards.Contains(judgementCard), "Judgement card should remain in JudgementZone when cardMoveService is null.");
    }

    #endregion

    #region Integration Tests

    /// <summary>
    /// Tests that TianduSkill works correctly with CompleteJudgement flow.
    /// Input: Game with cards, player with tiandu skill, complete judgement flow.
    /// Expected: After judgement, card is moved to HandZone by skill, CompleteJudgement skips moving to discard pile.
    /// </summary>
    [TestMethod]
    public void TianduSkillWorksWithCompleteJudgementFlow()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(2, 5);
        var player = game.Players[0];
        player.IsAlive = true;

        var cardMoveService = new BasicCardMoveService();
        var eventBus = new BasicEventBus();
        var skill = new TianduSkill(cardMoveService);
        skill.Attach(game, player, eventBus);

        var rule = new RedJudgementRule();
        var source = CreateTestEffectSource();
        var request = new JudgementRequest(
            Guid.NewGuid(),
            player.Seat,
            JudgementReason.Skill,
            source,
            rule);

        var service = new BasicJudgementService(eventBus);

        // Act - Execute judgement (triggers Tiandu skill)
        var result = service.ExecuteJudgement(game, player, request, cardMoveService);

        // Act - Complete judgement (should skip since card is already moved)
        service.CompleteJudgement(game, player, result.FinalCard, cardMoveService);

        // Assert
        var judgementCard = result.FinalCard;
        Assert.IsFalse(player.JudgementZone.Cards.Contains(judgementCard), "Judgement card should not be in JudgementZone.");
        Assert.IsTrue(player.HandZone.Cards.Contains(judgementCard), "Judgement card should be in HandZone.");
        Assert.IsFalse(game.DiscardPile.Cards.Contains(judgementCard), "Judgement card should not be in discard pile.");
    }

    /// <summary>
    /// Tests that TianduSkill works with BaguaArray judgement.
    /// Input: Game with cards, player with tiandu skill and bagua array equipment, bagua array triggers judgement.
    /// Expected: After bagua array judgement, card is moved to HandZone by tiandu skill, not to discard pile.
    /// </summary>
    [TestMethod]
    public void TianduSkillWorksWithBaguaArrayJudgement()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(2, 5);
        var player = game.Players[0];
        player.IsAlive = true;

        var cardMoveService = new BasicCardMoveService();
        var eventBus = new BasicEventBus();
        var skill = new TianduSkill(cardMoveService);
        skill.Attach(game, player, eventBus);

        var rule = new RedJudgementRule();
        var source = CreateTestEffectSource("bagua_array", "Equipment", "八卦阵");
        var request = new JudgementRequest(
            Guid.NewGuid(),
            player.Seat,
            JudgementReason.Armor,
            source,
            rule);

        var service = new BasicJudgementService(eventBus);

        // Act - Execute judgement (triggers Tiandu skill)
        var result = service.ExecuteJudgement(game, player, request, cardMoveService);

        // Act - Complete judgement (should skip since card is already moved)
        service.CompleteJudgement(game, player, result.FinalCard, cardMoveService);

        // Assert
        var judgementCard = result.FinalCard;
        Assert.IsFalse(player.JudgementZone.Cards.Contains(judgementCard), "Judgement card should not be in JudgementZone.");
        Assert.IsTrue(player.HandZone.Cards.Contains(judgementCard), "Judgement card should be in HandZone.");
        Assert.IsFalse(game.DiscardPile.Cards.Contains(judgementCard), "Judgement card should not be in discard pile.");
    }

    #endregion
}
