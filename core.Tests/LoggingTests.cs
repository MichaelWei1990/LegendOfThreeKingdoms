using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Logging;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests;

[TestClass]
public sealed class LoggingTests
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
            DefinitionId = "slash_basic",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = Suit.Spade,
            Rank = 7
        };
    }

    #region BasicLogCollector Tests

    [TestMethod]
    public void BasicLogCollector_Collect_EventStored()
    {
        // Arrange
        var collector = new BasicLogCollector();
        var game = CreateDefaultGame();
        var logEvent = new TurnStartLogEvent(
            DateTime.UtcNow,
            1,
            game,
            0,
            1
        );

        // Act
        collector.Collect(logEvent);

        // Assert
        var events = collector.GetEvents();
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(logEvent, events[0]);
    }

    [TestMethod]
    public void BasicLogCollector_GetNextSequenceNumber_Increments()
    {
        // Arrange
        var collector = new BasicLogCollector();

        // Act & Assert
        Assert.AreEqual(1, collector.GetNextSequenceNumber());
        Assert.AreEqual(2, collector.GetNextSequenceNumber());
        Assert.AreEqual(3, collector.GetNextSequenceNumber());
    }

    [TestMethod]
    public void BasicLogCollector_Clear_RemovesAllEvents()
    {
        // Arrange
        var collector = new BasicLogCollector();
        var game = CreateDefaultGame();
        collector.Collect(new TurnStartLogEvent(DateTime.UtcNow, 1, game, 0, 1));
        collector.Collect(new TurnEndLogEvent(DateTime.UtcNow, 2, game, 0, 1));

        // Act
        collector.Clear();

        // Assert
        Assert.AreEqual(0, collector.GetEvents().Count);
        Assert.AreEqual(1, collector.GetNextSequenceNumber()); // Sequence counter should reset
    }

    [TestMethod]
    public void BasicLogCollector_GetEvents_ReturnsReadOnlyList()
    {
        // Arrange
        var collector = new BasicLogCollector();
        var game = CreateDefaultGame();
        collector.Collect(new TurnStartLogEvent(DateTime.UtcNow, 1, game, 0, 1));

        // Act
        var events = collector.GetEvents();

        // Assert
        Assert.IsInstanceOfType(events, typeof(IReadOnlyList<ILogEvent>));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void BasicLogCollector_Collect_NullEvent_ThrowsException()
    {
        // Arrange
        var collector = new BasicLogCollector();

        // Act
        collector.Collect(null!);
    }

    #endregion

    #region LogEventMapper Tests

    [TestMethod]
    public void LogEventMapper_TurnStartEvent_MapsCorrectly()
    {
        // Arrange
        var game = CreateDefaultGame();
        var gameEvent = new TurnStartEvent(game, 0, 1);
        var sequenceNumber = 1L;

        // Act
        var logEvent = LogEventMapper.MapFromGameEvent(gameEvent, sequenceNumber);

        // Assert
        Assert.IsNotNull(logEvent);
        Assert.IsInstanceOfType(logEvent, typeof(TurnStartLogEvent));
        var turnStartLogEvent = (TurnStartLogEvent)logEvent;
        Assert.AreEqual(game, turnStartLogEvent.Game);
        Assert.AreEqual(0, turnStartLogEvent.PlayerSeat);
        Assert.AreEqual(1, turnStartLogEvent.TurnNumber);
        Assert.AreEqual(sequenceNumber, turnStartLogEvent.SequenceNumber);
    }

    [TestMethod]
    public void LogEventMapper_DamageAppliedEvent_MapsCorrectly()
    {
        // Arrange
        var game = CreateDefaultGame();
        var damage = new DamageDescriptor(0, 1, 1, DamageType.Normal, "Test");
        var gameEvent = new DamageAppliedEvent(game, damage, 3, 2);
        var sequenceNumber = 1L;

        // Act
        var logEvent = LogEventMapper.MapFromGameEvent(gameEvent, sequenceNumber);

        // Assert
        Assert.IsNotNull(logEvent);
        Assert.IsInstanceOfType(logEvent, typeof(DamageAppliedLogEvent));
        var damageLogEvent = (DamageAppliedLogEvent)logEvent;
        Assert.AreEqual(0, damageLogEvent.SourceSeat);
        Assert.AreEqual(1, damageLogEvent.TargetSeat);
        Assert.AreEqual(1, damageLogEvent.Amount);
        Assert.AreEqual(DamageType.Normal, damageLogEvent.Type);
        Assert.AreEqual(3, damageLogEvent.PreviousHealth);
        Assert.AreEqual(2, damageLogEvent.CurrentHealth);
    }

    [TestMethod]
    public void LogEventMapper_CardMovedEvent_MapsCorrectly()
    {
        // Arrange
        var game = CreateDefaultGame();
        var cardMoveEvent = new CardMoveEvent(
            "Hand_0",
            0,
            "DiscardPile",
            null,
            new[] { 1 },
            CardMoveReason.Play,
            CardMoveOrdering.ToTop,
            CardMoveEventTiming.After
        );
        var gameEvent = new CardMovedEvent(game, cardMoveEvent);
        var sequenceNumber = 1L;

        // Act
        var logEvent = LogEventMapper.MapFromGameEvent(gameEvent, sequenceNumber);

        // Assert
        Assert.IsNotNull(logEvent);
        Assert.IsInstanceOfType(logEvent, typeof(CardMovedLogEvent));
        var cardMovedLogEvent = (CardMovedLogEvent)logEvent;
        Assert.AreEqual(1, cardMovedLogEvent.CardId);
        Assert.AreEqual("Hand_0", cardMovedLogEvent.FromZone);
        Assert.AreEqual("DiscardPile", cardMovedLogEvent.ToZone);
        Assert.AreEqual(0, cardMovedLogEvent.FromOwnerSeat);
        Assert.IsNull(cardMovedLogEvent.ToOwnerSeat);
    }

    [TestMethod]
    public void LogEventMapper_CardMovedEvent_BeforeTiming_ReturnsNull()
    {
        // Arrange
        var game = CreateDefaultGame();
        var cardMoveEvent = new CardMoveEvent(
            "Hand_0",
            0,
            "DiscardPile",
            null,
            new[] { 1 },
            CardMoveReason.Play,
            CardMoveOrdering.ToTop,
            CardMoveEventTiming.Before // Before timing should not be logged
        );
        var gameEvent = new CardMovedEvent(game, cardMoveEvent);
        var sequenceNumber = 1L;

        // Act
        var logEvent = LogEventMapper.MapFromGameEvent(gameEvent, sequenceNumber);

        // Assert
        Assert.IsNull(logEvent);
    }

    [TestMethod]
    public void LogEventMapper_NullEvent_ReturnsNull()
    {
        // Act
        var logEvent = LogEventMapper.MapFromGameEvent(null!, 1L);

        // Assert
        Assert.IsNull(logEvent);
    }

    #endregion

    #region Event Bus Integration Tests

    [TestMethod]
    public void SubscribeToLogCollector_SubscribesToEvents()
    {
        // Arrange
        var eventBus = new BasicEventBus();
        var collector = new BasicLogCollector();
        var game = CreateDefaultGame();

        // Act
        eventBus.SubscribeToLogCollector(collector);
        eventBus.Publish(new TurnStartEvent(game, 0, 1));

        // Assert
        var events = collector.GetEvents();
        Assert.AreEqual(1, events.Count);
        Assert.IsInstanceOfType(events[0], typeof(TurnStartLogEvent));
    }

    [TestMethod]
    public void SubscribeToLogCollector_MultipleEvents_AllCollected()
    {
        // Arrange
        var eventBus = new BasicEventBus();
        var collector = new BasicLogCollector();
        var game = CreateDefaultGame();

        // Act
        eventBus.SubscribeToLogCollector(collector);
        eventBus.Publish(new TurnStartEvent(game, 0, 1));
        eventBus.Publish(new PhaseStartEvent(game, 0, Phase.Draw));
        eventBus.Publish(new TurnEndEvent(game, 0, 1));

        // Assert
        var events = collector.GetEvents();
        Assert.AreEqual(3, events.Count);
        Assert.IsInstanceOfType(events[0], typeof(TurnStartLogEvent));
        Assert.IsInstanceOfType(events[1], typeof(PhaseStartLogEvent));
        Assert.IsInstanceOfType(events[2], typeof(TurnEndLogEvent));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void SubscribeToLogCollector_NullEventBus_ThrowsException()
    {
        // Arrange
        var collector = new BasicLogCollector();

        // Act
        ((IEventBus)null!).SubscribeToLogCollector(collector);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void SubscribeToLogCollector_NullCollector_ThrowsException()
    {
        // Arrange
        var eventBus = new BasicEventBus();

        // Act
        eventBus.SubscribeToLogCollector(null!);
    }

    #endregion

    #region Resolver Logging Tests

    [TestMethod]
    public void UseCardResolver_WithLogCollector_RecordsCardUsedEvent()
    {
        // Arrange
        var game = CreateDefaultGame();
        game.CurrentPhase = Phase.Play;
        var sourcePlayer = game.Players[0];
        var targetPlayer = game.Players[1];
        var card = CreateSlashCard(1);
        ((Zone)sourcePlayer.HandZone).MutableCards.Add(card);

        var collector = new BasicLogCollector();
        var stack = new BasicResolutionStack();
        var ruleService = new RuleService();
        var cardMoveService = new BasicCardMoveService();
        var eventBus = new BasicEventBus();

        var action = new ActionDescriptor(
            ActionId: "UseSlash",
            DisplayKey: "action.useSlash",
            RequiresTargets: true,
            TargetConstraints: new TargetConstraints(1, 1, TargetFilterType.Enemies),
            CardCandidates: new[] { card }
        );
        var choice = new ChoiceResult(
            "test_request",
            sourcePlayer.Seat,
            new[] { targetPlayer.Seat }, // target seat
            new[] { card.Id },
            null,
            true
        );

        var context = new ResolutionContext(
            game,
            sourcePlayer,
            action,
            choice,
            stack,
            cardMoveService,
            ruleService,
            LogCollector: collector,
            EventBus: eventBus
        );

        var resolver = new UseCardResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);
        var events = collector.GetEvents();
        Assert.AreEqual(1, events.Count);
        Assert.IsInstanceOfType(events[0], typeof(CardUsedLogEvent));
        var cardUsedEvent = (CardUsedLogEvent)events[0];
        Assert.AreEqual(sourcePlayer.Seat, cardUsedEvent.SourcePlayerSeat);
        Assert.AreEqual(card.Id, cardUsedEvent.CardId);
        Assert.AreEqual(CardSubType.Slash, cardUsedEvent.CardSubType);
    }

    [TestMethod]
    public void UseCardResolver_WithoutLogCollector_NoException()
    {
        // Arrange
        var game = CreateDefaultGame();
        game.CurrentPhase = Phase.Play;
        var sourcePlayer = game.Players[0];
        var targetPlayer = game.Players[1];
        var card = CreateSlashCard(1);
        ((Zone)sourcePlayer.HandZone).MutableCards.Add(card);

        var stack = new BasicResolutionStack();
        var ruleService = new RuleService();
        var cardMoveService = new BasicCardMoveService();

        var action = new ActionDescriptor(
            ActionId: "UseSlash",
            DisplayKey: "action.useSlash",
            RequiresTargets: true,
            TargetConstraints: new TargetConstraints(1, 1, TargetFilterType.Enemies),
            CardCandidates: new[] { card }
        );
        var choice = new ChoiceResult(
            "test_request",
            sourcePlayer.Seat,
            new[] { targetPlayer.Seat },
            new[] { card.Id },
            null,
            true
        );

        var context = new ResolutionContext(
            game,
            sourcePlayer,
            action,
            choice,
            stack,
            cardMoveService,
            ruleService,
            LogCollector: null // No log collector
        );

        var resolver = new UseCardResolver();

        // Act & Assert - should not throw
        var result = resolver.Resolve(context);
        Assert.IsTrue(result.Success);
    }

    #endregion

    #region Serialization Tests

    [TestMethod]
    public void LogEventSerialization_SerializeToJson_ProducesValidJson()
    {
        // Arrange
        var game = CreateDefaultGame();
        var logEvent = new TurnStartLogEvent(
            DateTime.UtcNow,
            1,
            game,
            0,
            1
        );

        // Act
        var json = LogEventSerialization.SerializeToJson(logEvent);

        // Assert
        Assert.IsNotNull(json);
        Assert.IsTrue(json.Contains("turnStart", StringComparison.OrdinalIgnoreCase) || 
                     json.Contains("eventType", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void LogEventSerialization_SerializeArray_ProducesValidJson()
    {
        // Arrange
        var game = CreateDefaultGame();
        var events = new ILogEvent[]
        {
            new TurnStartLogEvent(DateTime.UtcNow, 1, game, 0, 1),
            new TurnEndLogEvent(DateTime.UtcNow, 2, game, 0, 1)
        };

        // Act
        IEnumerable<ILogEvent> eventsEnumerable = events;
        var json = LogEventSerialization.SerializeToJson(eventsEnumerable);

        // Assert
        Assert.IsNotNull(json);
        Assert.IsTrue(json.StartsWith("[") && json.EndsWith("]"));
    }

    [TestMethod]
    public void LogEventSerialization_DeserializeFromJson_NullOrEmpty_ReturnsNull()
    {
        // Act & Assert
        Assert.IsNull(LogEventSerialization.DeserializeFromJson(null!));
        Assert.IsNull(LogEventSerialization.DeserializeFromJson(""));
        Assert.IsNull(LogEventSerialization.DeserializeFromJson("   "));
    }

    [TestMethod]
    public void LogEventSerialization_DeserializeArrayFromJson_NullOrEmpty_ReturnsEmpty()
    {
        // Act & Assert
        var result1 = LogEventSerialization.DeserializeArrayFromJson(null!);
        var result2 = LogEventSerialization.DeserializeArrayFromJson("");
        var result3 = LogEventSerialization.DeserializeArrayFromJson("   ");

        Assert.AreEqual(0, result1.Count);
        Assert.AreEqual(0, result2.Count);
        Assert.AreEqual(0, result3.Count);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void LogEventSerialization_SerializeToJson_NullEvent_ThrowsException()
    {
        // Act
        ILogEvent? nullEvent = null;
        LogEventSerialization.SerializeToJson(nullEvent!);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void LogEventSerialization_SerializeToJson_NullCollection_ThrowsException()
    {
        // Act
        IEnumerable<ILogEvent>? nullCollection = null;
        LogEventSerialization.SerializeToJson(nullCollection!);
    }

    #endregion
}

