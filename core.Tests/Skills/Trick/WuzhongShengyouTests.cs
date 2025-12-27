using System;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests;

[TestClass]
public sealed class WuzhongShengyouTests
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

    private static Card CreateWuzhongShengyouCard(int id = 1)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "wuzhong_shengyou",
            Name = "无中生有",
            CardType = CardType.Trick,
            CardSubType = CardSubType.WuzhongShengyou,
            Suit = Suit.Heart,
            Rank = 7
        };
    }

    #region WuzhongShengyouResolver Tests

    /// <summary>
    /// Tests that WuzhongShengyouResolver draws 2 cards for the user.
    /// Input: Game with cards in draw pile, player, WuzhongShengyouResolver.
    /// Expected: Player's hand has 2 more cards, draw pile has 2 fewer cards.
    /// </summary>
    [TestMethod]
    public void WuzhongShengyouResolverDrawsTwoCards()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(playerCount: 1, cardCount: 10);
        var player = game.Players[0];
        var initialHandCount = player.HandZone.Cards.Count;
        var initialDrawPileCount = game.DrawPile.Cards.Count;

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        var context = new ResolutionContext(
            game,
            player,
            Action: null,
            Choice: null,
            stack,
            cardMoveService,
            ruleService
        );

        var resolver = new WuzhongShengyouResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(initialHandCount + 2, player.HandZone.Cards.Count, "Player should have 2 more cards in hand.");
        Assert.AreEqual(initialDrawPileCount - 2, game.DrawPile.Cards.Count, "Draw pile should have 2 fewer cards.");
    }

    /// <summary>
    /// Tests that WuzhongShengyouResolver handles draw pile being empty gracefully.
    /// Input: Game with empty draw pile, player, WuzhongShengyouResolver.
    /// Expected: Resolver returns failure result with appropriate error code.
    /// </summary>
    [TestMethod]
    public void WuzhongShengyouResolverHandlesEmptyDrawPile()
    {
        // Arrange
        var game = CreateDefaultGame(playerCount: 1);
        var player = game.Players[0];

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        var context = new ResolutionContext(
            game,
            player,
            Action: null,
            Choice: null,
            stack,
            cardMoveService,
            ruleService
        );

        var resolver = new WuzhongShengyouResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual(ResolutionErrorCode.InvalidState, result.ErrorCode);
        Assert.IsNotNull(result.MessageKey);
        Assert.AreEqual("resolution.wuzhongshengyou.drawFailed", result.MessageKey);
    }

    /// <summary>
    /// Tests that WuzhongShengyouResolver handles insufficient cards in draw pile.
    /// Input: Game with only 1 card in draw pile, player, WuzhongShengyouResolver (tries to draw 2).
    /// Expected: Resolver returns failure result.
    /// </summary>
    [TestMethod]
    public void WuzhongShengyouResolverHandlesInsufficientCards()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(playerCount: 1, cardCount: 1);
        var player = game.Players[0];

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        var context = new ResolutionContext(
            game,
            player,
            Action: null,
            Choice: null,
            stack,
            cardMoveService,
            ruleService
        );

        var resolver = new WuzhongShengyouResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual(ResolutionErrorCode.InvalidState, result.ErrorCode);
        Assert.IsNotNull(result.MessageKey);
    }

    #endregion

    #region Integration Tests (UseCardResolver)

    /// <summary>
    /// Verifies that UseCardResolver successfully processes a valid WuzhongShengyou usage through the complete flow.
    /// 
    /// Test scenario:
    /// - Sets up a 2-player game in Play phase
    /// - Source player has a WuzhongShengyou card in hand
    /// - Creates a UseWuzhongShengyou action
    /// - Executes UseCardResolver with the action and choice
    /// 
    /// Expected results:
    /// - Resolution succeeds
    /// - WuzhongShengyou card is moved from source player's hand to the discard pile
    /// - WuzhongShengyouResolver is pushed onto the stack for further processing
    /// - Player draws 2 cards from the draw pile
    /// 
    /// This test verifies the core functionality of UseCardResolver with WuzhongShengyou:
    /// validation, card movement, and delegation to WuzhongShengyouResolver.
    /// </summary>
    [TestMethod]
    public void UseCardResolverProcessesValidWuzhongShengyou()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(playerCount: 2, cardCount: 10);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];

        var wuzhongShengyou = CreateWuzhongShengyouCard();
        ((Zone)source.HandZone).MutableCards.Add(wuzhongShengyou);
        
        // Capture initial counts AFTER adding the card
        var initialHandCount = source.HandZone.Cards.Count;
        var initialDrawPileCount = game.DrawPile.Cards.Count;

        var stack = new BasicResolutionStack();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();

        var action = new ActionDescriptor(
            ActionId: "UseWuzhongShengyou",
            DisplayKey: "action.useWuzhongShengyou",
            RequiresTargets: false,
            TargetConstraints: new TargetConstraints(0, 0, TargetFilterType.Any),
            CardCandidates: new[] { wuzhongShengyou }
        );

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: new[] { wuzhongShengyou.Id },
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
            ruleService
        );

        var resolver = new UseCardResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);

        // Verify card was moved from hand to discard pile
        Assert.IsFalse(source.HandZone.Cards.Contains(wuzhongShengyou), "WuzhongShengyou card should be removed from hand.");
        Assert.IsTrue(game.DiscardPile.Cards.Contains(wuzhongShengyou), "WuzhongShengyou card should be in discard pile.");

        // Verify ImmediateTrickResolver (which will delegate to WuzhongShengyouResolver) was pushed onto the stack
        Assert.IsFalse(stack.IsEmpty, "Stack should not be empty after UseCardResolver.");

        // Execute the stack to trigger WuzhongShengyouResolver
        while (!stack.IsEmpty)
        {
            var stackResult = stack.Pop();
            Assert.IsTrue(stackResult.Success, "Stack execution should succeed.");
        }

        // Verify player drew 2 cards
        Assert.AreEqual(initialHandCount - 1 + 2, source.HandZone.Cards.Count, "Player should have drawn 2 cards (original hand - 1 card used + 2 cards drawn).");
        Assert.AreEqual(initialDrawPileCount - 2, game.DrawPile.Cards.Count, "Draw pile should have 2 fewer cards.");
    }

    /// <summary>
    /// Verifies that UseCardResolver properly handles the error case when the selected WuzhongShengyou card
    /// is not found in the player's hand.
    /// 
    /// Test scenario:
    /// - Sets up a 2-player game in Play phase
    /// - Creates a WuzhongShengyou card but does NOT add it to the source player's hand
    /// - Creates a UseWuzhongShengyou action that references the non-existent card
    /// - Executes UseCardResolver with invalid card selection
    /// 
    /// Expected results:
    /// - Resolution fails with CardNotFound error code
    /// - Card is NOT moved (validation fails before card movement)
    /// - Game state remains unchanged
    /// 
    /// This test ensures that UseCardResolver validates card ownership before attempting
    /// to move cards, preventing invalid state changes.
    /// </summary>
    [TestMethod]
    public void UseCardResolverFailsWhenWuzhongShengyouCardNotFound()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];

        // Don't add card to hand - it should fail
        var wuzhongShengyou = CreateWuzhongShengyouCard();

        var stack = new BasicResolutionStack();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();

        var action = new ActionDescriptor(
            ActionId: "UseWuzhongShengyou",
            DisplayKey: "action.useWuzhongShengyou",
            RequiresTargets: false,
            TargetConstraints: new TargetConstraints(0, 0, TargetFilterType.Any),
            CardCandidates: new[] { wuzhongShengyou }
        );

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: new[] { wuzhongShengyou.Id },
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
            ruleService
        );

        var resolver = new UseCardResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual(ResolutionErrorCode.CardNotFound, result.ErrorCode);
        Assert.IsNotNull(result.MessageKey);

        // Verify card was NOT moved
        Assert.IsFalse(source.HandZone.Cards.Contains(wuzhongShengyou));
        Assert.IsFalse(game.DiscardPile.Cards.Contains(wuzhongShengyou));
    }

    #endregion
}
