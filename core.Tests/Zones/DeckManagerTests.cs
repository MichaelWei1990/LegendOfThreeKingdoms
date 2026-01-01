using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests.Zones;

[TestClass]
public sealed class DeckManagerTests
{
    private sealed class FixedRandomSource : IRandomSource
    {
        private readonly int[] _values;
        private int _index;

        public FixedRandomSource(params int[] values)
        {
            _values = values;
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (_values.Length == 0)
            {
                return minInclusive;
            }

            var value = _values[_index % _values.Length];
            _index++;
            return Math.Clamp(value, minInclusive, maxExclusive - 1);
        }
    }

    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Card CreateTestCard(int id, CardSubType subType = CardSubType.Slash)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"test_card_{id}",
            CardType = CardType.Basic,
            CardSubType = subType,
            Suit = Suit.Spade,
            Rank = 5
        };
    }

    private static Game CreateGameWithCardsInDrawPile(int playerCount = 2, int cardCount = 10)
    {
        var game = CreateDefaultGame(playerCount);

        // Add cards to draw pile
        if (game.DrawPile is Zone drawZone)
        {
            for (int i = 0; i < cardCount; i++)
            {
                var card = CreateTestCard(i + 1);
                drawZone.MutableCards.Add(card);
            }
        }

        return game;
    }

    private static Game CreateGameWithCardsInDiscardPile(int playerCount = 2, int cardCount = 10)
    {
        var game = CreateDefaultGame(playerCount);

        // Add cards to discard pile
        if (game.DiscardPile is Zone discardZone)
        {
            for (int i = 0; i < cardCount; i++)
            {
                var card = CreateTestCard(i + 1);
                discardZone.MutableCards.Add(card);
            }
        }

        return game;
    }

    private static DeckManager CreateDeckManager(ICardMoveService cardMoveService, IRandomSource random, IEventBus? eventBus = null)
    {
        return new DeckManager(cardMoveService, random, eventBus);
    }

    #region Draw Tests

    /// <summary>
    /// Tests that Draw returns the requested count when draw pile has sufficient cards.
    /// Input: Game with 10 cards in draw pile, request 3 cards.
    /// Expected: Returns 3 cards, draw pile has 7 cards remaining.
    /// </summary>
    [TestMethod]
    public void Draw_WithSufficientCards_ReturnsRequestedCount()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(playerCount: 2, cardCount: 10);
        var initialDrawPileCount = game.DrawPile.Cards.Count;
        var cardMoveService = new BasicCardMoveService();
        var random = new SeededRandomSource(42);
        var deckManager = CreateDeckManager(cardMoveService, random);

        // Act
        var drawn = deckManager.Draw(game, count: 3);

        // Assert
        Assert.AreEqual(3, drawn.Count, "Should return exactly 3 cards.");
        Assert.AreEqual(initialDrawPileCount - 3, game.DrawPile.Cards.Count, "Draw pile should have 7 cards remaining.");
        Assert.IsTrue(drawn.All(c => c != null), "All drawn cards should be non-null.");
    }

    /// <summary>
    /// Tests that Draw with zero count returns empty list.
    /// Input: Game with cards, request 0 cards.
    /// Expected: Returns empty list, draw pile unchanged.
    /// </summary>
    [TestMethod]
    public void Draw_WithZeroCount_ReturnsEmpty()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(playerCount: 2, cardCount: 10);
        var initialDrawPileCount = game.DrawPile.Cards.Count;
        var cardMoveService = new BasicCardMoveService();
        var random = new SeededRandomSource(42);
        var deckManager = CreateDeckManager(cardMoveService, random);

        // Act
        var drawn = deckManager.Draw(game, count: 0);

        // Assert
        Assert.AreEqual(0, drawn.Count, "Should return empty list for zero count.");
        Assert.AreEqual(initialDrawPileCount, game.DrawPile.Cards.Count, "Draw pile should be unchanged.");
    }

    /// <summary>
    /// Tests that Draw reshuffles discard pile when draw pile is insufficient.
    /// Input: Game with 2 cards in draw pile, 5 cards in discard pile, request 4 cards.
    /// Expected: Returns 4 cards (2 from draw + 2 from reshuffled discard), discard pile empty.
    /// </summary>
    [TestMethod]
    public void Draw_WithInsufficientCards_ReshufflesAndReturnsAll()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(playerCount: 2, cardCount: 2);
        var discardZone = game.DiscardPile as Zone;
        Assert.IsNotNull(discardZone, "Discard pile should be a Zone.");

        // Add 5 cards to discard pile
        for (int i = 0; i < 5; i++)
        {
            var card = CreateTestCard(100 + i);
            discardZone.MutableCards.Add(card);
        }

        var initialDiscardCount = game.DiscardPile.Cards.Count;
        var cardMoveService = new BasicCardMoveService();
        var random = new SeededRandomSource(42);
        var deckManager = CreateDeckManager(cardMoveService, random);

        // Act
        var drawn = deckManager.Draw(game, count: 4);

        // Assert
        Assert.AreEqual(4, drawn.Count, "Should return exactly 4 cards after reshuffle.");
        Assert.AreEqual(0, game.DiscardPile.Cards.Count, "Discard pile should be empty after reshuffle.");
        Assert.AreEqual(initialDiscardCount + 2 - 4, game.DrawPile.Cards.Count, "Draw pile should have remaining cards (2 original + 5 from discard - 4 drawn = 3).");
    }

    /// <summary>
    /// Tests that Draw returns partial count when discard pile is empty.
    /// Input: Game with 2 cards in draw pile, empty discard pile, request 4 cards.
    /// Expected: Returns 2 cards (all available), draw pile empty.
    /// </summary>
    [TestMethod]
    public void Draw_WithEmptyDiscardPile_ReturnsPartialCount()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(playerCount: 2, cardCount: 2);
        var initialDrawPileCount = game.DrawPile.Cards.Count;
        var cardMoveService = new BasicCardMoveService();
        var random = new SeededRandomSource(42);
        var deckManager = CreateDeckManager(cardMoveService, random);

        // Act
        var drawn = deckManager.Draw(game, count: 4);

        // Assert
        Assert.AreEqual(initialDrawPileCount, drawn.Count, "Should return all available cards (2).");
        Assert.AreEqual(0, game.DrawPile.Cards.Count, "Draw pile should be empty.");
        Assert.AreEqual(0, game.DiscardPile.Cards.Count, "Discard pile should remain empty.");
    }

    /// <summary>
    /// Tests that Draw handles multiple reshuffles correctly.
    /// Input: Game with 1 card in draw pile, 3 cards in discard pile, request 5 cards.
    /// Expected: Returns 4 cards (1 + 3), both piles empty.
    /// </summary>
    [TestMethod]
    public void Draw_MultipleReshuffles_HandlesCorrectly()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(playerCount: 2, cardCount: 1);
        var discardZone = game.DiscardPile as Zone;
        Assert.IsNotNull(discardZone, "Discard pile should be a Zone.");

        // Add 3 cards to discard pile
        for (int i = 0; i < 3; i++)
        {
            var card = CreateTestCard(200 + i);
            discardZone.MutableCards.Add(card);
        }

        var cardMoveService = new BasicCardMoveService();
        var random = new SeededRandomSource(42);
        var deckManager = CreateDeckManager(cardMoveService, random);

        // Act
        var drawn = deckManager.Draw(game, count: 5);

        // Assert
        Assert.AreEqual(4, drawn.Count, "Should return all available cards (1 + 3 = 4).");
        Assert.AreEqual(0, game.DrawPile.Cards.Count, "Draw pile should be empty.");
        Assert.AreEqual(0, game.DiscardPile.Cards.Count, "Discard pile should be empty.");
    }

    /// <summary>
    /// Tests that Draw reshuffles discard pile correctly using Fisher-Yates algorithm.
    /// Input: Game with empty draw pile, 5 cards in discard pile with known order, request 5 cards.
    /// Expected: Returns 5 cards in shuffled order (different from original order).
    /// </summary>
    [TestMethod]
    public void Draw_ReshufflesDiscardPile_ShufflesCorrectly()
    {
        // Arrange
        var game = CreateDefaultGame(playerCount: 2);
        var discardZone = game.DiscardPile as Zone;
        Assert.IsNotNull(discardZone, "Discard pile should be a Zone.");

        // Add 5 cards to discard pile in known order
        var originalCards = new List<Card>();
        for (int i = 0; i < 5; i++)
        {
            var card = CreateTestCard(300 + i);
            originalCards.Add(card);
            discardZone.MutableCards.Add(card);
        }

        var originalOrder = originalCards.Select(c => c.Id).ToList();

        // Use a fixed random source that will produce a predictable shuffle
        // For Fisher-Yates, we need random values for indices
        // Using FixedRandomSource with values that will cause swaps
        var cardMoveService = new BasicCardMoveService();
        var random = new FixedRandomSource(3, 2, 1, 0); // Will cause swaps at different positions
        var deckManager = CreateDeckManager(cardMoveService, random);

        // Act
        var drawn = deckManager.Draw(game, count: 5);

        // Assert
        Assert.AreEqual(5, drawn.Count, "Should return all 5 cards.");
        Assert.AreEqual(0, game.DiscardPile.Cards.Count, "Discard pile should be empty.");
        
        // Verify that the order is different (shuffled)
        var drawnOrder = drawn.Select(c => c.Id).ToList();
        // Note: Due to the way Fisher-Yates works with our fixed random, the order should be different
        // We verify that at least some cards are in different positions
        bool orderChanged = false;
        for (int i = 0; i < Math.Min(originalOrder.Count, drawnOrder.Count); i++)
        {
            if (originalOrder[i] != drawnOrder[i])
            {
                orderChanged = true;
                break;
            }
        }
        // With our fixed random source, the shuffle should change the order
        Assert.IsTrue(orderChanged || drawnOrder.Count != originalOrder.Count, "Cards should be shuffled (order should change).");
    }

    #endregion

    #region GetDrawPileCount and GetDiscardPileCount Tests

    /// <summary>
    /// Tests that GetDrawPileCount returns correct count.
    /// Input: Game with 10 cards in draw pile.
    /// Expected: Returns 10.
    /// </summary>
    [TestMethod]
    public void GetDrawPileCount_ReturnsCorrectCount()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(playerCount: 2, cardCount: 10);
        var cardMoveService = new BasicCardMoveService();
        var random = new SeededRandomSource(42);
        var deckManager = CreateDeckManager(cardMoveService, random);

        // Act
        var count = deckManager.GetDrawPileCount(game);

        // Assert
        Assert.AreEqual(10, count, "Should return correct draw pile count.");
    }

    /// <summary>
    /// Tests that GetDiscardPileCount returns correct count.
    /// Input: Game with 5 cards in discard pile.
    /// Expected: Returns 5.
    /// </summary>
    [TestMethod]
    public void GetDiscardPileCount_ReturnsCorrectCount()
    {
        // Arrange
        var game = CreateGameWithCardsInDiscardPile(playerCount: 2, cardCount: 5);
        var cardMoveService = new BasicCardMoveService();
        var random = new SeededRandomSource(42);
        var deckManager = CreateDeckManager(cardMoveService, random);

        // Act
        var count = deckManager.GetDiscardPileCount(game);

        // Assert
        Assert.AreEqual(5, count, "Should return correct discard pile count.");
    }

    #endregion

    #region Edge Cases and Error Handling

    /// <summary>
    /// Tests that Draw throws ArgumentNullException when game is null.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Draw_WithNullGame_ThrowsArgumentNullException()
    {
        // Arrange
        var cardMoveService = new BasicCardMoveService();
        var random = new SeededRandomSource(42);
        var deckManager = CreateDeckManager(cardMoveService, random);

        // Act
        deckManager.Draw(game: null!, count: 1);
    }

    /// <summary>
    /// Tests that Draw throws ArgumentOutOfRangeException when count is negative.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void Draw_WithNegativeCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var game = CreateDefaultGame();
        var cardMoveService = new BasicCardMoveService();
        var random = new SeededRandomSource(42);
        var deckManager = CreateDeckManager(cardMoveService, random);

        // Act
        deckManager.Draw(game, count: -1);
    }

    /// <summary>
    /// Tests that GetDrawPileCount throws ArgumentNullException when game is null.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void GetDrawPileCount_WithNullGame_ThrowsArgumentNullException()
    {
        // Arrange
        var cardMoveService = new BasicCardMoveService();
        var random = new SeededRandomSource(42);
        var deckManager = CreateDeckManager(cardMoveService, random);

        // Act
        deckManager.GetDrawPileCount(game: null!);
    }

    /// <summary>
    /// Tests that GetDiscardPileCount throws ArgumentNullException when game is null.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void GetDiscardPileCount_WithNullGame_ThrowsArgumentNullException()
    {
        // Arrange
        var cardMoveService = new BasicCardMoveService();
        var random = new SeededRandomSource(42);
        var deckManager = CreateDeckManager(cardMoveService, random);

        // Act
        deckManager.GetDiscardPileCount(game: null!);
    }

    #endregion

    #region Integration with CardMoveService

    /// <summary>
    /// Tests that reshuffle operation publishes CardMovedEvent through CardMoveService.
    /// Input: Game with empty draw pile, cards in discard pile, event bus subscribed.
    /// Expected: CardMovedEvent is published when reshuffle occurs.
    /// </summary>
    [TestMethod]
    public void Draw_WithReshuffle_PublishesCardMovedEvents()
    {
        // Arrange
        var game = CreateDefaultGame(playerCount: 2);
        var discardZone = game.DiscardPile as Zone;
        Assert.IsNotNull(discardZone, "Discard pile should be a Zone.");

        // Add 3 cards to discard pile
        for (int i = 0; i < 3; i++)
        {
            var card = CreateTestCard(400 + i);
            discardZone.MutableCards.Add(card);
        }

        var eventBus = new BasicEventBus();
        var publishedEvents = new List<CardMovedEvent>();
        eventBus.Subscribe<CardMovedEvent>(evt => publishedEvents.Add(evt));

        var cardMoveService = new BasicCardMoveService(eventBus: eventBus);
        var random = new SeededRandomSource(42);
        var deckManager = CreateDeckManager(cardMoveService, random, eventBus);

        // Act
        var drawn = deckManager.Draw(game, count: 3);

        // Assert
        Assert.AreEqual(3, drawn.Count, "Should return 3 cards.");
        // Verify that CardMovedEvent was published for the reshuffle operation
        Assert.IsTrue(publishedEvents.Count > 0, "Should publish CardMovedEvent for reshuffle.");
        var reshuffleEvent = publishedEvents.FirstOrDefault(e => e.CardMoveEvent.Reason == CardMoveReason.ReturnToDeckBottom);
        Assert.IsNotNull(reshuffleEvent, "Should publish CardMovedEvent with ReturnToDeckBottom reason.");
    }

    #endregion
}
