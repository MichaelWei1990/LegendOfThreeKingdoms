using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Resolver for Taoyuan Jieyi (桃园结义) immediate trick card.
/// Effect: All alive players restore 1 health point, in turn order starting from the user.
/// Note: This trick affects all players without explicit targeting, so nullification
/// cannot be used to cancel the effect on a single target (单体无懈无效).
/// </summary>
public sealed class TaoyuanJieyiResolver : UntargetedMassTrickResolverBase
{
    private const int HealAmount = 1;

    /// <inheritdoc />
    public override ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var game = context.Game;
        var sourcePlayer = context.SourcePlayer;

        // Get all alive players in turn order starting from source player
        var playersInOrder = GetPlayersInTurnOrder(game, sourcePlayer);

        if (playersInOrder.Count == 0)
        {
            // No alive players, nothing to do
            return ResolutionResult.SuccessResult;
        }

        // Heal each player
        var healedPlayers = new List<(Player Player, int PreviousHealth, int NewHealth)>();
        
        foreach (var player in playersInOrder)
        {
            if (player.CurrentHealth < player.MaxHealth)
            {
                var previousHealth = player.CurrentHealth;
                player.CurrentHealth = Math.Min(player.CurrentHealth + HealAmount, player.MaxHealth);
                healedPlayers.Add((player, previousHealth, player.CurrentHealth));
            }
        }

        // Log the effect if log sink is available
        if (context.LogSink is not null && healedPlayers.Count > 0)
        {
            var logEntry = new LogEntry
            {
                EventType = "TaoyuanJieyiEffect",
                Level = "Info",
                Message = $"Taoyuan Jieyi: {healedPlayers.Count} player(s) restored health",
                Data = new
                {
                    SourcePlayerSeat = sourcePlayer.Seat,
                    HealedCount = healedPlayers.Count,
                    HealedPlayers = healedPlayers.Select(h => new
                    {
                        PlayerSeat = h.Player.Seat,
                        PreviousHealth = h.PreviousHealth,
                        NewHealth = h.NewHealth
                    }).ToArray()
                }
            };
            context.LogSink.Log(logEntry);
        }

        // Publish event if available
        if (context.EventBus is not null && healedPlayers.Count > 0)
        {
            // TODO: Create HealthRestoredEvent if needed
            // For now, we can use existing events or just log
        }

        return ResolutionResult.SuccessResult;
    }

    /// <summary>
    /// Gets all alive players in turn order starting from the specified player.
    /// </summary>
    /// <param name="game">The game instance.</param>
    /// <param name="startPlayer">The player to start from (usually the card user).</param>
    /// <returns>List of alive players in turn order starting from startPlayer.</returns>
    private static IReadOnlyList<Player> GetPlayersInTurnOrder(Game game, Player startPlayer)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        if (startPlayer is null) throw new ArgumentNullException(nameof(startPlayer));

        var players = game.Players;
        var total = players.Count;
        
        if (total == 0)
        {
            return Array.Empty<Player>();
        }

        // Find the index of the start player
        var startIndex = -1;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].Seat == startPlayer.Seat)
            {
                startIndex = i;
                break;
            }
        }

        if (startIndex < 0)
        {
            // Start player not found, return empty list
            return Array.Empty<Player>();
        }

        // Collect alive players in turn order starting from startIndex
        var result = new List<Player>();
        for (int i = 0; i < total; i++)
        {
            var index = (startIndex + i) % total;
            var player = players[index];
            if (player.IsAlive)
            {
                result.Add(player);
            }
        }

        return result;
    }
}
