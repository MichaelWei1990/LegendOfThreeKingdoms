using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Identity;

/// <summary>
/// Basic implementation of win condition service for identity mode.
/// </summary>
public sealed class BasicWinConditionService : IWinConditionService
{
    /// <inheritdoc />
    public WinConditionResult CheckWinConditions(Game game)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));

        var alivePlayers = game.Players.Where(p => p.IsAlive).ToList();
        var allPlayers = game.Players.ToList();

        // Check if Lord is alive
        var lord = allPlayers.FirstOrDefault(p => p.CampId == RoleConstants.Lord);
        if (lord is null)
        {
            // No Lord found - invalid game state
            return WinConditionResult.NotOver();
        }

        var lordAlive = lord.IsAlive;

        // Count roles
        var aliveLoyalists = alivePlayers.Count(p => p.CampId == RoleConstants.Loyalist);
        var aliveRebels = alivePlayers.Count(p => p.CampId == RoleConstants.Rebel);
        var aliveRenegades = alivePlayers.Count(p => p.CampId == RoleConstants.Renegade);
        var totalRenegades = allPlayers.Count(p => p.CampId == RoleConstants.Renegade);

        // Check Renegade win condition first (highest priority)
        // Renegade wins if they are the sole survivor
        if (aliveRenegades > 0)
        {
            // Check if any renegade is the sole survivor
            foreach (var renegade in alivePlayers.Where(p => p.CampId == RoleConstants.Renegade))
            {
                // For single renegade: all others must be dead
                // For multiple renegades: all others (including other renegades) must be dead
                var othersAlive = alivePlayers.Count(p => p.Seat != renegade.Seat);
                if (othersAlive == 0)
                {
                    return WinConditionResult.GameOver(
                        WinType.Renegade,
                        new List<Player> { renegade },
                        totalRenegades > 1
                            ? "Renegade is the sole survivor (multiple renegades mode)"
                            : "Renegade is the sole survivor");
                }
            }
        }

        // If Lord is dead, check if Renegade win takes priority over Rebel win
        if (!lordAlive)
        {
            // Check if Renegade is the sole survivor (this takes priority over Rebel win)
            if (aliveRenegades == 1 && alivePlayers.Count == 1)
            {
                var renegade = alivePlayers.First(p => p.CampId == RoleConstants.Renegade);
                return WinConditionResult.GameOver(
                    WinType.Renegade,
                    new List<Player> { renegade },
                    "Renegade is the sole survivor after Lord's death");
            }

            // Otherwise, Rebels win
            var rebelPlayers = allPlayers.Where(p => p.CampId == RoleConstants.Rebel).ToList();
            return WinConditionResult.GameOver(
                WinType.Rebels,
                rebelPlayers,
                "Lord is dead");
        }

        // Lord is alive - check if Lord and Loyalists win
        // Win condition: all Rebels and Renegades are dead
        if (aliveRebels == 0 && aliveRenegades == 0)
        {
            var lordAndLoyalists = allPlayers
                .Where(p => p.CampId == RoleConstants.Lord || p.CampId == RoleConstants.Loyalist)
                .ToList();
            return WinConditionResult.GameOver(
                WinType.LordAndLoyalists,
                lordAndLoyalists,
                "All Rebels and Renegades are eliminated");
        }

        // No win condition met
        return WinConditionResult.NotOver();
    }
}

