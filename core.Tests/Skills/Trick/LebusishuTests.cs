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
public sealed class LebusishuTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Card CreateLebusishuCard(int id = 1)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "lebusishu",
            Name = "乐不思蜀",
            CardType = CardType.Trick,
            CardSubType = CardSubType.Lebusishu,
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
    /// Tests that DelayedTrickResolver successfully places Lebusishu into target's judgement zone.
    /// Input: Game with 2 players, source player uses Lebusishu on target.
    /// Expected: Lebusishu card is moved from source player's hand to target's judgement zone.
    /// </summary>
    [TestMethod]
    public void DelayedTrickResolverPlacesLebusishuInJudgementZone()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var target = game.Players[1];

        var lebusishu = CreateLebusishuCard();
        ((Zone)source.HandZone).MutableCards.Add(lebusishu);

        var initialSourceHandCount = source.HandZone.Cards.Count;
        var initialTargetJudgementCount = target.JudgementZone.Cards.Count;

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { target.Seat },
            SelectedCardIds: new[] { lebusishu.Id },
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
        Assert.IsTrue(result.Success);
        Assert.AreEqual(initialSourceHandCount - 1, source.HandZone.Cards.Count, "Source player should have 1 fewer card.");
        Assert.AreEqual(initialTargetJudgementCount + 1, target.JudgementZone.Cards.Count, "Target player should have 1 more card in judgement zone.");
        Assert.IsFalse(source.HandZone.Cards.Contains(lebusishu), "Lebusishu card should not be in source player's hand.");
        Assert.IsTrue(target.JudgementZone.Cards.Contains(lebusishu), "Lebusishu card should be in target player's judgement zone.");
    }

    #endregion

    #region DelayedTrickJudgementResolver Tests

    /// <summary>
    /// Tests that DelayedTrickJudgementResolver correctly handles Heart judgement (success - no effect).
    /// Input: Game with 2 players, target has Lebusishu in judgement zone, draw pile has Heart card.
    /// Expected: Judgement succeeds, no SkipPlayPhase flag is set, card moves to discard pile.
    /// </summary>
    [TestMethod]
    public void DelayedTrickJudgementResolverHeartJudgementSuccessNoEffect()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var target = game.Players[0];

        var lebusishu = CreateLebusishuCard();
        ((Zone)target.JudgementZone).MutableCards.Add(lebusishu);

        var heartCard = CreateJudgementCard(100, Suit.Heart, 5);
        ((Zone)game.DrawPile).MutableCards.Insert(0, heartCard); // Insert at top

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();
        var eventBus = new BasicEventBus();

        var context = new ResolutionContext(
            game,
            target,
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
            JudgementService: new BasicJudgementService(eventBus)
        );

        var resolver = new DelayedTrickJudgementResolver(lebusishu);

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);

        // Execute the stack to complete judgement
        while (!stack.IsEmpty)
        {
            var stackResult = stack.Pop();
            Assert.IsTrue(stackResult.Success, 
                $"Stack execution should succeed. Error: {stackResult.ErrorCode}, Message: {stackResult.MessageKey}");
        }

        // Verify no SkipPlayPhase flag is set
        Assert.IsFalse(target.Flags.ContainsKey("SkipPlayPhase"), "SkipPlayPhase flag should not be set for Heart judgement.");

        // Verify card moved to discard pile
        Assert.IsFalse(target.JudgementZone.Cards.Contains(lebusishu), "Lebusishu card should be removed from judgement zone.");
        Assert.IsTrue(game.DiscardPile.Cards.Contains(lebusishu), "Lebusishu card should be in discard pile.");
    }

    /// <summary>
    /// Tests that DelayedTrickJudgementResolver correctly handles non-Heart judgement (failure - skip play phase).
    /// Input: Game with 2 players, target has Lebusishu in judgement zone, draw pile has Spade card.
    /// Expected: Judgement fails, SkipPlayPhase flag is set, card moves to discard pile.
    /// </summary>
    [TestMethod]
    public void DelayedTrickJudgementResolverNonHeartJudgementFailureSkipPlayPhase()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var target = game.Players[0];

        var lebusishu = CreateLebusishuCard();
        ((Zone)target.JudgementZone).MutableCards.Add(lebusishu);

        var spadeCard = CreateJudgementCard(100, Suit.Spade, 5);
        ((Zone)game.DrawPile).MutableCards.Insert(0, spadeCard); // Insert at top

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();
        var eventBus = new BasicEventBus();

        // Provide a GetPlayerChoice function that returns "pass" (no nullification)
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null, // Pass (no nullification)
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var context = new ResolutionContext(
            game,
            target,
            Action: null,
            Choice: null,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: getPlayerChoice,
            IntermediateResults: new System.Collections.Generic.Dictionary<string, object>(),
            EventBus: eventBus,
            LogCollector: null,
            SkillManager: null,
            EquipmentSkillRegistry: null,
            JudgementService: new BasicJudgementService(eventBus)
        );

        var resolver = new DelayedTrickJudgementResolver(lebusishu);

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);

        // Execute the stack to complete judgement
        while (!stack.IsEmpty)
        {
            var stackResult = stack.Pop();
            Assert.IsTrue(stackResult.Success, 
                $"Stack execution should succeed. Error: {stackResult.ErrorCode}, Message: {stackResult.MessageKey}");
        }

        // Verify SkipPlayPhase flag is set
        Assert.IsTrue(target.Flags.ContainsKey("SkipPlayPhase"), "SkipPlayPhase flag should be set for non-Heart judgement.");
        Assert.IsTrue(target.Flags["SkipPlayPhase"] is true, "SkipPlayPhase flag should be true.");

        // Verify card moved to discard pile
        Assert.IsFalse(target.JudgementZone.Cards.Contains(lebusishu), "Lebusishu card should be removed from judgement zone.");
        Assert.IsTrue(game.DiscardPile.Cards.Contains(lebusishu), "Lebusishu card should be in discard pile.");
    }

    #endregion

    #region Integration Tests - UseCardResolver

    /// <summary>
    /// Verifies that UseCardResolver successfully processes a valid Lebusishu usage through the complete flow.
    /// 
    /// Test scenario:
    /// - Sets up a 2-player game in Play phase
    /// - Source player has a Lebusishu card in hand
    /// - Creates a UseLebusishu action
    /// - Executes UseCardResolver with the action and choice
    /// 
    /// Expected results:
    /// - Resolution succeeds
    /// - Lebusishu card is moved from source player's hand to target's judgement zone
    /// - DelayedTrickResolver is pushed onto the stack for further processing
    /// </summary>
    [TestMethod]
    public void UseCardResolverProcessesValidLebusishu()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];
        var target = game.Players[1];

        var lebusishu = CreateLebusishuCard();
        ((Zone)source.HandZone).MutableCards.Add(lebusishu);

        var initialSourceHandCount = source.HandZone.Cards.Count;
        var initialTargetJudgementCount = target.JudgementZone.Cards.Count;

        var stack = new BasicResolutionStack();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var eventBus = new BasicEventBus();

        var action = new ActionDescriptor(
            ActionId: "UseLebusishu",
            DisplayKey: "action.useLebusishu",
            RequiresTargets: true,
            TargetConstraints: new TargetConstraints(1, 1, TargetFilterType.Any),
            CardCandidates: new[] { lebusishu }
        );

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { target.Seat },
            SelectedCardIds: new[] { lebusishu.Id },
            SelectedOptionId: null,
            Confirmed: null
        );

        var context = new ResolutionContext(
            game,
            source,
            action,
            choice,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: null,
            IntermediateResults: null,
            EventBus: eventBus,
            LogCollector: null,
            SkillManager: null,
            EquipmentSkillRegistry: null,
            JudgementService: null
        );

        var resolver = new UseCardResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);

        // Verify DelayedTrickResolver was pushed onto the stack
        Assert.IsFalse(stack.IsEmpty, "Stack should not be empty after UseCardResolver.");

        // Execute the stack to trigger DelayedTrickResolver (card movement happens here)
        while (!stack.IsEmpty)
        {
            var stackResult = stack.Pop();
            Assert.IsTrue(stackResult.Success, "Stack execution should succeed.");
        }

        // Verify Lebusishu card was moved from hand to target's judgement zone (after stack execution)
        Assert.IsFalse(source.HandZone.Cards.Contains(lebusishu), "Lebusishu card should be removed from hand.");
        Assert.IsTrue(target.JudgementZone.Cards.Contains(lebusishu), "Lebusishu card should be in target's judgement zone.");
    }

    #endregion

    #region Integration Tests - Phase Skipping

    /// <summary>
    /// Tests that BasicTurnEngine correctly skips Play phase when SkipPlayPhase flag is set.
    /// Input: Game with 2 players, target has SkipPlayPhase flag set, phase advances from Draw to Play.
    /// Expected: Play phase is automatically skipped, phase advances directly to Discard, flag is cleared.
    /// </summary>
    [TestMethod]
    public void BasicTurnEngineSkipsPlayPhaseWhenFlagSet()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var gameMode = new TestGameMode();
        var eventBus = new BasicEventBus();
        var turnEngine = new BasicTurnEngine(gameMode, eventBus);

        game.CurrentPlayerSeat = game.Players[0].Seat;
        game.CurrentPhase = Phase.Draw;

        // Set SkipPlayPhase flag
        game.Players[0].Flags["SkipPlayPhase"] = true;

        // Act - Advance phase from Draw to Play (should skip Play and go to Discard)
        var result = turnEngine.AdvancePhase(game);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(Phase.Discard, game.CurrentPhase, "Phase should skip Play and go directly to Discard.");
        Assert.IsFalse(game.Players[0].Flags.ContainsKey("SkipPlayPhase"), "SkipPlayPhase flag should be cleared after skipping.");
    }

    /// <summary>
    /// Tests complete flow: Lebusishu judgement failure -> SkipPlayPhase flag set -> Play phase skipped.
    /// Input: Game with 2 players, target has Lebusishu in judgement zone, non-Heart card in draw pile.
    /// Expected: Judgement fails, SkipPlayPhase flag is set, when phase advances to Play it is skipped.
    /// </summary>
    [TestMethod]
    public void CompleteFlowLebusishuJudgementFailureSkipsPlayPhase()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var gameMode = new TestGameMode();
        var eventBus = new BasicEventBus();
        var turnEngine = new BasicTurnEngine(gameMode, eventBus);

        var target = game.Players[0];
        game.CurrentPlayerSeat = target.Seat;
        game.CurrentPhase = Phase.Judge;

        var lebusishu = CreateLebusishuCard();
        ((Zone)target.JudgementZone).MutableCards.Add(lebusishu);

        var spadeCard = CreateJudgementCard(100, Suit.Spade, 5);
        ((Zone)game.DrawPile).MutableCards.Insert(0, spadeCard);

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();
        var judgePhaseService = new JudgePhaseService(cardMoveService, ruleService, eventBus, stack);

        // Provide a GetPlayerChoice function that returns "pass" (no nullification)
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null, // Pass (no nullification)
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        // Act - Trigger judge phase (this will trigger Lebusishu judgement)
        var phaseStartEvent = new PhaseStartEvent(game, target.Seat, Phase.Judge);
        eventBus.Publish(phaseStartEvent);

        // Execute the stack to complete judgement
        while (!stack.IsEmpty)
        {
            var stackResult = stack.Pop();
            Assert.IsTrue(stackResult.Success, 
                $"Stack execution should succeed. Error: {stackResult.ErrorCode}, Message: {stackResult.MessageKey}");
        }

        // Verify SkipPlayPhase flag is set
        Assert.IsTrue(target.Flags.ContainsKey("SkipPlayPhase"), "SkipPlayPhase flag should be set after non-Heart judgement.");

        // Act - Advance phase from Draw to Play (should skip Play)
        game.CurrentPhase = Phase.Draw;
        var advanceResult = turnEngine.AdvancePhase(game);

        // Assert
        Assert.IsTrue(advanceResult.IsSuccess);
        Assert.AreEqual(Phase.Discard, game.CurrentPhase, "Play phase should be skipped.");
        Assert.IsFalse(target.Flags.ContainsKey("SkipPlayPhase"), "SkipPlayPhase flag should be cleared after skipping.");
    }

    #endregion

    #region Integration Tests - Interaction with Other Tricks

    /// <summary>
    /// Tests that ShunshouQianyang can obtain Lebusishu from target's judgement zone.
    /// Input: Game with 2 players, target has Lebusishu in judgement zone, source player uses ShunshouQianyang.
    /// Expected: Lebusishu card is moved from target's judgement zone to source player's hand.
    /// </summary>
    [TestMethod]
    public void ShunshouQianyangCanObtainLebusishuFromJudgementZone()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];
        var target = game.Players[1];

        var lebusishu = CreateLebusishuCard();
        ((Zone)target.JudgementZone).MutableCards.Add(lebusishu);

        var shunshouQianyang = new Card
        {
            Id = 100,
            DefinitionId = "shunshou_qianyang",
            Name = "顺手牵羊",
            CardType = CardType.Trick,
            CardSubType = CardSubType.ShunshouQianyang,
            Suit = Suit.Heart,
            Rank = 3
        };
        ((Zone)source.HandZone).MutableCards.Add(shunshouQianyang);

        var initialSourceHandCount = source.HandZone.Cards.Count;
        var initialTargetJudgementCount = target.JudgementZone.Cards.Count;

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { target.Seat },
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: null
        );

        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: new[] { lebusishu.Id },
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var context = new ResolutionContext(
            game,
            source,
            Action: null,
            choice,
            stack,
            cardMoveService,
            ruleService,
            GetPlayerChoice: getPlayerChoice
        );

        var resolver = new ShunshouQianyangResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);

        // Execute the stack to complete the effect (nullification window and effect handler)
        while (!stack.IsEmpty)
        {
            var stackResult = stack.Pop();
            Assert.IsTrue(stackResult.Success, 
                $"Stack execution should succeed. Error: {stackResult.ErrorCode}, Message: {stackResult.MessageKey}");
        }

        Assert.AreEqual(initialSourceHandCount + 1, source.HandZone.Cards.Count, "Source player should have 1 more card.");
        Assert.AreEqual(initialTargetJudgementCount - 1, target.JudgementZone.Cards.Count, "Target player should have 1 fewer judgement card.");
        Assert.IsTrue(source.HandZone.Cards.Contains(lebusishu), "Lebusishu card should be in source player's hand.");
        Assert.IsFalse(target.JudgementZone.Cards.Contains(lebusishu), "Lebusishu card should not be in target player's judgement zone.");
    }

    /// <summary>
    /// Tests that GuoheChaiqiao can discard Lebusishu from target's judgement zone.
    /// Input: Game with 2 players, target has Lebusishu in judgement zone, source player uses GuoheChaiqiao.
    /// Expected: Lebusishu card is moved from target's judgement zone to discard pile.
    /// </summary>
    [TestMethod]
    public void GuoheChaiqiaoCanDiscardLebusishuFromJudgementZone()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];
        var target = game.Players[1];

        var lebusishu = CreateLebusishuCard();
        ((Zone)target.JudgementZone).MutableCards.Add(lebusishu);

        var guoheChaiqiao = new Card
        {
            Id = 200,
            DefinitionId = "guohe_chaiqiao",
            Name = "过河拆桥",
            CardType = CardType.Trick,
            CardSubType = CardSubType.GuoheChaiqiao,
            Suit = Suit.Heart,
            Rank = 3
        };
        ((Zone)source.HandZone).MutableCards.Add(guoheChaiqiao);

        var initialTargetJudgementCount = target.JudgementZone.Cards.Count;
        var initialDiscardPileCount = game.DiscardPile.Cards.Count;

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { target.Seat },
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: null
        );

        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: new[] { lebusishu.Id },
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var context = new ResolutionContext(
            game,
            source,
            Action: null,
            choice,
            stack,
            cardMoveService,
            ruleService,
            GetPlayerChoice: getPlayerChoice
        );

        var resolver = new GuoheChaiqiaoResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);

        // Execute the resolution stack to apply the effect
        while (!stack.IsEmpty)
        {
            var stackResult = stack.Pop();
            Assert.IsTrue(stackResult.Success, "Stack execution should succeed.");
        }

        Assert.AreEqual(initialTargetJudgementCount - 1, target.JudgementZone.Cards.Count, "Target player should have 1 fewer judgement card.");
        Assert.AreEqual(initialDiscardPileCount + 1, game.DiscardPile.Cards.Count, "Discard pile should have 1 more card.");
        Assert.IsFalse(target.JudgementZone.Cards.Contains(lebusishu), "Lebusishu card should not be in target player's judgement zone.");
        Assert.IsTrue(game.DiscardPile.Cards.Contains(lebusishu), "Lebusishu card should be in discard pile.");
    }

    #endregion

    #region Helper Classes

    private sealed class TestGameMode : IGameMode
    {
        public string Id => "TestGameMode";
        public string DisplayName => "Test Game Mode";

        public int SelectFirstPlayerSeat(Game game)
        {
            return game.Players.FirstOrDefault(p => p.IsAlive)?.Seat ?? 0;
        }
    }

    #endregion
}
