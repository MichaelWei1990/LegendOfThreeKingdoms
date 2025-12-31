using System.Linq;
using LegendOfThreeKingdoms.Core.Identity;
using LegendOfThreeKingdoms.Core.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests.Identity;

[TestClass]
public sealed class WinConditionServiceTests
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
            HandZone = new Model.Zones.Zone($"Hand_{p.Seat}", p.Seat, isPublic: false),
            EquipmentZone = new Model.Zones.Zone($"Equip_{p.Seat}", p.Seat, isPublic: true),
            JudgementZone = new Model.Zones.Zone($"Judge_{p.Seat}", p.Seat, isPublic: true)
        }).ToArray();

        return new Game
        {
            Players = playerList,
            CurrentPlayerSeat = 0,
            CurrentPhase = Phase.None,
            TurnNumber = 1,
            DrawPile = new Model.Zones.Zone("DrawPile", ownerSeat: null, isPublic: false),
            DiscardPile = new Model.Zones.Zone("DiscardPile", ownerSeat: null, isPublic: true),
            IsFinished = false
        };
    }

    [TestMethod]
    public void CheckWinConditions_LordAndLoyalistsWin_AllRebelsAndRenegadesDead()
    {
        // Arrange
        var service = new BasicWinConditionService();
        var game = CreateGameWithRoles(
            (0, Model.RoleConstants.Lord, true),
            (1, Model.RoleConstants.Loyalist, true),
            (2, Model.RoleConstants.Rebel, false),
            (3, Model.RoleConstants.Renegade, false)
        );

        // Act
        var result = service.CheckWinConditions(game);

        // Assert
        Assert.IsTrue(result.IsGameOver);
        Assert.AreEqual(WinType.LordAndLoyalists, result.WinType);
        Assert.IsNotNull(result.WinningPlayers);
        Assert.AreEqual(2, result.WinningPlayers.Count);
        Assert.IsTrue(result.WinningPlayers.Any(p => p.CampId == Model.RoleConstants.Lord));
        Assert.IsTrue(result.WinningPlayers.Any(p => p.CampId == Model.RoleConstants.Loyalist));
    }

    [TestMethod]
    public void CheckWinConditions_RebelsWin_LordDead()
    {
        // Arrange
        var service = new BasicWinConditionService();
        var game = CreateGameWithRoles(
            (0, Model.RoleConstants.Lord, false),
            (1, Model.RoleConstants.Loyalist, true),
            (2, Model.RoleConstants.Rebel, true),
            (3, Model.RoleConstants.Renegade, true)
        );

        // Act
        var result = service.CheckWinConditions(game);

        // Assert
        Assert.IsTrue(result.IsGameOver);
        Assert.AreEqual(WinType.Rebels, result.WinType);
        Assert.IsNotNull(result.WinningPlayers);
        Assert.IsTrue(result.WinningPlayers.All(p => p.CampId == Model.RoleConstants.Rebel));
    }

    [TestMethod]
    public void CheckWinConditions_RenegadeWins_SoleSurvivor()
    {
        // Arrange
        var service = new BasicWinConditionService();
        var game = CreateGameWithRoles(
            (0, Model.RoleConstants.Lord, false),
            (1, Model.RoleConstants.Loyalist, false),
            (2, Model.RoleConstants.Rebel, false),
            (3, Model.RoleConstants.Renegade, true)
        );

        // Act
        var result = service.CheckWinConditions(game);

        // Assert
        Assert.IsTrue(result.IsGameOver);
        Assert.AreEqual(WinType.Renegade, result.WinType);
        Assert.IsNotNull(result.WinningPlayers);
        Assert.AreEqual(1, result.WinningPlayers.Count);
        Assert.AreEqual(Model.RoleConstants.Renegade, result.WinningPlayers[0].CampId);
    }

    [TestMethod]
    public void CheckWinConditions_RenegadeWinsPriorityOverRebels_WhenSoleSurvivor()
    {
        // Arrange - Lord dead, only Renegade alive
        var service = new BasicWinConditionService();
        var game = CreateGameWithRoles(
            (0, Model.RoleConstants.Lord, false),
            (1, Model.RoleConstants.Loyalist, false),
            (2, Model.RoleConstants.Rebel, false),
            (3, Model.RoleConstants.Renegade, true)
        );

        // Act
        var result = service.CheckWinConditions(game);

        // Assert - Renegade should win, not Rebels
        Assert.IsTrue(result.IsGameOver);
        Assert.AreEqual(WinType.Renegade, result.WinType);
        Assert.IsNotNull(result.WinningPlayers);
        Assert.AreEqual(Model.RoleConstants.Renegade, result.WinningPlayers[0].CampId);
    }

    [TestMethod]
    public void CheckWinConditions_DualRenegades_OneWinsWhenSoleSurvivor()
    {
        // Arrange - 10 player game with 2 renegades, only one survives
        var service = new BasicWinConditionService();
        var game = CreateGameWithRoles(
            (0, Model.RoleConstants.Lord, false),
            (1, Model.RoleConstants.Loyalist, false),
            (2, Model.RoleConstants.Loyalist, false),
            (3, Model.RoleConstants.Loyalist, false),
            (4, Model.RoleConstants.Rebel, false),
            (5, Model.RoleConstants.Rebel, false),
            (6, Model.RoleConstants.Rebel, false),
            (7, Model.RoleConstants.Rebel, false),
            (8, Model.RoleConstants.Renegade, false),
            (9, Model.RoleConstants.Renegade, true)
        );

        // Act
        var result = service.CheckWinConditions(game);

        // Assert
        Assert.IsTrue(result.IsGameOver);
        Assert.AreEqual(WinType.Renegade, result.WinType);
        Assert.IsNotNull(result.WinningPlayers);
        Assert.AreEqual(1, result.WinningPlayers.Count);
        Assert.AreEqual(9, result.WinningPlayers[0].Seat);
        Assert.AreEqual(Model.RoleConstants.Renegade, result.WinningPlayers[0].CampId);
    }

    [TestMethod]
    public void CheckWinConditions_GameNotOver_SomePlayersAlive()
    {
        // Arrange
        var service = new BasicWinConditionService();
        var game = CreateGameWithRoles(
            (0, Model.RoleConstants.Lord, true),
            (1, Model.RoleConstants.Loyalist, true),
            (2, Model.RoleConstants.Rebel, true),
            (3, Model.RoleConstants.Renegade, true)
        );

        // Act
        var result = service.CheckWinConditions(game);

        // Assert
        Assert.IsFalse(result.IsGameOver);
        Assert.IsNull(result.WinType);
        Assert.IsNull(result.WinningPlayers);
    }

    [TestMethod]
    public void CheckWinConditions_NoLord_ReturnsNotOver()
    {
        // Arrange - Invalid game state (no Lord)
        var service = new BasicWinConditionService();
        var game = CreateGameWithRoles(
            (0, Model.RoleConstants.Loyalist, true),
            (1, Model.RoleConstants.Rebel, true)
        );

        // Act
        var result = service.CheckWinConditions(game);

        // Assert
        Assert.IsFalse(result.IsGameOver);
    }
}

