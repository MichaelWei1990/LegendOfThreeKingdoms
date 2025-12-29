using System;
using System.Collections.Generic;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Configuration;
using LegendOfThreeKingdoms.Core.GameSetup;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Replay;
using LegendOfThreeKingdoms.Core.Rules;

namespace core.Tests;

[TestClass]
public sealed class ReplayTests
{
    private sealed class DummyGameMode : IGameMode
    {
        public string Id => "dummy";
        public string DisplayName => "Dummy Mode";

        public int SelectFirstPlayerSeat(Game game)
        {
            return 0;
        }
    }

    private static GameConfig CreateDefaultGameConfig(int playerCount = 2, int? seed = 12345)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount, seed: seed);
        config.DeckConfig.IncludedPacks.Add("Base");
        return config;
    }

    [TestMethod]
    public void ReplayRecord_CanBeCreated()
    {
        var config = CreateDefaultGameConfig(playerCount: 2, seed: 12345);
        var choices = new List<ChoiceResult>();

        var record = new ReplayRecord
        {
            Seed = 12345,
            InitialConfig = config,
            ChoiceSequence = choices
        };

        Assert.IsNotNull(record);
        Assert.AreEqual(12345, record.Seed);
        Assert.AreEqual(config, record.InitialConfig);
        Assert.AreEqual(0, record.ChoiceSequence.Count);
    }

    [TestMethod]
    public void SeededRandomSource_ProducesDeterministicResults()
    {
        var source1 = new SeededRandomSource(12345);
        var source2 = new SeededRandomSource(12345);

        var values1 = new List<int>();
        var values2 = new List<int>();

        for (int i = 0; i < 10; i++)
        {
            values1.Add(source1.NextInt(0, 100));
            values2.Add(source2.NextInt(0, 100));
        }

        CollectionAssert.AreEqual(values1, values2);
    }

    [TestMethod]
    public void SeededRandomSource_WithDifferentSeeds_ProducesDifferentResults()
    {
        var source1 = new SeededRandomSource(12345);
        var source2 = new SeededRandomSource(67890);

        var values1 = new List<int>();
        var values2 = new List<int>();

        for (int i = 0; i < 10; i++)
        {
            values1.Add(source1.NextInt(0, 100));
            values2.Add(source2.NextInt(0, 100));
        }

        CollectionAssert.AreNotEqual(values1, values2);
    }

    [TestMethod]
    public void ReplayChoiceProvider_ReturnsChoicesInOrder()
    {
        var choices = new List<ChoiceResult>
        {
            new ChoiceResult("req1", 0, null, new List<int> { 1 }, null, null),
            new ChoiceResult("req2", 1, null, new List<int> { 2 }, null, null),
            new ChoiceResult("req3", 0, null, new List<int> { 3 }, null, null)
        };

        var provider = new ReplayChoiceProvider(choices);

        var request1 = new ChoiceRequest("req1", 0, ChoiceType.SelectTargets, null, null);
        var choice1 = provider.GetNextChoice(request1);
        Assert.IsNotNull(choice1);
        Assert.AreEqual("req1", choice1.RequestId);
        Assert.AreEqual(0, choice1.PlayerSeat);

        var request2 = new ChoiceRequest("req2", 1, ChoiceType.SelectTargets, null, null);
        var choice2 = provider.GetNextChoice(request2);
        Assert.IsNotNull(choice2);
        Assert.AreEqual("req2", choice2.RequestId);
        Assert.AreEqual(1, choice2.PlayerSeat);

        var request3 = new ChoiceRequest("req3", 0, ChoiceType.SelectTargets, null, null);
        var choice3 = provider.GetNextChoice(request3);
        Assert.IsNotNull(choice3);
        Assert.AreEqual("req3", choice3.RequestId);
        Assert.AreEqual(0, choice3.PlayerSeat);

        Assert.AreEqual(0, provider.RemainingChoices);
        Assert.AreEqual(3, provider.ChoicesConsumed);
    }

    [TestMethod]
    public void ReplayChoiceProvider_ReturnsNull_WhenChoicesExhausted()
    {
        var choices = new List<ChoiceResult>
        {
            new ChoiceResult("req1", 0, null, null, null, null)
        };

        var provider = new ReplayChoiceProvider(choices);

        var request1 = new ChoiceRequest("req1", 0, ChoiceType.SelectTargets, null, null);
        var choice1 = provider.GetNextChoice(request1);
        Assert.IsNotNull(choice1);

        var request2 = new ChoiceRequest("req2", 1, ChoiceType.SelectTargets, null, null);
        var choice2 = provider.GetNextChoice(request2);
        Assert.IsNull(choice2);
    }

    [TestMethod]
    public void BasicReplayEngine_RebuildsGameWithSameSeed()
    {
        var config = CreateDefaultGameConfig(playerCount: 2, seed: 12345);
        var choices = new List<ChoiceResult>();

        var record = new ReplayRecord
        {
            Seed = 12345,
            InitialConfig = config,
            ChoiceSequence = choices
        };

        var gameMode = new DummyGameMode();
        var engine = new BasicReplayEngine(new BasicGameInitializer());

        var result1 = engine.StartReplay(record, gameMode);
        var result2 = engine.StartReplay(record, gameMode);

        Assert.IsTrue(result1.Success);
        Assert.IsTrue(result2.Success);
        Assert.IsNotNull(result1.Game);
        Assert.IsNotNull(result2.Game);

        // Verify that games initialized with the same seed have the same initial state
        var game1 = result1.Game!;
        var game2 = result2.Game!;

        Assert.AreEqual(game1.Players.Count, game2.Players.Count);
        Assert.AreEqual(game1.DrawPile.Cards.Count, game2.DrawPile.Cards.Count);

        // Verify that players have the same number of cards
        for (int i = 0; i < game1.Players.Count; i++)
        {
            Assert.AreEqual(
                game1.Players[i].HandZone.Cards.Count,
                game2.Players[i].HandZone.Cards.Count);
        }
    }

    [TestMethod]
    public void BasicReplayEngine_FailsWithNullConfig()
    {
        var record = new ReplayRecord
        {
            Seed = 12345,
            InitialConfig = null!,
            ChoiceSequence = new List<ChoiceResult>()
        };

        var gameMode = new DummyGameMode();
        var engine = new BasicReplayEngine(new BasicGameInitializer());

        var result = engine.StartReplay(record, gameMode);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("InvalidConfig", result.ErrorCode);
    }

    [TestMethod]
    public void BasicReplayEngine_FailsWithNullChoiceSequence()
    {
        var config = CreateDefaultGameConfig(playerCount: 2, seed: 12345);
        var record = new ReplayRecord
        {
            Seed = 12345,
            InitialConfig = config,
            ChoiceSequence = null!
        };

        var gameMode = new DummyGameMode();
        var engine = new BasicReplayEngine(new BasicGameInitializer());

        var result = engine.StartReplay(record, gameMode);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("InvalidChoiceSequence", result.ErrorCode);
    }

    [TestMethod]
    public void CreateGetPlayerChoiceFunction_ThrowsWhenChoicesExhausted()
    {
        var choices = new List<ChoiceResult>
        {
            new ChoiceResult("req1", 0, null, null, null, null)
        };

        var record = new ReplayRecord
        {
            Seed = 12345,
            InitialConfig = CreateDefaultGameConfig(),
            ChoiceSequence = choices
        };

        var getPlayerChoice = BasicReplayEngine.CreateGetPlayerChoiceFunction(record, out var provider);

        var request1 = new ChoiceRequest("req1", 0, ChoiceType.SelectTargets, null, null);
        var choice1 = getPlayerChoice(request1);
        Assert.IsNotNull(choice1);

        var request2 = new ChoiceRequest("req2", 1, ChoiceType.SelectTargets, null, null);
        Assert.ThrowsException<InvalidOperationException>(() => getPlayerChoice(request2));
    }

    [TestMethod]
    public void ReplayExtensions_CreateReplayEngine()
    {
        var engine = ReplayExtensions.CreateReplayEngine();
        Assert.IsNotNull(engine);
        Assert.IsInstanceOfType(engine, typeof(BasicReplayEngine));
    }

    [TestMethod]
    public void ReplayExtensions_StartReplay()
    {
        var config = CreateDefaultGameConfig(playerCount: 2, seed: 12345);
        var choices = new List<ChoiceResult>();

        var record = new ReplayRecord
        {
            Seed = 12345,
            InitialConfig = config,
            ChoiceSequence = choices
        };

        var gameMode = new DummyGameMode();
        var result = ReplayExtensions.StartReplay(record, gameMode);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.Game);
        Assert.AreEqual(0, result.ChoicesReplayed);
        Assert.AreEqual(0, result.TotalChoices);
    }
}






