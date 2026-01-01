using System;
using System.Collections.Concurrent;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Rules;

/// <summary>
/// Default seat / attack range rules.
/// </summary>
public sealed class RangeRuleService : IRangeRuleService
{
    private readonly IRuleModifierProvider? _modifierProvider;
    
    // Cache for seat distance calculations
    // Key: (fromSeat, toSeat, playerCount) as string
    // Value: calculated seat distance
    // Note: Seat distance only depends on seat positions and player count, which rarely change during a game
    private static readonly ConcurrentDictionary<string, int> _seatDistanceCache = new();

    /// <summary>
    /// Creates a new RangeRuleService.
    /// </summary>
    /// <param name="modifierProvider">Optional rule modifier provider for applying equipment and skill modifications.</param>
    public RangeRuleService(IRuleModifierProvider? modifierProvider = null)
    {
        _modifierProvider = modifierProvider;
    }

    /// <inheritdoc />
    public int GetSeatDistance(Game game, Player from, Player to)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        if (from is null) throw new ArgumentNullException(nameof(from));
        if (to is null) throw new ArgumentNullException(nameof(to));

        if (ReferenceEquals(from, to) || from.Seat == to.Seat)
        {
            throw new InvalidOperationException(
                "GetSeatDistance was called with the same player as both source and target. " +
                "Seat distance is only defined between distinct players.");
        }

        var players = game.Players;
        var count = players.Count;
        if (count == 0)
        {
            throw new InvalidOperationException(
                "GetSeatDistance was called on a game with zero players. " +
                "This indicates an invalid Game state; at least one player is required.");
        }

        var fromIndex = IndexOfSeat(players, from.Seat);
        var toIndex = IndexOfSeat(players, to.Seat);
        if (fromIndex < 0 || toIndex < 0)
        {
            throw new InvalidOperationException(
                $"Player seat not found in game.Players (fromSeat={from.Seat}, toSeat={to.Seat}). " +
                "This indicates a model consistency bug and should never happen in normal gameplay.");
        }

        var clockwise = (toIndex - fromIndex + count) % count;
        var counterClockwise = (fromIndex - toIndex + count) % count;
        var distance = Math.Min(clockwise, counterClockwise);

        // Distance should be at least 1 when seats differ.
        return distance == 0 ? RulesConstants.MinimumSeatDistance : distance;
    }

    /// <inheritdoc />
    public int GetAttackDistance(Game game, Player from, Player to)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        if (from is null) throw new ArgumentNullException(nameof(from));
        if (to is null) throw new ArgumentNullException(nameof(to));

        // Base attack distance is always 1.
        int baseDistance = 1;

        // Apply rule modifiers from the attacker's perspective
        if (_modifierProvider is not null)
        {
            var modifiers = _modifierProvider.GetModifiersFor(game, from);
            foreach (var modifier in modifiers)
            {
                var modified = modifier.ModifyAttackDistance(baseDistance, game, from, to);
                if (modified.HasValue)
                {
                    baseDistance = modified.Value;
                }
            }
        }

        return baseDistance;
    }

    /// <inheritdoc />
    public bool IsWithinAttackRange(Game game, Player from, Player to)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        if (from is null) throw new ArgumentNullException(nameof(from));
        if (to is null) throw new ArgumentNullException(nameof(to));

        if (!to.IsAlive)
        {
            return false;
        }

        var seatDistance = GetSeatDistance(game, from, to);
        var attackDistance = GetAttackDistance(game, from, to);

        // Apply defensive rule modifiers from the defender's perspective
        // This allows defensive equipment (like defensive horse) to modify the seat distance requirement
        if (_modifierProvider is not null)
        {
            var modifiers = _modifierProvider.GetModifiersFor(game, to);
            foreach (var modifier in modifiers)
            {
                var modified = modifier.ModifySeatDistance(seatDistance, game, from, to);
                if (modified.HasValue)
                {
                    seatDistance = modified.Value;
                }
            }
        }

        // Apply offensive rule modifiers from the attacker's perspective
        // This allows offensive equipment (like offensive horse) to modify the seat distance requirement
        if (_modifierProvider is not null)
        {
            var modifiers = _modifierProvider.GetModifiersFor(game, from);
            foreach (var modifier in modifiers)
            {
                var modified = modifier.ModifySeatDistance(seatDistance, game, from, to);
                if (modified.HasValue)
                {
                    seatDistance = modified.Value;
                }
            }
        }

        return seatDistance <= attackDistance;
    }

    private static int IndexOfSeat(IReadOnlyList<Player> players, int seat)
    {
        for (var i = 0; i < players.Count; i++)
        {
            if (players[i].Seat == seat)
            {
                return i;
            }
        }

        return -1;
    }
}
