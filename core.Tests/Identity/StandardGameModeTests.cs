using System.Linq;
using LegendOfThreeKingdoms.Core.GameMode;
using LegendOfThreeKingdoms.Core.Identity;
using LegendOfThreeKingdoms.Core.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests.Identity;

[TestClass]
public sealed class StandardGameModeTests
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
    public void Id_ReturnsStandard()
    {
        // Arrange
        var gameMode = new StandardGameMode();

        // Act & Assert
        Assert.AreEqual("standard", gameMode.Id);
    }

    [TestMethod]
    public void DisplayName_ReturnsIdentityMode()
    {
        // Arrange
        var gameMode = new StandardGameMode();

        // Act & Assert
        Assert.AreEqual("身份模式", gameMode.DisplayName);
    }

    [TestMethod]
    public void SelectFirstPlayerSeat_ReturnsLordSeat()
    {
        // Arrange
        var gameMode = new StandardGameMode();
        var game = CreateGameWithRoles(
            (0, Model.RoleConstants.Loyalist, true),
            (1, Model.RoleConstants.Lord, true),
            (2, Model.RoleConstants.Rebel, true),
            (3, Model.RoleConstants.Renegade, true)
        );

        // Act
        var firstSeat = gameMode.SelectFirstPlayerSeat(game);

        // Assert
        Assert.AreEqual(1, firstSeat, "Should return Lord's seat");
    }

    [TestMethod]
    public void SelectFirstPlayerSeat_LordDead_ReturnsFirstAlivePlayer()
    {
        // Arrange
        var gameMode = new StandardGameMode();
        var game = CreateGameWithRoles(
            (0, Model.RoleConstants.Loyalist, true),
            (1, Model.RoleConstants.Lord, false),
            (2, Model.RoleConstants.Rebel, true),
            (3, Model.RoleConstants.Renegade, true)
        );

        // Act
        var firstSeat = gameMode.SelectFirstPlayerSeat(game);

        // Assert
        Assert.AreEqual(0, firstSeat, "Should return first alive player when Lord is dead");
    }

    [TestMethod]
    public void GetRoleAssignmentService_ReturnsService()
    {
        // Arrange
        var gameMode = new StandardGameMode();

        // Act
        var service = gameMode.GetRoleAssignmentService();

        // Assert
        Assert.IsNotNull(service);
        Assert.IsInstanceOfType(service, typeof(IRoleAssignmentService));
    }

    [TestMethod]
    public void GetWinConditionService_ReturnsService()
    {
        // Arrange
        var gameMode = new StandardGameMode();

        // Act
        var service = gameMode.GetWinConditionService();

        // Assert
        Assert.IsNotNull(service);
        Assert.IsInstanceOfType(service, typeof(IWinConditionService));
    }
}

