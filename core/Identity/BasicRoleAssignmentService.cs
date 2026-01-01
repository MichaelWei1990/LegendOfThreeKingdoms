using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Identity;

/// <summary>
/// Basic implementation of role assignment service for identity mode.
/// </summary>
public sealed class BasicRoleAssignmentService : IRoleAssignmentService
{
    private readonly RoleDistributionTable _distributionTable;

    /// <summary>
    /// Creates a new BasicRoleAssignmentService with default distribution table.
    /// </summary>
    public BasicRoleAssignmentService()
    {
        _distributionTable = new RoleDistributionTable();
    }

    /// <summary>
    /// Creates a new BasicRoleAssignmentService with a custom distribution table.
    /// </summary>
    /// <param name="distributionTable">The role distribution table to use.</param>
    public BasicRoleAssignmentService(RoleDistributionTable distributionTable)
    {
        _distributionTable = distributionTable ?? throw new ArgumentNullException(nameof(distributionTable));
    }

    /// <inheritdoc />
    public Game? AssignRoles(Game game, IRandomSource random, int variantIndex = 0)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        if (random is null) throw new ArgumentNullException(nameof(random));

        var playerCount = game.Players.Count;
        if (playerCount < 4 || playerCount > 10)
        {
            return null; // Identity mode supports 4-10 players
        }

        // Get the appropriate distribution
        RoleDistribution? distribution;
        if (variantIndex == 0)
        {
            distribution = _distributionTable.GetDefaultDistribution(playerCount);
        }
        else
        {
            var variants = _distributionTable.GetVariants(playerCount);
            if (variantIndex < 1 || variantIndex > variants.Count)
            {
                return null; // Invalid variant index
            }
            distribution = variants[variantIndex - 1];
        }

        if (distribution is null)
        {
            return null;
        }

        if (distribution.TotalCount != playerCount)
        {
            return null; // Distribution doesn't match player count
        }

        // Build list of roles to assign
        var rolesToAssign = new List<string>();
        
        for (int i = 0; i < distribution.LordCount; i++)
        {
            rolesToAssign.Add(RoleConstants.Lord);
        }
        for (int i = 0; i < distribution.LoyalistCount; i++)
        {
            rolesToAssign.Add(RoleConstants.Loyalist);
        }
        for (int i = 0; i < distribution.RebelCount; i++)
        {
            rolesToAssign.Add(RoleConstants.Rebel);
        }
        for (int i = 0; i < distribution.RenegadeCount; i++)
        {
            rolesToAssign.Add(RoleConstants.Renegade);
        }

        // Shuffle roles using Fisher-Yates
        for (int i = rolesToAssign.Count - 1; i > 0; i--)
        {
            var j = random.NextInt(0, i + 1);
            (rolesToAssign[i], rolesToAssign[j]) = (rolesToAssign[j], rolesToAssign[i]);
        }

        // Create updated players with assigned roles
        var updatedPlayers = new List<Player>();
        for (int i = 0; i < game.Players.Count; i++)
        {
            var originalPlayer = game.Players[i];
            var role = rolesToAssign[i];
            
            var updatedPlayer = new Player
            {
                Seat = originalPlayer.Seat,
                CampId = role,
                FactionId = originalPlayer.FactionId,
                HeroId = originalPlayer.HeroId,
                Gender = originalPlayer.Gender,
                MaxHealth = originalPlayer.MaxHealth,
                CurrentHealth = originalPlayer.CurrentHealth,
                IsAlive = originalPlayer.IsAlive,
                RoleRevealed = false, // Will be set to true for Lord later
                HandZone = originalPlayer.HandZone,
                EquipmentZone = originalPlayer.EquipmentZone,
                JudgementZone = originalPlayer.JudgementZone
            };

            // Copy flags
            foreach (var flag in originalPlayer.Flags)
            {
                updatedPlayer.Flags[flag.Key] = flag.Value;
            }

            updatedPlayers.Add(updatedPlayer);
        }

        // Create new Game instance with updated players (similar to BasicCharacterSelectionService)
        return new Game
        {
            Players = updatedPlayers.ToArray(),
            CurrentPlayerSeat = game.CurrentPlayerSeat,
            CurrentPhase = game.CurrentPhase,
            TurnNumber = game.TurnNumber,
            DrawPile = game.DrawPile,
            DiscardPile = game.DiscardPile,
            IsFinished = game.IsFinished,
            WinnerDescription = game.WinnerDescription,
            State = game.State
        };
    }

    /// <inheritdoc />
    public void RevealLordRole(Game game)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));

        var lord = game.Players.FirstOrDefault(p => p.CampId == RoleConstants.Lord);
        if (lord is not null)
        {
            lord.RoleRevealed = true;
        }
    }
}

