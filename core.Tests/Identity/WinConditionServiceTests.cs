using System.Linq;
using LegendOfThreeKingdoms.Core.Identity;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Zones;
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
    public void CheckWinConditions_LordAndLoyalistsWin_AllRebelsAndRenegadesDead()
    {
        // Arrange
        var service = new BasicWinConditionService();
        var game = CreateGameWithRoles(
            (0, RoleConstants.Lord, true),
            (1, RoleConstants.Loyalist, true),
            (2, RoleConstants.Rebel, false),
            (3, RoleConstants.Renegade, false)
        );

        // Act
        var result = service.CheckWinConditions(game);

        // Assert
        Assert.IsTrue(result.IsGameOver);
        Assert.AreEqual(WinType.LordAndLoyalists, result.WinType);
        Assert.IsNotNull(result.WinningPlayers);
        Assert.AreEqual(2, result.WinningPlayers.Count);
        Assert.IsTrue(result.WinningPlayers.Any(p => p.CampId == RoleConstants.Lord));
        Assert.IsTrue(result.WinningPlayers.Any(p => p.CampId == RoleConstants.Loyalist));
    }

    [TestMethod]
    public void CheckWinConditions_RebelsWin_LordDead()
    {
        // Arrange
        var service = new BasicWinConditionService();
        var game = CreateGameWithRoles(
            (0, RoleConstants.Lord, false),
            (1, RoleConstants.Loyalist, true),
            (2, RoleConstants.Rebel, true),
            (3, RoleConstants.Renegade, true)
        );

        // Act
        var result = service.CheckWinConditions(game);

        // Assert
        Assert.IsTrue(result.IsGameOver);
        Assert.AreEqual(WinType.Rebels, result.WinType);
        Assert.IsNotNull(result.WinningPlayers);
        Assert.IsTrue(result.WinningPlayers.All(p => p.CampId == RoleConstants.Rebel));
    }

    [TestMethod]
    public void CheckWinConditions_RenegadeWins_SoleSurvivor()
    {
        // Arrange
        var service = new BasicWinConditionService();
        var game = CreateGameWithRoles(
            (0, RoleConstants.Lord, false),
            (1, RoleConstants.Loyalist, false),
            (2, RoleConstants.Rebel, false),
            (3, RoleConstants.Renegade, true)
        );

        // Act
        var result = service.CheckWinConditions(game);

        // Assert
        Assert.IsTrue(result.IsGameOver);
        Assert.AreEqual(WinType.Renegade, result.WinType);
        Assert.IsNotNull(result.WinningPlayers);
        Assert.AreEqual(1, result.WinningPlayers.Count);
        Assert.AreEqual(RoleConstants.Renegade, result.WinningPlayers[0].CampId);
    }

    [TestMethod]
    public void CheckWinConditions_RenegadeWinsPriorityOverRebels_WhenSoleSurvivor()
    {
        // Arrange - Lord dead, only Renegade alive
        var service = new BasicWinConditionService();
        var game = CreateGameWithRoles(
            (0, RoleConstants.Lord, false),
            (1, RoleConstants.Loyalist, false),
            (2, RoleConstants.Rebel, false),
            (3, RoleConstants.Renegade, true)
        );

        // Act
        var result = service.CheckWinConditions(game);

        // Assert - Renegade should win, not Rebels
        Assert.IsTrue(result.IsGameOver);
        Assert.AreEqual(WinType.Renegade, result.WinType);
        Assert.IsNotNull(result.WinningPlayers);
        Assert.AreEqual(RoleConstants.Renegade, result.WinningPlayers[0].CampId);
    }

    [TestMethod]
    public void CheckWinConditions_DualRenegades_OneWinsWhenSoleSurvivor()
    {
        // Arrange - 10 player game with 2 renegades, only one survives
        var service = new BasicWinConditionService();
        var game = CreateGameWithRoles(
            (0, RoleConstants.Lord, false),
            (1, RoleConstants.Loyalist, false),
            (2, RoleConstants.Loyalist, false),
            (3, RoleConstants.Loyalist, false),
            (4, RoleConstants.Rebel, false),
            (5, RoleConstants.Rebel, false),
            (6, RoleConstants.Rebel, false),
            (7, RoleConstants.Rebel, false),
            (8, RoleConstants.Renegade, false),
            (9, RoleConstants.Renegade, true)
        );

        // Act
        var result = service.CheckWinConditions(game);

        // Assert
        Assert.IsTrue(result.IsGameOver);
        Assert.AreEqual(WinType.Renegade, result.WinType);
        Assert.IsNotNull(result.WinningPlayers);
        Assert.AreEqual(1, result.WinningPlayers.Count);
        Assert.AreEqual(9, result.WinningPlayers[0].Seat);
        Assert.AreEqual(RoleConstants.Renegade, result.WinningPlayers[0].CampId);
    }

    [TestMethod]
    public void CheckWinConditions_GameNotOver_SomePlayersAlive()
    {
        // Arrange
        var service = new BasicWinConditionService();
        var game = CreateGameWithRoles(
            (0, RoleConstants.Lord, true),
            (1, RoleConstants.Loyalist, true),
            (2, RoleConstants.Rebel, true),
            (3, RoleConstants.Renegade, true)
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
            (0, RoleConstants.Loyalist, true),
            (1, RoleConstants.Rebel, true)
        );

        // Act
        var result = service.CheckWinConditions(game);

        // Assert
        Assert.IsFalse(result.IsGameOver);
    }
}

