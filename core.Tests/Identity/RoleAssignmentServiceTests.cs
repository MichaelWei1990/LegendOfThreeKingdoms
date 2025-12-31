using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Identity;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests.Identity;

[TestClass]
public sealed class RoleAssignmentServiceTests
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
            return System.Math.Clamp(value, minInclusive, maxExclusive - 1);
        }
    }

    private static Game CreateGameWithPlayers(int playerCount)
    {
        var players = new Player[playerCount];
        for (int i = 0; i < playerCount; i++)
        {
            players[i] = new Player
            {
                Seat = i,
                MaxHealth = 4,
                CurrentHealth = 4,
                IsAlive = true,
                HandZone = new Model.Zones.Zone($"Hand_{i}", i, isPublic: false),
                EquipmentZone = new Model.Zones.Zone($"Equip_{i}", i, isPublic: true),
                JudgementZone = new Model.Zones.Zone($"Judge_{i}", i, isPublic: true)
            };
        }

        return new Game
        {
            Players = players,
            CurrentPlayerSeat = 0,
            CurrentPhase = Phase.None,
            TurnNumber = 1,
            DrawPile = new Zone("DrawPile", ownerSeat: null, isPublic: false),
            DiscardPile = new Zone("DiscardPile", ownerSeat: null, isPublic: true),
            IsFinished = false
        };
    }

    [TestMethod]
    public void AssignRoles_4Players_AssignsCorrectRoles()
    {
        // Arrange
        var service = new BasicRoleAssignmentService();
        var game = CreateGameWithPlayers(4);
        var random = new FixedRandomSource(0, 1, 2, 3);

        // Act
        var result = service.AssignRoles(game, random);

        // Assert
        Assert.IsNotNull(result);
        var lordCount = result.Players.Count(p => p.CampId == Model.RoleConstants.Lord);
        var loyalistCount = result.Players.Count(p => p.CampId == Model.RoleConstants.Loyalist);
        var rebelCount = result.Players.Count(p => p.CampId == Model.RoleConstants.Rebel);
        var renegadeCount = result.Players.Count(p => p.CampId == Model.RoleConstants.Renegade);

        Assert.AreEqual(1, lordCount);
        Assert.AreEqual(1, loyalistCount);
        Assert.AreEqual(1, rebelCount);
        Assert.AreEqual(1, renegadeCount);
    }

    [TestMethod]
    public void AssignRoles_5Players_AssignsCorrectRoles()
    {
        // Arrange
        var service = new BasicRoleAssignmentService();
        var game = CreateGameWithPlayers(5);
        var random = new FixedRandomSource(0, 1, 2, 3, 4);

        // Act
        var result = service.AssignRoles(game, random);

        // Assert
        Assert.IsNotNull(result);
        var lordCount = result.Players.Count(p => p.CampId == Model.RoleConstants.Lord);
        var loyalistCount = result.Players.Count(p => p.CampId == Model.RoleConstants.Loyalist);
        var rebelCount = result.Players.Count(p => p.CampId == Model.RoleConstants.Rebel);
        var renegadeCount = result.Players.Count(p => p.CampId == Model.RoleConstants.Renegade);

        Assert.AreEqual(1, lordCount);
        Assert.AreEqual(1, loyalistCount);
        Assert.AreEqual(2, rebelCount);
        Assert.AreEqual(1, renegadeCount);
    }

    [TestMethod]
    public void AssignRoles_10Players_AssignsCorrectRoles()
    {
        // Arrange
        var service = new BasicRoleAssignmentService();
        var game = CreateGameWithPlayers(10);
        var random = new FixedRandomSource(0, 1, 2, 3, 4, 5, 6, 7, 8, 9);

        // Act
        var result = service.AssignRoles(game, random);

        // Assert
        Assert.IsNotNull(result);
        var lordCount = result.Players.Count(p => p.CampId == Model.RoleConstants.Lord);
        var loyalistCount = result.Players.Count(p => p.CampId == Model.RoleConstants.Loyalist);
        var rebelCount = result.Players.Count(p => p.CampId == Model.RoleConstants.Rebel);
        var renegadeCount = result.Players.Count(p => p.CampId == Model.RoleConstants.Renegade);

        Assert.AreEqual(1, lordCount);
        Assert.AreEqual(3, loyalistCount);
        Assert.AreEqual(4, rebelCount);
        Assert.AreEqual(2, renegadeCount);
    }

    [TestMethod]
    public void AssignRoles_InvalidPlayerCount_ReturnsNull()
    {
        // Arrange
        var service = new BasicRoleAssignmentService();
        var game3 = CreateGameWithPlayers(3);
        var game11 = CreateGameWithPlayers(11);
        var random = new FixedRandomSource(0);

        // Act
        var result3 = service.AssignRoles(game3, random);
        var result11 = service.AssignRoles(game11, random);

        // Assert
        Assert.IsNull(result3);
        Assert.IsNull(result11);
    }

    [TestMethod]
    public void AssignRoles_AllPlayersHaveRoleRevealedFalse()
    {
        // Arrange
        var service = new BasicRoleAssignmentService();
        var game = CreateGameWithPlayers(4);
        var random = new FixedRandomSource(0, 1, 2, 3);

        // Act
        var result = service.AssignRoles(game, random);

        // Assert
        Assert.IsNotNull(result);
        foreach (var player in result.Players)
        {
            Assert.IsFalse(player.RoleRevealed, $"Player {player.Seat} should have RoleRevealed = false initially");
        }
    }

    [TestMethod]
    public void RevealLordRole_SetsLordRoleRevealedToTrue()
    {
        // Arrange
        var service = new BasicRoleAssignmentService();
        var game = CreateGameWithPlayers(4);
        var random = new FixedRandomSource(0, 1, 2, 3);
        var gameWithRoles = service.AssignRoles(game, random);
        Assert.IsNotNull(gameWithRoles);

        // Act
        service.RevealLordRole(gameWithRoles);

        // Assert
        var lord = gameWithRoles.Players.FirstOrDefault(p => p.CampId == Model.RoleConstants.Lord);
        Assert.IsNotNull(lord);
        Assert.IsTrue(lord.RoleRevealed, "Lord should have RoleRevealed = true");

        // Other players should still have RoleRevealed = false
        var nonLords = gameWithRoles.Players.Where(p => p.CampId != Model.RoleConstants.Lord);
        foreach (var player in nonLords)
        {
            Assert.IsFalse(player.RoleRevealed, $"Non-lord player {player.Seat} should have RoleRevealed = false");
        }
    }

    [TestMethod]
    public void AssignRoles_PreservesPlayerProperties()
    {
        // Arrange
        var service = new BasicRoleAssignmentService();
        var game = CreateGameWithPlayers(4);
        var originalPlayer = game.Players[0];
        var random = new FixedRandomSource(0, 1, 2, 3);

        // Act
        var result = service.AssignRoles(game, random);

        // Assert
        Assert.IsNotNull(result);
        var updatedPlayer = result.Players[0];
        Assert.AreEqual(originalPlayer.Seat, updatedPlayer.Seat);
        Assert.AreEqual(originalPlayer.MaxHealth, updatedPlayer.MaxHealth);
        Assert.AreEqual(originalPlayer.CurrentHealth, updatedPlayer.CurrentHealth);
        Assert.AreEqual(originalPlayer.IsAlive, updatedPlayer.IsAlive);
        // CampId should be set (not null)
        Assert.IsNotNull(updatedPlayer.CampId);
    }
}

