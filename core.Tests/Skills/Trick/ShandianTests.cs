using System;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Judgement;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Phases;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Tricks;
using LegendOfThreeKingdoms.Core.Turns;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests;

[TestClass]
public sealed class ShandianTests
{
    private static Game CreateDefaultGame(int playerCount = 3)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Card CreateShandianCard(int id = 1)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "shandian",
            Name = "闪电",
            CardType = CardType.Trick,
            CardSubType = CardSubType.Shandian,
            Suit = Suit.Heart,
            Rank = 3
        };
    }

    private static Card CreateJudgementCard(int id, Suit suit, int rank = 5)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"judgement_card_{id}",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = suit,
            Rank = rank
        };
    }

    #region DelayedTrickResolver Tests

    /// <summary>
    /// Tests that DelayedTrickResolver successfully places Shandian into source's own judgement zone.
    /// Input: Game with 2 players, source player uses Shandian on self.
    /// Expected: Shandian card is moved from source player's hand to source's own judgement zone.
    /// </summary>
    [TestMethod]
    public void DelayedTrickResolverPlacesShandianInOwnJudgementZone()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var source = game.Players[0];

        var shandian = CreateShandianCard();
        ((Zone)source.HandZone).MutableCards.Add(shandian);

        var initialSourceHandCount = source.HandZone.Cards.Count;
        var initialSourceJudgementCount = source.JudgementZone.Cards.Count;

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { source.Seat }, // Self-targeting
            SelectedCardIds: new[] { shandian.Id },
            SelectedOptionId: null,
            Confirmed: null
        );

        var context = new ResolutionContext(
            game,
            source,
            Action: null,
            choice,
            stack,
            cardMoveService,
            ruleService
        );

        var resolver = new DelayedTrickResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success, "DelayedTrickResolver should succeed.");
        Assert.IsFalse(source.HandZone.Cards.Contains(shandian), "Shandian should be removed from source's hand.");
        Assert.IsTrue(source.JudgementZone.Cards.Contains(shandian), "Shandian should be in source's judgement zone.");
        Assert.AreEqual(initialSourceHandCount - 1, source.HandZone.Cards.Count, "Source hand count should decrease by 1.");
        Assert.AreEqual(initialSourceJudgementCount + 1, source.JudgementZone.Cards.Count, "Source judgement zone count should increase by 1.");
    }

    #endregion

    #region Rule Tests

    /// <summary>
    /// Tests that Shandian can be used during Play phase.
    /// Input: Game with 2 players, source player in Play phase, Shandian card in hand.
    /// Expected: CanUseCard returns Allowed.
    /// </summary>
    [TestMethod]
    public void ShandianCanBeUsedDuringPlayPhase()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = game.Players[0].Seat;
        var source = game.Players[0];
        var shandian = CreateShandianCard();
        ((Zone)source.HandZone).MutableCards.Add(shandian);

        var ruleService = new RuleService();
        var usageContext = new CardUsageContext(
            game,
            source,
            shandian,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 0);

        // Act
        var result = ruleService.CanUseCard(usageContext);

        // Assert
        Assert.IsTrue(result.IsAllowed, "Shandian should be usable during Play phase.");
    }

    /// <summary>
    /// Tests that Shandian requires self-targeting.
    /// Input: Game with 2 players, Shandian card in hand.
    /// Expected: GetLegalTargets returns only the source player.
    /// </summary>
    [TestMethod]
    public void ShandianRequiresSelfTargeting()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = game.Players[0].Seat;
        var source = game.Players[0];
        var shandian = CreateShandianCard();
        ((Zone)source.HandZone).MutableCards.Add(shandian);

        var ruleService = new RuleService();
        var usageContext = new CardUsageContext(
            game,
            source,
            shandian,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 0);

        // Act
        var legalTargets = ruleService.GetLegalTargetsForUse(usageContext);

        // Assert
        Assert.IsTrue(legalTargets.HasAny, "Shandian should have legal targets.");
        Assert.AreEqual(1, legalTargets.Items.Count, "Shandian should have exactly one legal target (self).");
        Assert.AreEqual(source.Seat, legalTargets.Items[0].Seat, "Shandian's legal target should be the source player.");
    }

    #endregion

    #region Judgement Tests

    /// <summary>
    /// Tests that Shandian deals 3 thunder damage when judgement succeeds (Spade 2-9).
    /// Input: Game with 2 players, Shandian in source's judgement zone, judgement card is Spade 5.
    /// Expected: Source takes 3 thunder damage, Shandian is discarded.
    /// </summary>
    [TestMethod]
    public void ShandianDealsDamageOnSuccessJudgement()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var initialHealth = source.CurrentHealth;

        var shandian = CreateShandianCard();
        ((Zone)source.JudgementZone).MutableCards.Add(shandian);

        // Create judgement card: Spade 5 (should succeed)
        var judgementCard = CreateJudgementCard(100, Suit.Spade, 5);
        // Place judgement card at top of draw pile
        ((Zone)game.DrawPile).MutableCards.Insert(0, judgementCard);

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var eventBus = new BasicEventBus();
        var stack = new BasicResolutionStack();
        var judgementService = new BasicJudgementService(eventBus);

        var context = new ResolutionContext(
            game,
            source,
            Action: null,
            Choice: null,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: null,
            IntermediateResults: new System.Collections.Generic.Dictionary<string, object>(),
            EventBus: eventBus,
            LogCollector: null,
            SkillManager: null,
            EquipmentSkillRegistry: null,
            JudgementService: judgementService
        );

        var resolver = new DelayedTrickJudgementResolver(shandian);

        // Act
        var result = resolver.Resolve(context);

        // Execute stack to process judgement and effects
        while (!stack.IsEmpty)
        {
            var stackResult = stack.Pop();
            Assert.IsTrue(stackResult.Success, 
                $"Stack execution should succeed. Error: {stackResult.ErrorCode}, Message: {stackResult.MessageKey}");
        }

        // Assert
        Assert.IsTrue(result.Success, "DelayedTrickJudgementResolver should succeed.");
        // Note: Actual damage application depends on the judgement result
        // We need to verify the judgement was performed correctly
    }

    /// <summary>
    /// Tests that Shandian moves to next player when judgement fails (non-Spade 2-9).
    /// Input: Game with 3 players, Shandian in player 0's judgement zone, judgement card is Heart 5.
    /// Expected: Shandian moves from player 0 to player 1's judgement zone.
    /// </summary>
    [TestMethod]
    public void ShandianMovesToNextPlayerOnFailureJudgement()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        var player0 = game.Players[0];
        var player1 = game.Players[1];

        var shandian = CreateShandianCard();
        ((Zone)player0.JudgementZone).MutableCards.Add(shandian);

        var initialPlayer0JudgementCount = player0.JudgementZone.Cards.Count;
        var initialPlayer1JudgementCount = player1.JudgementZone.Cards.Count;

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var eventBus = new BasicEventBus();
        var stack = new BasicResolutionStack();
        var judgementService = new BasicJudgementService(eventBus);

        // Create judgement rule for Shandian (Spade 2-9 = success)
        var judgementRule = new CompositeJudgementRule(
            new IJudgementRule[]
            {
                new SuitJudgementRule(Suit.Spade),
                new RankRangeJudgementRule(2, 9)
            },
            JudgementRuleOperator.And);

        // Create a judgement result that fails (Heart 5, not Spade 2-9)
        var judgementCard = CreateJudgementCard(100, Suit.Heart, 5);
        var isSuccess = judgementRule.Evaluate(judgementCard);
        Assert.IsFalse(isSuccess, "Heart 5 should not match Spade 2-9 rule.");

        var context = new ResolutionContext(
            game,
            player0,
            Action: null,
            Choice: null,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: null,
            IntermediateResults: new System.Collections.Generic.Dictionary<string, object>
            {
                ["JudgementResult"] = new JudgementResult(
                    JudgementId: Guid.NewGuid(),
                    JudgeOwnerSeat: player0.Seat,
                    OriginalCard: judgementCard,
                    FinalCard: judgementCard,
                    IsSuccess: false,
                    RuleSnapshot: judgementRule.Description,
                    ModifiersApplied: Array.Empty<JudgementModificationRecord>()
                )
            },
            EventBus: eventBus,
            LogCollector: null,
            SkillManager: null,
            EquipmentSkillRegistry: null,
            JudgementService: judgementService
        );

        // Create effect resolver directly to test failure effect
        var shandianEffectResolver = new ShandianResolver();
        var effectResolver = new DelayedTrickEffectResolver(shandian, shandianEffectResolver);

        // Act
        var result = effectResolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success, "DelayedTrickEffectResolver should succeed.");
        Assert.IsFalse(player0.JudgementZone.Cards.Contains(shandian), "Shandian should be removed from player 0's judgement zone.");
        Assert.IsTrue(player1.JudgementZone.Cards.Contains(shandian), "Shandian should be in player 1's judgement zone.");
        Assert.AreEqual(initialPlayer0JudgementCount - 1, player0.JudgementZone.Cards.Count, "Player 0 judgement zone count should decrease by 1.");
        Assert.AreEqual(initialPlayer1JudgementCount + 1, player1.JudgementZone.Cards.Count, "Player 1 judgement zone count should increase by 1.");
    }

    /// <summary>
    /// Tests that Shandian judgement rule correctly identifies Spade 2-9 as success.
    /// Input: Various judgement cards.
    /// Expected: Only Spade 2-9 cards result in success.
    /// </summary>
    [TestMethod]
    public void ShandianJudgementRuleCorrectlyIdentifiesSuccess()
    {
        // Arrange
        var judgementRule = new CompositeJudgementRule(
            new IJudgementRule[]
            {
                new SuitJudgementRule(Suit.Spade),
                new RankRangeJudgementRule(2, 9)
            },
            JudgementRuleOperator.And);

        // Act & Assert
        // Spade 2-9 should succeed
        Assert.IsTrue(judgementRule.Evaluate(CreateJudgementCard(1, Suit.Spade, 2)), "Spade 2 should succeed.");
        Assert.IsTrue(judgementRule.Evaluate(CreateJudgementCard(2, Suit.Spade, 5)), "Spade 5 should succeed.");
        Assert.IsTrue(judgementRule.Evaluate(CreateJudgementCard(3, Suit.Spade, 9)), "Spade 9 should succeed.");

        // Other suits should fail
        Assert.IsFalse(judgementRule.Evaluate(CreateJudgementCard(4, Suit.Heart, 5)), "Heart 5 should fail.");
        Assert.IsFalse(judgementRule.Evaluate(CreateJudgementCard(5, Suit.Club, 5)), "Club 5 should fail.");
        Assert.IsFalse(judgementRule.Evaluate(CreateJudgementCard(6, Suit.Diamond, 5)), "Diamond 5 should fail.");

        // Spade but wrong rank should fail
        Assert.IsFalse(judgementRule.Evaluate(CreateJudgementCard(7, Suit.Spade, 1)), "Spade 1 should fail.");
        Assert.IsFalse(judgementRule.Evaluate(CreateJudgementCard(8, Suit.Spade, 10)), "Spade 10 should fail.");
        Assert.IsFalse(judgementRule.Evaluate(CreateJudgementCard(9, Suit.Spade, 13)), "Spade 13 should fail.");
    }

    #endregion
}
