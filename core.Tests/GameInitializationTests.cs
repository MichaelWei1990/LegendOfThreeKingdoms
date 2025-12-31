using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Configuration;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.GameMode;
using LegendOfThreeKingdoms.Core.GameSetup;
using LegendOfThreeKingdoms.Core.Identity;
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

    [TestMethod]
    public void initializeWithIdentityMode_AssignsRolesAndRevealsLord()
    {
        // Arrange
        var baseConfig = CreateDefaultGameConfig(playerCount: 4);
        var gameConfig = new GameConfig
        {
            PlayerConfigs = baseConfig.PlayerConfigs,
            DeckConfig = baseConfig.DeckConfig,
            Seed = baseConfig.Seed,
            GameModeId = "standard", // Identity mode
            GameVariantOptions = baseConfig.GameVariantOptions,
            InitialHandCardCount = 4
        };

        var random = new FixedRandomSource(0, 1, 2, 3);
        var gameMode = new StandardGameMode();
        var options = new GameInitializationOptions
        {
            GameConfig = gameConfig,
            Random = random,
            GameMode = gameMode
        };

        var initializer = new BasicGameInitializer();

        // Act
        var result = initializer.Initialize(options);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.Game);

        var game = result.Game!;

        // Check that roles are assigned
        var lordCount = game.Players.Count(p => p.CampId == RoleConstants.Lord);
        var loyalistCount = game.Players.Count(p => p.CampId == RoleConstants.Loyalist);
        var rebelCount = game.Players.Count(p => p.CampId == RoleConstants.Rebel);
        var renegadeCount = game.Players.Count(p => p.CampId == RoleConstants.Renegade);

        Assert.AreEqual(1, lordCount, "Should have exactly 1 Lord");
        Assert.AreEqual(1, loyalistCount, "Should have exactly 1 Loyalist");
        Assert.AreEqual(1, rebelCount, "Should have exactly 1 Rebel");
        Assert.AreEqual(1, renegadeCount, "Should have exactly 1 Renegade");

        // Check that Lord's role is revealed
        var lord = game.Players.FirstOrDefault(p => p.CampId == RoleConstants.Lord);
        Assert.IsNotNull(lord);
        Assert.IsTrue(lord.RoleRevealed, "Lord's role should be revealed");

        // Check that other players' roles are not revealed
        var nonLords = game.Players.Where(p => p.CampId != RoleConstants.Lord);
        foreach (var player in nonLords)
        {
            Assert.IsFalse(player.RoleRevealed, $"Player {player.Seat} should not have role revealed");
        }
    }

    [TestMethod]
    public void initializeWithIdentityMode_5Players_AssignsCorrectRoles()
    {
        // Arrange
        var baseConfig = CreateDefaultGameConfig(playerCount: 5);
        var gameConfig = new GameConfig
        {
            PlayerConfigs = baseConfig.PlayerConfigs,
            DeckConfig = baseConfig.DeckConfig,
            Seed = baseConfig.Seed,
            GameModeId = "standard",
            GameVariantOptions = baseConfig.GameVariantOptions,
            InitialHandCardCount = 4
        };

        var random = new FixedRandomSource(0, 1, 2, 3, 4);
        var gameMode = new StandardGameMode();
        var options = new GameInitializationOptions
        {
            GameConfig = gameConfig,
            Random = random,
            GameMode = gameMode
        };

        var initializer = new BasicGameInitializer();

        // Act
        var result = initializer.Initialize(options);

        // Assert
        Assert.IsTrue(result.Success);
        var game = result.Game!;

        var lordCount = game.Players.Count(p => p.CampId == RoleConstants.Lord);
        var loyalistCount = game.Players.Count(p => p.CampId == RoleConstants.Loyalist);
        var rebelCount = game.Players.Count(p => p.CampId == RoleConstants.Rebel);
        var renegadeCount = game.Players.Count(p => p.CampId == RoleConstants.Renegade);

        Assert.AreEqual(1, lordCount);
        Assert.AreEqual(1, loyalistCount);
        Assert.AreEqual(2, rebelCount);
        Assert.AreEqual(1, renegadeCount);
    }

    [TestMethod]
    public void initializeWithNonIdentityMode_DoesNotAssignRoles()
    {
        // Arrange
        var baseConfig = CreateDefaultGameConfig(playerCount: 4);
        var gameConfig = new GameConfig
        {
            PlayerConfigs = baseConfig.PlayerConfigs,
            DeckConfig = baseConfig.DeckConfig,
            Seed = baseConfig.Seed,
            GameModeId = "dummy", // Non-identity mode
            GameVariantOptions = baseConfig.GameVariantOptions,
            InitialHandCardCount = 4
        };

        var random = new FixedRandomSource(0, 1, 2, 3);
        var options = new GameInitializationOptions
        {
            GameConfig = gameConfig,
            Random = random,
            GameMode = new DummyGameMode()
        };

        var initializer = new BasicGameInitializer();

        // Act
        var result = initializer.Initialize(options);

        // Assert
        Assert.IsTrue(result.Success);
        var game = result.Game!;

        // Roles should not be assigned (CampId should be null)
        foreach (var player in game.Players)
        {
            Assert.IsNull(player.CampId, $"Player {player.Seat} should not have CampId assigned in non-identity mode");
            Assert.IsFalse(player.RoleRevealed, $"Player {player.Seat} should not have role revealed");
        }
    }

    [TestMethod]
    public void initializeWithIdentityModeAndEventBus_SetsUpWinConditionChecking()
    {
        // Arrange
        var baseConfig = CreateDefaultGameConfig(playerCount: 4);
        var gameConfig = new GameConfig
        {
            PlayerConfigs = baseConfig.PlayerConfigs,
            DeckConfig = baseConfig.DeckConfig,
            Seed = baseConfig.Seed,
            GameModeId = "standard",
            GameVariantOptions = baseConfig.GameVariantOptions,
            InitialHandCardCount = 4
        };

        var random = new FixedRandomSource(0, 1, 2, 3);
        var eventBus = new BasicEventBus();
        var gameMode = new StandardGameMode();
        var options = new GameInitializationOptions
        {
            GameConfig = gameConfig,
            Random = random,
            GameMode = gameMode,
            EventBus = eventBus
        };

        var initializer = new BasicGameInitializer();

        // Act
        var result = initializer.Initialize(options);

        // Assert
        Assert.IsTrue(result.Success);
        var game = result.Game!;

        // Verify roles are assigned
        var lordCount = game.Players.Count(p => p.CampId == RoleConstants.Lord);
        Assert.AreEqual(1, lordCount);

        // Verify win condition checking is set up by publishing a PlayerDiedEvent
        // and checking if GameEndedEvent is published when win condition is met
        var gameEndedEvents = new List<GameEndedEvent>();
        eventBus.Subscribe<GameEndedEvent>(evt => gameEndedEvents.Add(evt));

        // Simulate game state where Lord and Loyalist are alive, Rebel and Renegade are dead
        // First, mark Rebel and Renegade as dead
        var rebel = game.Players.First(p => p.CampId == RoleConstants.Rebel);
        var renegade = game.Players.First(p => p.CampId == RoleConstants.Renegade);
        rebel.IsAlive = false;
        renegade.IsAlive = false;

        // Publish PlayerDiedEvent for the last enemy (Renegade)
        var playerDiedEvent = new PlayerDiedEvent(game, renegade.Seat, null);
        eventBus.Publish(playerDiedEvent);

        // Assert that GameEndedEvent was published and game is marked as finished
        Assert.AreEqual(1, gameEndedEvents.Count, "GameEndedEvent should be published when win condition is met");
        Assert.IsTrue(game.IsFinished, "Game should be marked as finished");
        Assert.IsNotNull(game.WinnerDescription);
        Assert.IsTrue(game.WinnerDescription.Contains("Lord and Loyalists"));
    }

    private sealed class DummyGameMode : IGameMode
    {
        public string Id => "dummy";

        public string DisplayName => "Dummy";

        public int SelectFirstPlayerSeat(Game game) => 0;
    }
}

