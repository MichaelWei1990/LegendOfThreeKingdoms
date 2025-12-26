using System;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Skills.Hero;
using LegendOfThreeKingdoms.Core.Turns;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests;

[TestClass]
public sealed class KejiTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Card CreateSlashCard(int id = 1)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "slash",
            Name = "杀",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = Suit.Spade,
            Rank = 5
        };
    }

    private static Card CreateOtherCard(int id = 2)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "other",
            Name = "其他",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Peach,
            Suit = Suit.Heart,
            Rank = 1
        };
    }

    #region Skill Factory Tests

    /// <summary>
    /// Tests that KejiSkillFactory creates correct skill instance.
    /// Input: KejiSkillFactory instance.
    /// Expected: Created skill has correct Id, Name, and SkillType.Locked.
    /// </summary>
    [TestMethod]
    public void KejiSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new KejiSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("keji", skill.Id);
        Assert.AreEqual("克己", skill.Name);
        Assert.AreEqual(SkillType.Locked, skill.Type);
    }

    /// <summary>
    /// Tests that SkillRegistry can register and retrieve Keji skill.
    /// Input: Empty registry, KejiSkillFactory.
    /// Expected: After registration, GetSkill returns a skill with correct Id.
    /// </summary>
    [TestMethod]
    public void SkillRegistryRegisterKejiSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new KejiSkillFactory();

        // Act
        registry.RegisterSkill("keji", factory);
        var skill = registry.GetSkill("keji");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("keji", skill.Id);
        Assert.AreEqual("克己", skill.Name);
    }

    #endregion

    #region Phase Skipping Tests

    /// <summary>
    /// Tests that KejiSkill skips discard phase when no Slash was used during play phase.
    /// Input: Game with 2 players, player has Keji skill, play phase ends without using Slash.
    /// Expected: SkipDiscardPhase flag is set, discard phase is skipped.
    /// </summary>
    [TestMethod]
    public void KejiSkillSkipsDiscardPhaseWhenNoSlashUsed()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        game.CurrentPlayerSeat = player.Seat;
        game.CurrentPhase = Phase.Play;

        // Setup skill manager and register Keji skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("keji", new KejiSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Keji skill to player
        var kejiSkill = skillRegistry.GetSkill("keji");
        if (kejiSkill is not null)
        {
            skillManager.AddEquipmentSkill(game, player, kejiSkill);
        }

        // Act - End play phase (simulate by publishing PhaseEndEvent)
        var phaseEndEvent = new PhaseEndEvent(game, player.Seat, Phase.Play);
        eventBus.Publish(phaseEndEvent);

        // Assert
        Assert.IsTrue(player.Flags.ContainsKey("SkipDiscardPhase"), 
            "SkipDiscardPhase flag should be set when no Slash was used during play phase.");
        Assert.IsTrue(player.Flags["SkipDiscardPhase"] is true, 
            "SkipDiscardPhase flag should be true.");
    }

    /// <summary>
    /// Tests that KejiSkill does NOT skip discard phase when Slash was used during play phase.
    /// Input: Game with 2 players, player has Keji skill, Slash was used during play phase.
    /// Expected: SkipDiscardPhase flag is NOT set, discard phase is NOT skipped.
    /// </summary>
    [TestMethod]
    public void KejiSkillDoesNotSkipDiscardPhaseWhenSlashUsed()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        game.CurrentPlayerSeat = player.Seat;
        game.CurrentPhase = Phase.Play;

        // Setup skill manager and register Keji skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("keji", new KejiSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Keji skill to player
        var kejiSkill = skillRegistry.GetSkill("keji");
        if (kejiSkill is not null)
        {
            skillManager.AddEquipmentSkill(game, player, kejiSkill);
        }

        // Simulate Slash being used during play phase
        var slash = CreateSlashCard();
        var cardUsedEvent = new CardUsedEvent(game, player.Seat, slash.Id, CardSubType.Slash);
        eventBus.Publish(cardUsedEvent);

        // Act - End play phase
        var phaseEndEvent = new PhaseEndEvent(game, player.Seat, Phase.Play);
        eventBus.Publish(phaseEndEvent);

        // Assert
        Assert.IsFalse(player.Flags.ContainsKey("SkipDiscardPhase"), 
            "SkipDiscardPhase flag should NOT be set when Slash was used during play phase.");
    }

    /// <summary>
    /// Tests that KejiSkill does NOT skip discard phase when Slash was played (in response) during play phase.
    /// Input: Game with 2 players, player has Keji skill, Slash was played in response during play phase.
    /// Expected: SkipDiscardPhase flag is NOT set, discard phase is NOT skipped.
    /// </summary>
    [TestMethod]
    public void KejiSkillDoesNotSkipDiscardPhaseWhenSlashPlayed()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        game.CurrentPlayerSeat = player.Seat;
        game.CurrentPhase = Phase.Play;

        // Setup skill manager and register Keji skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("keji", new KejiSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Keji skill to player
        var kejiSkill = skillRegistry.GetSkill("keji");
        if (kejiSkill is not null)
        {
            skillManager.AddEquipmentSkill(game, player, kejiSkill);
        }

        // Simulate Slash being played in response during play phase
        var slash = CreateSlashCard();
        var cardPlayedEvent = new CardPlayedEvent(
            game, 
            player.Seat, 
            slash.Id, 
            CardSubType.Slash, 
            ResponseType.SlashAgainstDuel);
        eventBus.Publish(cardPlayedEvent);

        // Act - End play phase
        var phaseEndEvent = new PhaseEndEvent(game, player.Seat, Phase.Play);
        eventBus.Publish(phaseEndEvent);

        // Assert
        Assert.IsFalse(player.Flags.ContainsKey("SkipDiscardPhase"), 
            "SkipDiscardPhase flag should NOT be set when Slash was played (in response) during play phase.");
    }

    /// <summary>
    /// Tests that KejiSkill resets tracking flag when play phase starts.
    /// Input: Game with 2 players, player has Keji skill, Slash was used in previous play phase.
    /// Expected: Tracking flag is reset when new play phase starts.
    /// </summary>
    [TestMethod]
    public void KejiSkillResetsTrackingFlagWhenPlayPhaseStarts()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        game.CurrentPlayerSeat = player.Seat;
        game.CurrentPhase = Phase.Play;

        // Setup skill manager and register Keji skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("keji", new KejiSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Keji skill to player
        var kejiSkill = skillRegistry.GetSkill("keji");
        if (kejiSkill is not null)
        {
            skillManager.AddEquipmentSkill(game, player, kejiSkill);
        }

        // Simulate Slash being used in first play phase
        var slash = CreateSlashCard();
        var cardUsedEvent = new CardUsedEvent(game, player.Seat, slash.Id, CardSubType.Slash);
        eventBus.Publish(cardUsedEvent);

        // End first play phase
        var phaseEndEvent1 = new PhaseEndEvent(game, player.Seat, Phase.Play);
        eventBus.Publish(phaseEndEvent1);

        // Act - Start new play phase (should reset tracking)
        game.CurrentPhase = Phase.Play;
        var phaseStartEvent = new PhaseStartEvent(game, player.Seat, Phase.Play);
        eventBus.Publish(phaseStartEvent);

        // End second play phase (without using Slash)
        var phaseEndEvent2 = new PhaseEndEvent(game, player.Seat, Phase.Play);
        eventBus.Publish(phaseEndEvent2);

        // Assert
        Assert.IsTrue(player.Flags.ContainsKey("SkipDiscardPhase"), 
            "SkipDiscardPhase flag should be set in second play phase when no Slash was used (tracking was reset).");
    }

    /// <summary>
    /// Tests that BasicTurnEngine skips discard phase when SkipDiscardPhase flag is set.
    /// Input: Game with 2 players, player has SkipDiscardPhase flag set, phase advances from Play to Discard.
    /// Expected: Discard phase is automatically skipped, phase advances directly to End, flag is cleared.
    /// </summary>
    [TestMethod]
    public void BasicTurnEngineSkipsDiscardPhaseWhenFlagSet()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var gameMode = new TestGameMode();
        var eventBus = new BasicEventBus();
        var turnEngine = new BasicTurnEngine(gameMode, eventBus);

        game.CurrentPlayerSeat = game.Players[0].Seat;
        game.CurrentPhase = Phase.Play;

        // Set SkipDiscardPhase flag
        game.Players[0].Flags["SkipDiscardPhase"] = true;

        // Act - Advance phase from Play to Discard (should skip Discard and go to End)
        var result = turnEngine.AdvancePhase(game);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(Phase.End, game.CurrentPhase, "Discard phase should be skipped.");
        Assert.IsFalse(game.Players[0].Flags.ContainsKey("SkipDiscardPhase"), 
            "SkipDiscardPhase flag should be cleared after skipping.");
    }

    #endregion

    private sealed class TestGameMode : IGameMode
    {
        public string Id => "TestGameMode";
        public string DisplayName => "Test Game Mode";

        public int SelectFirstPlayerSeat(Game game)
        {
            return game.Players.FirstOrDefault(p => p.IsAlive)?.Seat ?? 0;
        }
    }
}
