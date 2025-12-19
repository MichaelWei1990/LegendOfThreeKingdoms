using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Response;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;

namespace core.Tests;

[TestClass]
public sealed class ResponseTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Card CreateDodgeCard(int id = 1)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "dodge_basic",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Dodge,
            Suit = Suit.Spade,
            Rank = 2
        };
    }

    private static Card CreateSlashCard(int id = 1)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "slash_basic",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = Suit.Spade,
            Rank = 7
        };
    }

    /// <summary>
    /// Verifies that BasicResponseWindow polls players in the correct order.
    /// 
    /// Test scenario:
    /// - Creates a game with 3 players
    /// - Player 1 has a Dodge card
    /// - Opens a response window with responder order [Player 2, Player 1, Player 3]
    /// - Player 2 passes, Player 1 responds
    /// 
    /// Expected: The window polls players in the specified order and stops when a player responds.
    /// </summary>
    [TestMethod]
    public void basicResponseWindowPollsPlayersInOrder()
    {
        var game = CreateDefaultGame(3);
        var player1 = game.Players[0];
        var player2 = game.Players[1];
        var player3 = game.Players[2];

        // Give player 1 a Dodge card (will respond)
        var dodge1 = CreateDodgeCard(1);
        ((Zone)player1.HandZone).MutableCards.Add(dodge1);
        
        // Give player 2 a Dodge card (will pass, but should be polled)
        var dodge2 = CreateDodgeCard(2);
        ((Zone)player2.HandZone).MutableCards.Add(dodge2);

        // Create responder order: [Player 2, Player 1, Player 3]
        var responderOrder = new[] { player2, player1, player3 };

        // Track the order of players polled
        var polledOrder = new List<int>();

        // Create response window context
        var ruleService = new RuleService();
        var responseRuleService = new ResponseRuleService();
        var choiceFactory = new ChoiceRequestFactory();
        var cardMoveService = new BasicCardMoveService();
        var windowContext = new ResponseWindowContext(
            game,
            ResponseType.JinkAgainstSlash,
            responderOrder,
            SourceEvent: null,
            ruleService,
            responseRuleService,
            choiceFactory,
            cardMoveService,
            LogSink: null
        );

        // Create getPlayerChoice function that tracks order and makes player 1 respond
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = request =>
        {
            polledOrder.Add(request.PlayerSeat);
            if (request.PlayerSeat == player1.Seat)
            {
                // Player 1 responds with Dodge
                return new ChoiceResult(
                    request.RequestId,
                    request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: new[] { dodge1.Id },
                    SelectedOptionId: null,
                    Confirmed: null
                );
            }
            // Other players pass
            return new ChoiceResult(
                request.RequestId,
                request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        // Execute response window
        var responseWindow = new BasicResponseWindow();
        var result = responseWindow.Execute(windowContext, getPlayerChoice);

        // Verify polling order: should poll Player 2 first, then Player 1
        Assert.AreEqual(2, polledOrder.Count);
        Assert.AreEqual(player2.Seat, polledOrder[0]);
        Assert.AreEqual(player1.Seat, polledOrder[1]);
        // Player 3 should not be polled because Player 1 responded

        // Verify result
        Assert.AreEqual(ResponseWindowState.ResponseSuccess, result.State);
        Assert.AreEqual(player1, result.Responder);
        Assert.AreEqual(dodge1, result.ResponseCard);
    }

    /// <summary>
    /// Verifies that BasicResponseWindow returns NoResponse when all players pass.
    /// 
    /// Test scenario:
    /// - Creates a game with 2 players
    /// - Player 2 has a Dodge card but chooses not to respond
    /// - Opens a response window for Player 2
    /// 
    /// Expected: The window returns NoResponse state.
    /// </summary>
    [TestMethod]
    public void basicResponseWindowReturnsNoResponseWhenAllPlayersPass()
    {
        var game = CreateDefaultGame(2);
        var player2 = game.Players[1];

        // Give player 2 a Dodge card (but they will choose not to use it)
        var dodge = CreateDodgeCard(1);
        ((Zone)player2.HandZone).MutableCards.Add(dodge);

        // Create responder order: [Player 2]
        var responderOrder = new[] { player2 };

        // Create response window context
        var ruleService = new RuleService();
        var responseRuleService = new ResponseRuleService();
        var choiceFactory = new ChoiceRequestFactory();
        var cardMoveService = new BasicCardMoveService();
        var windowContext = new ResponseWindowContext(
            game,
            ResponseType.JinkAgainstSlash,
            responderOrder,
            SourceEvent: null,
            ruleService,
            responseRuleService,
            choiceFactory,
            cardMoveService,
            LogSink: null
        );

        // Create getPlayerChoice function that makes player 2 pass
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = request =>
        {
            // Player 2 passes (no card selected)
            return new ChoiceResult(
                request.RequestId,
                request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        // Execute response window
        var responseWindow = new BasicResponseWindow();
        var result = responseWindow.Execute(windowContext, getPlayerChoice);

        // Verify result
        Assert.AreEqual(ResponseWindowState.NoResponse, result.State);
        Assert.IsNull(result.Responder);
        Assert.IsNull(result.ResponseCard);

        // Verify Dodge card is still in hand (not moved)
        Assert.IsTrue(player2.HandZone.Cards.Contains(dodge));
    }

    /// <summary>
    /// Verifies that BasicResponseWindow moves the response card from hand to discard pile.
    /// 
    /// Test scenario:
    /// - Creates a game with 2 players
    /// - Player 2 has a Dodge card and responds
    /// - Opens a response window for Player 2
    /// 
    /// Expected: The Dodge card is moved from Player 2's hand to the discard pile.
    /// </summary>
    [TestMethod]
    public void basicResponseWindowMovesResponseCardToDiscardPile()
    {
        var game = CreateDefaultGame(2);
        var player2 = game.Players[1];

        // Give player 2 a Dodge card
        var dodge = CreateDodgeCard(1);
        ((Zone)player2.HandZone).MutableCards.Add(dodge);

        var initialHandCount = player2.HandZone.Cards.Count;
        var initialDiscardCount = game.DiscardPile.Cards.Count;

        // Create responder order: [Player 2]
        var responderOrder = new[] { player2 };

        // Create response window context
        var ruleService = new RuleService();
        var responseRuleService = new ResponseRuleService();
        var choiceFactory = new ChoiceRequestFactory();
        var cardMoveService = new BasicCardMoveService();
        var windowContext = new ResponseWindowContext(
            game,
            ResponseType.JinkAgainstSlash,
            responderOrder,
            SourceEvent: null,
            ruleService,
            responseRuleService,
            choiceFactory,
            cardMoveService,
            LogSink: null
        );

        // Create getPlayerChoice function that makes player 2 respond
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = request =>
        {
            // Player 2 responds with Dodge
            return new ChoiceResult(
                request.RequestId,
                request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: new[] { dodge.Id },
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        // Execute response window
        var responseWindow = new BasicResponseWindow();
        var result = responseWindow.Execute(windowContext, getPlayerChoice);

        // Verify result
        Assert.AreEqual(ResponseWindowState.ResponseSuccess, result.State);

        // Verify card movement
        Assert.IsFalse(player2.HandZone.Cards.Contains(dodge));
        Assert.IsTrue(game.DiscardPile.Cards.Contains(dodge));
        Assert.AreEqual(initialHandCount - 1, player2.HandZone.Cards.Count);
        Assert.AreEqual(initialDiscardCount + 1, game.DiscardPile.Cards.Count);
    }

    /// <summary>
    /// Verifies that ResponseWindowResolver can be pushed onto the resolution stack and executed.
    /// 
    /// Test scenario:
    /// - Creates a game with 2 players
    /// - Player 2 has a Dodge card and responds
    /// - Creates a ResponseWindowResolver and pushes it onto the stack
    /// 
    /// Expected: The resolver executes successfully and the response card is moved.
    /// </summary>
    [TestMethod]
    public void responseWindowResolverCanBePushedOntoResolutionStack()
    {
        var game = CreateDefaultGame(2);
        var player2 = game.Players[1];

        // Give player 2 a Dodge card
        var dodge = CreateDodgeCard(1);
        ((Zone)player2.HandZone).MutableCards.Add(dodge);

        // Create responder order: [Player 2]
        var responderOrder = new[] { player2 };

        // Create response window context
        var ruleService = new RuleService();
        var responseRuleService = new ResponseRuleService();
        var choiceFactory = new ChoiceRequestFactory();
        var cardMoveService = new BasicCardMoveService();
        var windowContext = new ResponseWindowContext(
            game,
            ResponseType.JinkAgainstSlash,
            responderOrder,
            SourceEvent: null,
            ruleService,
            responseRuleService,
            choiceFactory,
            cardMoveService,
            LogSink: null
        );

        // Create getPlayerChoice function that makes player 2 respond
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = request =>
        {
            // Player 2 responds with Dodge
            return new ChoiceResult(
                request.RequestId,
                request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: new[] { dodge.Id },
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        // Create resolver
        var resolver = new ResponseWindowResolver(windowContext, getPlayerChoice);

        // Create resolution context with IntermediateResults
        var stack = new BasicResolutionStack();
        var intermediateResults = new Dictionary<string, object>();
        var resolutionContext = new ResolutionContext(
            game,
            player2,
            Action: null,
            Choice: null,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            EventBus: null,
            IntermediateResults: intermediateResults
        );

        // Push resolver onto stack
        stack.Push(resolver, resolutionContext);

        // Execute resolver
        var result = stack.Pop();

        // Verify result
        Assert.IsTrue(result.Success);

        // Verify card movement
        Assert.IsFalse(player2.HandZone.Cards.Contains(dodge));
        Assert.IsTrue(game.DiscardPile.Cards.Contains(dodge));
    }

    /// <summary>
    /// Verifies that ResponseExtensions.CreateJinkResponseWindow creates a valid response window.
    /// 
    /// Test scenario:
    /// - Creates a game with 2 players
    /// - Player 2 has a Dodge card and responds
    /// - Uses the extension method to create a Jink response window
    /// 
    /// Expected: The response window is created and executes successfully.
    /// </summary>
    [TestMethod]
    public void responseExtensionsCreateJinkResponseWindow()
    {
        var game = CreateDefaultGame(2);
        var player1 = game.Players[0];
        var player2 = game.Players[1];

        // Give player 2 a Dodge card
        var dodge = CreateDodgeCard(1);
        ((Zone)player2.HandZone).MutableCards.Add(dodge);

        // Create resolution context with IntermediateResults
        var ruleService = new RuleService();
        var cardMoveService = new BasicCardMoveService();
        var stack = new BasicResolutionStack();
        var intermediateResults = new Dictionary<string, object>();
        var resolutionContext = new ResolutionContext(
            game,
            player1,
            Action: null,
            Choice: null,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            EventBus: null,
            IntermediateResults: intermediateResults
        );

        // Create getPlayerChoice function that makes player 2 respond
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = request =>
        {
            // Player 2 responds with Dodge
            return new ChoiceResult(
                request.RequestId,
                request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: new[] { dodge.Id },
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        // Create response window using extension method
        var resolver = resolutionContext.CreateJinkResponseWindow(
            player2,
            sourceEvent: null,
            getPlayerChoice
        );

        // Push resolver onto stack
        stack.Push(resolver, resolutionContext);

        // Execute resolver
        var result = stack.Pop();

        // Verify result
        Assert.IsTrue(result.Success);

        // Verify card movement
        Assert.IsFalse(player2.HandZone.Cards.Contains(dodge));
        Assert.IsTrue(game.DiscardPile.Cards.Contains(dodge));
    }

    /// <summary>
    /// Verifies that BasicResponseWindow stops polling after the first successful response.
    /// 
    /// Test scenario:
    /// - Creates a game with 3 players
    /// - All players have Dodge cards
    /// - Opens a response window with responder order [Player 1, Player 2, Player 3]
    /// - Player 1 responds
    /// 
    /// Expected: The window stops after Player 1 responds and does not poll Player 2 or Player 3.
    /// </summary>
    [TestMethod]
    public void basicResponseWindowStopsAfterFirstResponse()
    {
        var game = CreateDefaultGame(3);
        var player1 = game.Players[0];
        var player2 = game.Players[1];
        var player3 = game.Players[2];

        // Give all players Dodge cards
        var dodge1 = CreateDodgeCard(1);
        var dodge2 = CreateDodgeCard(2);
        var dodge3 = CreateDodgeCard(3);
        ((Zone)player1.HandZone).MutableCards.Add(dodge1);
        ((Zone)player2.HandZone).MutableCards.Add(dodge2);
        ((Zone)player3.HandZone).MutableCards.Add(dodge3);

        // Create responder order: [Player 1, Player 2, Player 3]
        var responderOrder = new[] { player1, player2, player3 };

        // Track the order of players polled
        var polledOrder = new List<int>();

        // Create response window context
        var ruleService = new RuleService();
        var responseRuleService = new ResponseRuleService();
        var choiceFactory = new ChoiceRequestFactory();
        var cardMoveService = new BasicCardMoveService();
        var windowContext = new ResponseWindowContext(
            game,
            ResponseType.JinkAgainstSlash,
            responderOrder,
            SourceEvent: null,
            ruleService,
            responseRuleService,
            choiceFactory,
            cardMoveService,
            LogSink: null
        );

        // Create getPlayerChoice function that makes player 1 respond
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = request =>
        {
            polledOrder.Add(request.PlayerSeat);
            if (request.PlayerSeat == player1.Seat)
            {
                // Player 1 responds with Dodge
                return new ChoiceResult(
                    request.RequestId,
                    request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: new[] { dodge1.Id },
                    SelectedOptionId: null,
                    Confirmed: null
                );
            }
            // Other players would pass, but should not be polled
            return new ChoiceResult(
                request.RequestId,
                request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        // Execute response window
        var responseWindow = new BasicResponseWindow();
        var result = responseWindow.Execute(windowContext, getPlayerChoice);

        // Verify only Player 1 was polled
        Assert.AreEqual(1, polledOrder.Count);
        Assert.AreEqual(player1.Seat, polledOrder[0]);

        // Verify result
        Assert.AreEqual(ResponseWindowState.ResponseSuccess, result.State);
        Assert.AreEqual(player1, result.Responder);

        // Verify only Player 1's card was moved
        Assert.IsFalse(player1.HandZone.Cards.Contains(dodge1));
        Assert.IsTrue(player2.HandZone.Cards.Contains(dodge2));
        Assert.IsTrue(player3.HandZone.Cards.Contains(dodge3));
    }

    /// <summary>
    /// Verifies that BasicResponseWindow logs events when a LogSink is provided.
    /// 
    /// Test scenario:
    /// - Creates a game with 2 players
    /// - Player 2 has a Dodge card and responds
    /// - Provides a LogSink to capture log entries
    /// 
    /// Expected: The window logs ResponseWindowOpened, ResponseCardPlayed, and ResponseWindowClosed events.
    /// </summary>
    [TestMethod]
    public void basicResponseWindowLogsEvents()
    {
        var game = CreateDefaultGame(2);
        var player2 = game.Players[1];

        // Give player 2 a Dodge card
        var dodge = CreateDodgeCard(1);
        ((Zone)player2.HandZone).MutableCards.Add(dodge);

        // Create responder order: [Player 2]
        var responderOrder = new[] { player2 };

        // Create log sink to capture events
        var logEntries = new List<LogEntry>();
        var logSink = new TestLogSink(logEntries);

        // Create response window context
        var ruleService = new RuleService();
        var responseRuleService = new ResponseRuleService();
        var choiceFactory = new ChoiceRequestFactory();
        var cardMoveService = new BasicCardMoveService();
        var windowContext = new ResponseWindowContext(
            game,
            ResponseType.JinkAgainstSlash,
            responderOrder,
            SourceEvent: null,
            ruleService,
            responseRuleService,
            choiceFactory,
            cardMoveService,
            LogSink: logSink
        );

        // Create getPlayerChoice function that makes player 2 respond
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = request =>
        {
            // Player 2 responds with Dodge
            return new ChoiceResult(
                request.RequestId,
                request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: new[] { dodge.Id },
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        // Execute response window
        var responseWindow = new BasicResponseWindow();
        var result = responseWindow.Execute(windowContext, getPlayerChoice);

        // Verify result
        Assert.AreEqual(ResponseWindowState.ResponseSuccess, result.State);

        // Verify log entries
        Assert.IsTrue(logEntries.Any(e => e.EventType == "ResponseWindowOpened"));
        Assert.IsTrue(logEntries.Any(e => e.EventType == "ResponseCardPlayed"));
        // Note: ResponseWindowClosed is not logged when a response succeeds, only when no response
    }

    /// <summary>
    /// Test implementation of ILogSink that captures log entries for testing.
    /// </summary>
    private sealed class TestLogSink : ILogSink
    {
        private readonly List<LogEntry> _entries;

        public TestLogSink(List<LogEntry> entries)
        {
            _entries = entries;
        }

        public void Log(LogEntry entry)
        {
            _entries.Add(entry);
        }
    }
}
