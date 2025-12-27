using System;
using System.Collections.Generic;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests;

[TestClass]
public sealed class EventsBasicTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    [TestMethod]
    public void BasicEventBus_SubscribeAndPublish_HandlerInvoked()
    {
        // Arrange
        var eventBus = new BasicEventBus();
        var game = CreateDefaultGame();
        bool handlerInvoked = false;

        eventBus.Subscribe<TurnStartEvent>(evt =>
        {
            Assert.AreEqual(game, evt.Game);
            Assert.AreEqual(0, evt.PlayerSeat);
            Assert.AreEqual(1, evt.TurnNumber);
            handlerInvoked = true;
        });

        // Act
        var turnStartEvent = new TurnStartEvent(game, 0, 1);
        eventBus.Publish(turnStartEvent);

        // Assert
        Assert.IsTrue(handlerInvoked);
    }

    [TestMethod]
    public void BasicEventBus_MultipleSubscribers_AllHandlersInvoked()
    {
        // Arrange
        var eventBus = new BasicEventBus();
        var game = CreateDefaultGame();
        int invocationCount = 0;

        eventBus.Subscribe<PhaseStartEvent>(evt => invocationCount++);
        eventBus.Subscribe<PhaseStartEvent>(evt => invocationCount++);
        eventBus.Subscribe<PhaseStartEvent>(evt => invocationCount++);

        // Act
        var phaseStartEvent = new PhaseStartEvent(game, 0, Phase.Draw);
        eventBus.Publish(phaseStartEvent);

        // Assert
        Assert.AreEqual(3, invocationCount);
    }

    [TestMethod]
    public void BasicEventBus_NoSubscribers_NoException()
    {
        // Arrange
        var eventBus = new BasicEventBus();
        var game = CreateDefaultGame();

        // Act & Assert - should not throw
        var turnStartEvent = new TurnStartEvent(game, 0, 1);
        eventBus.Publish(turnStartEvent);
    }

    [TestMethod]
    public void BasicEventBus_Unsubscribe_HandlerNotInvoked()
    {
        // Arrange
        var eventBus = new BasicEventBus();
        var game = CreateDefaultGame();
        bool handlerInvoked = false;

        Action<DamageCreatedEvent> handler = evt => handlerInvoked = true;
        eventBus.Subscribe(handler);

        // Act
        eventBus.Unsubscribe(handler);

        var damageEvent = new DamageCreatedEvent(
            game,
            new DamageDescriptor(0, 1, 1, DamageType.Normal));
        eventBus.Publish(damageEvent);

        // Assert
        Assert.IsFalse(handlerInvoked);
    }

    [TestMethod]
    public void BasicEventBus_HandlerThrowsException_OtherHandlersStillInvoked()
    {
        // Arrange
        var eventBus = new BasicEventBus();
        var game = CreateDefaultGame();
        int invocationCount = 0;

        eventBus.Subscribe<DyingStartEvent>(evt => throw new InvalidOperationException("Test exception"));
        eventBus.Subscribe<DyingStartEvent>(evt => invocationCount++);
        eventBus.Subscribe<DyingStartEvent>(evt => invocationCount++);

        // Act
        var dyingStartEvent = new DyingStartEvent(game, 0);
        eventBus.Publish(dyingStartEvent);

        // Assert
        Assert.AreEqual(2, invocationCount);
    }

    [TestMethod]
    public void TurnStartEvent_HasCorrectProperties()
    {
        // Arrange
        var game = CreateDefaultGame();

        // Act
        var turnStartEvent = new TurnStartEvent(game, 1, 5);

        // Assert
        Assert.AreEqual(game, turnStartEvent.Game);
        Assert.AreEqual(1, turnStartEvent.PlayerSeat);
        Assert.AreEqual(5, turnStartEvent.TurnNumber);
        Assert.IsTrue(turnStartEvent.Timestamp > DateTime.MinValue);
    }

    [TestMethod]
    public void TurnStartEvent_TimestampDefaultsToUtcNow()
    {
        // Arrange
        var game = CreateDefaultGame();
        var beforeCreation = DateTime.UtcNow;

        // Act
        var turnStartEvent = new TurnStartEvent(game, 0, 1);
        var afterCreation = DateTime.UtcNow;

        // Assert
        Assert.IsTrue(turnStartEvent.Timestamp >= beforeCreation);
        Assert.IsTrue(turnStartEvent.Timestamp <= afterCreation);
    }

    [TestMethod]
    public void DamageCreatedEvent_HasCorrectProperties()
    {
        // Arrange
        var game = CreateDefaultGame();
        var damage = new DamageDescriptor(0, 1, 2, DamageType.Fire, "Test");

        // Act
        var damageCreatedEvent = new DamageCreatedEvent(game, damage);

        // Assert
        Assert.AreEqual(game, damageCreatedEvent.Game);
        Assert.AreEqual(damage, damageCreatedEvent.Damage);
        Assert.IsTrue(damageCreatedEvent.Timestamp > DateTime.MinValue);
    }

    [TestMethod]
    public void DamageAppliedEvent_HasCorrectProperties()
    {
        // Arrange
        var game = CreateDefaultGame();
        var damage = new DamageDescriptor(0, 1, 2, DamageType.Normal);

        // Act
        var damageAppliedEvent = new DamageAppliedEvent(game, damage, 3, 1);

        // Assert
        Assert.AreEqual(game, damageAppliedEvent.Game);
        Assert.AreEqual(damage, damageAppliedEvent.Damage);
        Assert.AreEqual(3, damageAppliedEvent.PreviousHealth);
        Assert.AreEqual(1, damageAppliedEvent.CurrentHealth);
    }

    [TestMethod]
    public void DyingStartEvent_HasCorrectProperties()
    {
        // Arrange
        var game = CreateDefaultGame();

        // Act
        var dyingStartEvent = new DyingStartEvent(game, 2);

        // Assert
        Assert.AreEqual(game, dyingStartEvent.Game);
        Assert.AreEqual(2, dyingStartEvent.DyingPlayerSeat);
    }

    [TestMethod]
    public void PlayerDiedEvent_HasCorrectProperties()
    {
        // Arrange
        var game = CreateDefaultGame();

        // Act
        var playerDiedEvent = new PlayerDiedEvent(game, 1, 0);

        // Assert
        Assert.AreEqual(game, playerDiedEvent.Game);
        Assert.AreEqual(1, playerDiedEvent.DeadPlayerSeat);
        Assert.AreEqual(0, playerDiedEvent.KillerSeat);
    }

    [TestMethod]
    public void PlayerDiedEvent_KillerSeatCanBeNull()
    {
        // Arrange
        var game = CreateDefaultGame();

        // Act
        var playerDiedEvent = new PlayerDiedEvent(game, 1, null);

        // Assert
        Assert.IsNull(playerDiedEvent.KillerSeat);
    }

    [TestMethod]
    public void PhaseStartEvent_HasCorrectProperties()
    {
        // Arrange
        var game = CreateDefaultGame();

        // Act
        var phaseStartEvent = new PhaseStartEvent(game, 0, Phase.Play);

        // Assert
        Assert.AreEqual(game, phaseStartEvent.Game);
        Assert.AreEqual(0, phaseStartEvent.PlayerSeat);
        Assert.AreEqual(Phase.Play, phaseStartEvent.Phase);
    }

    [TestMethod]
    public void PhaseEndEvent_HasCorrectProperties()
    {
        // Arrange
        var game = CreateDefaultGame();

        // Act
        var phaseEndEvent = new PhaseEndEvent(game, 0, Phase.Draw);

        // Assert
        Assert.AreEqual(game, phaseEndEvent.Game);
        Assert.AreEqual(0, phaseEndEvent.PlayerSeat);
        Assert.AreEqual(Phase.Draw, phaseEndEvent.Phase);
    }

    [TestMethod]
    public void CardMovedEvent_HasCorrectProperties()
    {
        // Arrange
        var game = CreateDefaultGame();
        var cardMoveEvent = new CardMoveEvent(
            "Hand_0",
            0,
            "DiscardPile",
            null,
            new[] { 1, 2 },
            CardMoveReason.Discard,
            CardMoveOrdering.ToTop,
            CardMoveEventTiming.After);

        // Act
        var cardMovedEvent = new CardMovedEvent(game, cardMoveEvent);

        // Assert
        Assert.AreEqual(game, cardMovedEvent.Game);
        Assert.AreEqual(cardMoveEvent, cardMovedEvent.CardMoveEvent);
    }
}



