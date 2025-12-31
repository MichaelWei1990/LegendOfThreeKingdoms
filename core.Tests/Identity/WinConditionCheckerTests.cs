using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Identity;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests.Identity;

[TestClass]
public sealed class WinConditionCheckerTests
{
    private static Game CreateGameWithRoles(params (int Seat, string Role, bool IsAlive)[] players)
    {
        var playerList = players.Select(p => new Player
        {
            Seat = p.Seat,
            CampId = p.Role,
            MaxHealth = 4,
            CurrentHealth = p.IsAlive ? 4 : 0,
            IsAlive = p.IsAlive,
            HandZone = new Zone($"Hand_{p.Seat}", p.Seat, isPublic: false),
            EquipmentZone = new Zone($"Equip_{p.Seat}", p.Seat, isPublic: true),
            JudgementZone = new Zone($"Judge_{p.Seat}", p.Seat, isPublic: true)
        }).ToArray();

        return new Game
        {
            Players = playerList,
            CurrentPlayerSeat = 0,
            CurrentPhase = Phase.None,
            TurnNumber = 1,
            DrawPile = new Zone("DrawPile", ownerSeat: null, isPublic: false),
            DiscardPile = new Zone("DiscardPile", ownerSeat: null, isPublic: true),
            IsFinished = false
        };
    }

    [TestMethod]
    public void OnPlayerDied_GameOver_GameMarkedAsFinished()
    {
        // Arrange
        var eventBus = new BasicEventBus();
        var winConditionService = new BasicWinConditionService();
        var checker = new WinConditionChecker(winConditionService, eventBus);

        // Game state: Lord and Loyalist alive, Rebel and Renegade dead
        var game = CreateGameWithRoles(
            (0, RoleConstants.Lord, true),
            (1, RoleConstants.Loyalist, true),
            (2, RoleConstants.Rebel, false),
            (3, RoleConstants.Renegade, false)
        );

        // Act - Publish PlayerDiedEvent (simulating last enemy death)
        var playerDiedEvent = new PlayerDiedEvent(game, 3, null);
        eventBus.Publish(playerDiedEvent);

        // Assert
        Assert.IsTrue(game.IsFinished);
        Assert.IsNotNull(game.WinnerDescription);
        Assert.IsTrue(game.WinnerDescription.Contains("Lord and Loyalists"));
    }

    [TestMethod]
    public void OnPlayerDied_GameOver_PublishesGameEndedEvent()
    {
        // Arrange
        var eventBus = new BasicEventBus();
        var winConditionService = new BasicWinConditionService();
        var checker = new WinConditionChecker(winConditionService, eventBus);

        var gameEndedEvents = new List<GameEndedEvent>();
        eventBus.Subscribe<GameEndedEvent>(evt => gameEndedEvents.Add(evt));

        // Game state: Only Renegade alive (sole survivor)
        var game = CreateGameWithRoles(
            (0, RoleConstants.Lord, false),
            (1, RoleConstants.Loyalist, false),
            (2, RoleConstants.Rebel, false),
            (3, RoleConstants.Renegade, true)
        );

        // Act
        var playerDiedEvent = new PlayerDiedEvent(game, 2, null);
        eventBus.Publish(playerDiedEvent);

        // Assert
        Assert.AreEqual(1, gameEndedEvents.Count);
        var gameEndedEvent = gameEndedEvents[0];
        Assert.AreEqual(game, gameEndedEvent.Game);
        Assert.AreEqual(WinType.Renegade, gameEndedEvent.WinType);
        Assert.AreEqual(1, gameEndedEvent.WinningPlayerSeats.Count);
        Assert.AreEqual(3, gameEndedEvent.WinningPlayerSeats[0]);
    }

    [TestMethod]
    public void OnPlayerDied_GameNotOver_GameNotMarkedAsFinished()
    {
        // Arrange
        var eventBus = new BasicEventBus();
        var winConditionService = new BasicWinConditionService();
        var checker = new WinConditionChecker(winConditionService, eventBus);

        // Game state: Multiple players still alive
        var game = CreateGameWithRoles(
            (0, RoleConstants.Lord, true),
            (1, RoleConstants.Loyalist, true),
            (2, RoleConstants.Rebel, true),
            (3, RoleConstants.Renegade, true)
        );

        // Act
        var playerDiedEvent = new PlayerDiedEvent(game, 2, null);
        eventBus.Publish(playerDiedEvent);

        // Assert
        Assert.IsFalse(game.IsFinished);
    }

    [TestMethod]
    public void OnPlayerDied_GameAlreadyFinished_DoesNotCheckAgain()
    {
        // Arrange
        var eventBus = new BasicEventBus();
        var winConditionService = new BasicWinConditionService();
        var checker = new WinConditionChecker(winConditionService, eventBus);

        var gameEndedEvents = new List<GameEndedEvent>();
        eventBus.Subscribe<GameEndedEvent>(evt => gameEndedEvents.Add(evt));

        // Game state: Game already finished
        var game = CreateGameWithRoles(
            (0, RoleConstants.Lord, true),
            (1, RoleConstants.Loyalist, true),
            (2, RoleConstants.Rebel, false),
            (3, RoleConstants.Renegade, false)
        );
        game.IsFinished = true;

        // Act
        var playerDiedEvent = new PlayerDiedEvent(game, 1, null);
        eventBus.Publish(playerDiedEvent);

        // Assert - Should not publish another GameEndedEvent
        Assert.AreEqual(0, gameEndedEvents.Count);
    }
}

