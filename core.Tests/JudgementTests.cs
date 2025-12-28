using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Judgement;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests;

[TestClass]
public sealed class JudgementTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
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

    #region Judgement Rule Tests

    /// <summary>
    /// Tests that RedJudgementRule returns true for red cards (Heart or Diamond).
    /// Input: Red cards (Heart, Diamond) and black cards (Spade, Club).
    /// Expected: RedJudgementRule.Evaluate returns true for red cards, false for black cards.
    /// </summary>
    [TestMethod]
    public void RedJudgementRuleEvaluatesRedCardsAsSuccess()
    {
        // Arrange
        var rule = new RedJudgementRule();
        var redHeart = CreateTestCard(1, Suit.Heart, 5);
        var redDiamond = CreateTestCard(2, Suit.Diamond, 5);
        var blackSpade = CreateTestCard(3, Suit.Spade, 5);
        var blackClub = CreateTestCard(4, Suit.Club, 5);

        // Act & Assert
        Assert.IsTrue(rule.Evaluate(redHeart), "Heart should be evaluated as success.");
        Assert.IsTrue(rule.Evaluate(redDiamond), "Diamond should be evaluated as success.");
        Assert.IsFalse(rule.Evaluate(blackSpade), "Spade should be evaluated as failure.");
        Assert.IsFalse(rule.Evaluate(blackClub), "Club should be evaluated as failure.");
    }

    /// <summary>
    /// Tests that BlackJudgementRule returns true for black cards (Spade or Club).
    /// Input: Black cards (Spade, Club) and red cards (Heart, Diamond).
    /// Expected: BlackJudgementRule.Evaluate returns true for black cards, false for red cards.
    /// </summary>
    [TestMethod]
    public void BlackJudgementRuleEvaluatesBlackCardsAsSuccess()
    {
        // Arrange
        var rule = new BlackJudgementRule();
        var blackSpade = CreateTestCard(1, Suit.Spade, 5);
        var blackClub = CreateTestCard(2, Suit.Club, 5);
        var redHeart = CreateTestCard(3, Suit.Heart, 5);
        var redDiamond = CreateTestCard(4, Suit.Diamond, 5);

        // Act & Assert
        Assert.IsTrue(rule.Evaluate(blackSpade), "Spade should be evaluated as success.");
        Assert.IsTrue(rule.Evaluate(blackClub), "Club should be evaluated as success.");
        Assert.IsFalse(rule.Evaluate(redHeart), "Heart should be evaluated as failure.");
        Assert.IsFalse(rule.Evaluate(redDiamond), "Diamond should be evaluated as failure.");
    }

    /// <summary>
    /// Tests that SuitJudgementRule returns true only for the specified suit.
    /// Input: Cards with different suits and a rule for Heart.
    /// Expected: SuitJudgementRule.Evaluate returns true only for Heart cards.
    /// </summary>
    [TestMethod]
    public void SuitJudgementRuleEvaluatesOnlySpecifiedSuit()
    {
        // Arrange
        var rule = new SuitJudgementRule(Suit.Heart);
        var heartCard = CreateTestCard(1, Suit.Heart, 5);
        var spadeCard = CreateTestCard(2, Suit.Spade, 5);
        var clubCard = CreateTestCard(3, Suit.Club, 5);
        var diamondCard = CreateTestCard(4, Suit.Diamond, 5);

        // Act & Assert
        Assert.IsTrue(rule.Evaluate(heartCard), "Heart card should be evaluated as success.");
        Assert.IsFalse(rule.Evaluate(spadeCard), "Spade card should be evaluated as failure.");
        Assert.IsFalse(rule.Evaluate(clubCard), "Club card should be evaluated as failure.");
        Assert.IsFalse(rule.Evaluate(diamondCard), "Diamond card should be evaluated as failure.");
    }

    /// <summary>
    /// Tests that RankJudgementRule returns true only for the specified rank.
    /// Input: Cards with different ranks and a rule for rank 5.
    /// Expected: RankJudgementRule.Evaluate returns true only for rank 5 cards.
    /// </summary>
    [TestMethod]
    public void RankJudgementRuleEvaluatesOnlySpecifiedRank()
    {
        // Arrange
        var rule = new RankJudgementRule(5);
        var rank5Card = CreateTestCard(1, Suit.Spade, 5);
        var rank3Card = CreateTestCard(2, Suit.Spade, 3);
        var rank10Card = CreateTestCard(3, Suit.Spade, 10);

        // Act & Assert
        Assert.IsTrue(rule.Evaluate(rank5Card), "Rank 5 card should be evaluated as success.");
        Assert.IsFalse(rule.Evaluate(rank3Card), "Rank 3 card should be evaluated as failure.");
        Assert.IsFalse(rule.Evaluate(rank10Card), "Rank 10 card should be evaluated as failure.");
    }

    /// <summary>
    /// Tests that RankRangeJudgementRule returns true for ranks within the range.
    /// Input: Cards with ranks 2, 5, 9 and a rule for range 2-9.
    /// Expected: RankRangeJudgementRule.Evaluate returns true for ranks 2, 5, 9, false for ranks outside range.
    /// </summary>
    [TestMethod]
    public void RankRangeJudgementRuleEvaluatesRanksInRange()
    {
        // Arrange
        var rule = new RankRangeJudgementRule(2, 9);
        var rank2Card = CreateTestCard(1, Suit.Spade, 2);
        var rank5Card = CreateTestCard(2, Suit.Spade, 5);
        var rank9Card = CreateTestCard(3, Suit.Spade, 9);
        var rank1Card = CreateTestCard(4, Suit.Spade, 1);
        var rank10Card = CreateTestCard(5, Suit.Spade, 10);

        // Act & Assert
        Assert.IsTrue(rule.Evaluate(rank2Card), "Rank 2 card should be evaluated as success.");
        Assert.IsTrue(rule.Evaluate(rank5Card), "Rank 5 card should be evaluated as success.");
        Assert.IsTrue(rule.Evaluate(rank9Card), "Rank 9 card should be evaluated as success.");
        Assert.IsFalse(rule.Evaluate(rank1Card), "Rank 1 card should be evaluated as failure.");
        Assert.IsFalse(rule.Evaluate(rank10Card), "Rank 10 card should be evaluated as failure.");
    }

    /// <summary>
    /// Tests that CompositeJudgementRule with And operator returns true only when all rules succeed.
    /// Input: Composite rule with RedJudgementRule and RankJudgementRule(5) using And operator.
    /// Expected: Returns true only for red cards with rank 5.
    /// </summary>
    [TestMethod]
    public void CompositeJudgementRuleWithAndOperatorRequiresAllRulesToSucceed()
    {
        // Arrange
        var redRule = new RedJudgementRule();
        var rank5Rule = new RankJudgementRule(5);
        var rule = new CompositeJudgementRule(
            new IJudgementRule[] { redRule, rank5Rule },
            JudgementRuleOperator.And);

        var redRank5Card = CreateTestCard(1, Suit.Heart, 5);
        var redRank3Card = CreateTestCard(2, Suit.Heart, 3);
        var blackRank5Card = CreateTestCard(3, Suit.Spade, 5);

        // Act & Assert
        Assert.IsTrue(rule.Evaluate(redRank5Card), "Red rank 5 card should be evaluated as success.");
        Assert.IsFalse(rule.Evaluate(redRank3Card), "Red rank 3 card should be evaluated as failure.");
        Assert.IsFalse(rule.Evaluate(blackRank5Card), "Black rank 5 card should be evaluated as failure.");
    }

    /// <summary>
    /// Tests that CompositeJudgementRule with Or operator returns true when any rule succeeds.
    /// Input: Composite rule with RedJudgementRule and RankJudgementRule(5) using Or operator.
    /// Expected: Returns true for red cards OR rank 5 cards.
    /// </summary>
    [TestMethod]
    public void CompositeJudgementRuleWithOrOperatorRequiresAnyRuleToSucceed()
    {
        // Arrange
        var redRule = new RedJudgementRule();
        var rank5Rule = new RankJudgementRule(5);
        var rule = new CompositeJudgementRule(
            new IJudgementRule[] { redRule, rank5Rule },
            JudgementRuleOperator.Or);

        var redRank3Card = CreateTestCard(1, Suit.Heart, 3);
        var blackRank5Card = CreateTestCard(2, Suit.Spade, 5);
        var blackRank3Card = CreateTestCard(3, Suit.Spade, 3);

        // Act & Assert
        Assert.IsTrue(rule.Evaluate(redRank3Card), "Red rank 3 card should be evaluated as success.");
        Assert.IsTrue(rule.Evaluate(blackRank5Card), "Black rank 5 card should be evaluated as success.");
        Assert.IsFalse(rule.Evaluate(blackRank3Card), "Black rank 3 card should be evaluated as failure.");
    }

    /// <summary>
    /// Tests that NegatedJudgementRule returns the opposite of the inner rule.
    /// Input: Negated RedJudgementRule.
    /// Expected: Returns true for black cards, false for red cards.
    /// </summary>
    [TestMethod]
    public void NegatedJudgementRuleReturnsOppositeOfInnerRule()
    {
        // Arrange
        var redRule = new RedJudgementRule();
        var rule = new NegatedJudgementRule(redRule);

        var redCard = CreateTestCard(1, Suit.Heart, 5);
        var blackCard = CreateTestCard(2, Suit.Spade, 5);

        // Act & Assert
        Assert.IsFalse(rule.Evaluate(redCard), "Red card should be evaluated as failure (negated).");
        Assert.IsTrue(rule.Evaluate(blackCard), "Black card should be evaluated as success (negated).");
    }

    #endregion

    #region BasicJudgementService Tests

    /// <summary>
    /// Tests that BasicJudgementService.ExecuteJudgement draws a card from draw pile and places it in JudgementZone.
    /// Input: Game with cards in draw pile, player, judgement request with RedJudgementRule.
    /// Expected: Card is removed from draw pile, added to JudgementZone, and result is calculated correctly.
    /// </summary>
    [TestMethod]
    public void BasicJudgementServiceExecuteJudgementMovesCardToJudgementZone()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        var cardMoveService = new BasicCardMoveService();

        // Add cards to draw pile
        if (game.DrawPile is Zone drawZone)
        {
            var testCard = CreateTestCard(1, Suit.Heart, 5);
            drawZone.MutableCards.Add(testCard);
        }

        var rule = new RedJudgementRule();
        var source = CreateTestEffectSource();
        var request = new JudgementRequest(
            Guid.NewGuid(),
            player.Seat,
            JudgementReason.Skill,
            source,
            rule);

        var service = new BasicJudgementService();

        // Act
        var result = service.ExecuteJudgement(game, player, request, cardMoveService);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(request.JudgementId, result.JudgementId);
        Assert.AreEqual(player.Seat, result.JudgeOwnerSeat);
        Assert.IsTrue(result.IsSuccess, "Red card should result in success.");
        Assert.AreEqual(1, result.OriginalCard.Id);
        Assert.AreEqual(Suit.Heart, result.OriginalCard.Suit);
        Assert.IsTrue(player.JudgementZone.Cards.Contains(result.OriginalCard), "Card should be in JudgementZone.");
        Assert.AreEqual(0, game.DrawPile.Cards.Count, "Card should be removed from draw pile.");
    }

    /// <summary>
    /// Tests that BasicJudgementService.ExecuteJudgement calculates failure correctly for black cards.
    /// Input: Game with black card in draw pile, player, judgement request with RedJudgementRule.
    /// Expected: Result.IsSuccess is false.
    /// </summary>
    [TestMethod]
    public void BasicJudgementServiceExecuteJudgementCalculatesFailureCorrectly()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        var cardMoveService = new BasicCardMoveService();

        // Add black card to draw pile
        if (game.DrawPile is Zone drawZone)
        {
            var testCard = CreateTestCard(1, Suit.Spade, 5);
            drawZone.MutableCards.Add(testCard);
        }

        var rule = new RedJudgementRule();
        var source = CreateTestEffectSource();
        var request = new JudgementRequest(
            Guid.NewGuid(),
            player.Seat,
            JudgementReason.Skill,
            source,
            rule);

        var service = new BasicJudgementService();

        // Act
        var result = service.ExecuteJudgement(game, player, request, cardMoveService);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess, "Black card should result in failure for RedJudgementRule.");
    }

    /// <summary>
    /// Tests that BasicJudgementService.CompleteJudgement moves card from JudgementZone to discard pile.
    /// Input: Game, player with card in JudgementZone.
    /// Expected: Card is removed from JudgementZone and added to discard pile.
    /// </summary>
    [TestMethod]
    public void BasicJudgementServiceCompleteJudgementMovesCardToDiscardPile()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        var cardMoveService = new BasicCardMoveService();

        // Add card to JudgementZone
        var testCard = CreateTestCard(1, Suit.Heart, 5);
        if (player.JudgementZone is Zone judgementZone)
        {
            judgementZone.MutableCards.Add(testCard);
        }

        var service = new BasicJudgementService();

        // Act
        service.CompleteJudgement(game, player, testCard, cardMoveService);

        // Assert
        Assert.IsFalse(player.JudgementZone.Cards.Contains(testCard), "Card should be removed from JudgementZone.");
        Assert.IsTrue(game.DiscardPile.Cards.Contains(testCard), "Card should be in discard pile.");
    }

    /// <summary>
    /// Tests that BasicJudgementService.ExecuteJudgement throws exception when draw pile is empty.
    /// Input: Game with empty draw pile.
    /// Expected: InvalidOperationException is thrown.
    /// </summary>
    [TestMethod]
    public void BasicJudgementServiceExecuteJudgementThrowsWhenDrawPileIsEmpty()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        var cardMoveService = new BasicCardMoveService();

        var rule = new RedJudgementRule();
        var source = CreateTestEffectSource();
        var request = new JudgementRequest(
            Guid.NewGuid(),
            player.Seat,
            JudgementReason.Skill,
            source,
            rule);

        var service = new BasicJudgementService();

        // Act & Assert
        Assert.ThrowsException<InvalidOperationException>(() =>
            service.ExecuteJudgement(game, player, request, cardMoveService));
    }

    /// <summary>
    /// Tests that BasicJudgementService publishes JudgementStartedEvent and JudgementCompletedEvent.
    /// Input: Game with event bus, player, judgement request.
    /// Expected: Both events are published to event bus.
    /// </summary>
    [TestMethod]
    public void BasicJudgementServicePublishesEvents()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        var cardMoveService = new BasicCardMoveService();
        var eventBus = new BasicEventBus();

        // Add card to draw pile
        if (game.DrawPile is Zone drawZone)
        {
            var testCard = CreateTestCard(1, Suit.Heart, 5);
            drawZone.MutableCards.Add(testCard);
        }

        var rule = new RedJudgementRule();
        var source = CreateTestEffectSource();
        var request = new JudgementRequest(
            Guid.NewGuid(),
            player.Seat,
            JudgementReason.Skill,
            source,
            rule);

        var service = new BasicJudgementService(eventBus);

        var startedEvents = new List<JudgementStartedEvent>();
        var completedEvents = new List<JudgementCompletedEvent>();

        eventBus.Subscribe<JudgementStartedEvent>(e => startedEvents.Add(e));
        eventBus.Subscribe<JudgementCompletedEvent>(e => completedEvents.Add(e));

        // Act
        var result = service.ExecuteJudgement(game, player, request, cardMoveService);

        // Assert
        Assert.AreEqual(1, startedEvents.Count, "JudgementStartedEvent should be published once.");
        Assert.AreEqual(request.JudgementId, startedEvents[0].JudgementId);
        Assert.AreEqual(player.Seat, startedEvents[0].JudgeOwnerSeat);

        Assert.AreEqual(1, completedEvents.Count, "JudgementCompletedEvent should be published once.");
        Assert.AreEqual(request.JudgementId, completedEvents[0].JudgementId);
        Assert.AreEqual(result, completedEvents[0].Result);
    }

    #endregion

    #region JudgementResolver Tests

    /// <summary>
    /// Tests that JudgementResolver executes judgement from IntermediateResults.
    /// Input: ResolutionContext with JudgementRequest in IntermediateResults.
    /// Expected: Judgement is executed and result is stored in IntermediateResults.
    /// </summary>
    [TestMethod]
    public void JudgementResolverExecutesJudgementFromIntermediateResults()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        var cardMoveService = new BasicCardMoveService();

        // Add card to draw pile
        if (game.DrawPile is Zone drawZone)
        {
            var testCard = CreateTestCard(1, Suit.Heart, 5);
            drawZone.MutableCards.Add(testCard);
        }

        var rule = new RedJudgementRule();
        var source = CreateTestEffectSource();
        var request = new JudgementRequest(
            Guid.NewGuid(),
            player.Seat,
            JudgementReason.Skill,
            source,
            rule,
            AllowModify: false); // Set to false to directly execute judgement and store result

        var intermediateResults = new Dictionary<string, object>
        {
            { "JudgementRequest", request }
        };

        var stack = new BasicResolutionStack();
        var ruleService = new RuleService();
        var context = new ResolutionContext(
            game,
            player,
            null,
            null,
            stack,
            cardMoveService,
            ruleService,
            null,
            null,
            null,
            intermediateResults,
            null,
            null,
            null,
            null,
            null);

        var resolver = new JudgementResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success, "Judgement should execute successfully.");
        Assert.IsTrue(intermediateResults.ContainsKey("JudgementResult"), "JudgementResult should be stored.");
        var storedResult = intermediateResults["JudgementResult"] as JudgementResult;
        Assert.IsNotNull(storedResult);
        Assert.IsTrue(storedResult.IsSuccess, "Red card should result in success.");
    }

    /// <summary>
    /// Tests that JudgementResolver returns failure when JudgementRequest is not found.
    /// Input: ResolutionContext without JudgementRequest in IntermediateResults.
    /// Expected: ResolutionResult with failure and appropriate error code.
    /// </summary>
    [TestMethod]
    public void JudgementResolverReturnsFailureWhenRequestNotFound()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        var cardMoveService = new BasicCardMoveService();
        var stack = new BasicResolutionStack();
        var ruleService = new RuleService();

        var context = new ResolutionContext(
            game,
            player,
            null,
            null,
            stack,
            cardMoveService,
            ruleService,
            null,
            null,
            null,
            new Dictionary<string, object>(), // Empty IntermediateResults
            null,
            null,
            null,
            null,
            null);

        var resolver = new JudgementResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsFalse(result.Success, "Should fail when request is not found.");
        Assert.AreEqual(ResolutionErrorCode.InvalidState, result.ErrorCode);
    }

    #endregion

    #region Integration Tests

    /// <summary>
    /// Tests the complete judgement flow: draw card, place in JudgementZone, calculate result, move to discard pile.
    /// Input: Game with cards in draw pile, player, judgement request.
    /// Expected: Card moves from draw pile -> JudgementZone -> discard pile, result is calculated correctly.
    /// </summary>
    [TestMethod]
    public void CompleteJudgementFlowMovesCardThroughAllZones()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        var cardMoveService = new BasicCardMoveService();

        // Add card to draw pile
        if (game.DrawPile is Zone drawZone)
        {
            var testCard = CreateTestCard(1, Suit.Heart, 5);
            drawZone.MutableCards.Add(testCard);
        }

        var rule = new RedJudgementRule();
        var source = CreateTestEffectSource();
        var request = new JudgementRequest(
            Guid.NewGuid(),
            player.Seat,
            JudgementReason.Skill,
            source,
            rule);

        var service = new BasicJudgementService();

        // Act - Execute judgement
        var result = service.ExecuteJudgement(game, player, request, cardMoveService);

        // Assert - Card should be in JudgementZone
        Assert.IsTrue(player.JudgementZone.Cards.Contains(result.OriginalCard), "Card should be in JudgementZone after execution.");
        Assert.AreEqual(0, game.DrawPile.Cards.Count, "Card should be removed from draw pile.");

        // Act - Complete judgement
        service.CompleteJudgement(game, player, result.OriginalCard, cardMoveService);

        // Assert - Card should be in discard pile
        Assert.IsFalse(player.JudgementZone.Cards.Contains(result.OriginalCard), "Card should be removed from JudgementZone.");
        Assert.IsTrue(game.DiscardPile.Cards.Contains(result.OriginalCard), "Card should be in discard pile.");
    }

    #endregion
}
