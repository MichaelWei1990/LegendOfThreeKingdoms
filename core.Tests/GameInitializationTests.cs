using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Configuration;
using LegendOfThreeKingdoms.Core.GameSetup;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;

namespace core.Tests;

[TestClass]
public sealed class GameInitializationTests
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

    private static GameConfig CreateDefaultGameConfig(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        // Ensure a deterministic included pack so that BuildDeckCardIds returns cards.
        config.DeckConfig.IncludedPacks.Add("Base");
        return config;
    }

    [TestMethod]
    public void initializeDealsInitialHandsAndLeavesRemainingDeck()
    {
        var baseConfig = CreateDefaultGameConfig(playerCount: 3);
        var gameConfig = new GameConfig
        {
            PlayerConfigs = baseConfig.PlayerConfigs,
            DeckConfig = baseConfig.DeckConfig,
            Seed = baseConfig.Seed,
            GameModeId = baseConfig.GameModeId,
            GameVariantOptions = baseConfig.GameVariantOptions,
            InitialHandCardCount = 4
        };

        var random = new FixedRandomSource(0, 1, 2, 3, 4);
        var options = new GameInitializationOptions
        {
            GameConfig = gameConfig,
            Random = random,
            GameMode = new DummyGameMode()
        };

        var initializer = new BasicGameInitializer();

        var result = initializer.Initialize(options);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.Game);

        var game = result.Game!;

        // Each player should have received InitialHandCardCount cards.
        foreach (var player in game.Players)
        {
            Assert.IsInstanceOfType(player.HandZone, typeof(Zone));
            var hand = ((Zone)player.HandZone).Cards;
            Assert.AreEqual(gameConfig.InitialHandCardCount, hand.Count);
        }

        // Total deck size is deterministic in BuildDeckCardIds (20+10+6 = 36).
        // 3 players * 4 cards = 12 cards dealt, so 24 should remain in draw pile.
        Assert.IsInstanceOfType(game.DrawPile, typeof(Zone));
        var remainingDeck = ((Zone)game.DrawPile).Cards;
        Assert.AreEqual(24, remainingDeck.Count);

        // Turn engine integration: first player and phase should be initialized.
        Assert.AreEqual(0, game.CurrentPlayerSeat);
        Assert.AreEqual(Phase.Start, game.CurrentPhase);
        Assert.AreEqual(1, game.TurnNumber);
    }

    [TestMethod]
    public void initializeFailsWhenDeckTooSmallForInitialHands()
    {
        var baseConfig = CreateDefaultGameConfig(playerCount: 4);
        var gameConfig = new GameConfig
        {
            PlayerConfigs = baseConfig.PlayerConfigs,
            DeckConfig = baseConfig.DeckConfig,
            Seed = baseConfig.Seed,
            GameModeId = baseConfig.GameModeId,
            GameVariantOptions = baseConfig.GameVariantOptions,
            InitialHandCardCount = 10 // deliberately too large for the small test deck
        };

        var random = new FixedRandomSource(0);
        var options = new GameInitializationOptions
        {
            GameConfig = gameConfig,
            Random = random,
            GameMode = new DummyGameMode()
        };

        var initializer = new BasicGameInitializer();

        var result = initializer.Initialize(options);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("NotEnoughCardsForInitialHands", result.ErrorCode);
    }

    private sealed class DummyGameMode : IGameMode
    {
        public string Id => "dummy";

        public string DisplayName => "Dummy";

        public int SelectFirstPlayerSeat(Game game) => 0;
    }
}

