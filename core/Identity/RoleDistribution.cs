using System.Collections.Generic;
using System.Linq;

namespace LegendOfThreeKingdoms.Core.Identity;

/// <summary>
/// Represents a role distribution for a specific player count.
/// Defines how many players should have each role.
/// </summary>
public sealed class RoleDistribution
{
    /// <summary>
    /// Number of Lord roles (always 1 in standard identity mode).
    /// </summary>
    public int LordCount { get; init; }

    /// <summary>
    /// Number of Loyalist roles.
    /// </summary>
    public int LoyalistCount { get; init; }

    /// <summary>
    /// Number of Rebel roles.
    /// </summary>
    public int RebelCount { get; init; }

    /// <summary>
    /// Number of Renegade roles (usually 1, but can be 2 in variant modes).
    /// </summary>
    public int RenegadeCount { get; init; }

    /// <summary>
    /// Total number of roles (should equal player count).
    /// </summary>
    public int TotalCount => LordCount + LoyalistCount + RebelCount + RenegadeCount;
}

/// <summary>
/// Table of role distributions for different player counts.
/// Supports both default configurations and variant configurations.
/// </summary>
public sealed class RoleDistributionTable
{
    private readonly Dictionary<int, RoleDistribution> _defaultDistributions;
    private readonly Dictionary<int, List<RoleDistribution>> _variantDistributions;

    /// <summary>
    /// Creates a new RoleDistributionTable with default configurations.
    /// </summary>
    public RoleDistributionTable()
    {
        _defaultDistributions = new Dictionary<int, RoleDistribution>
        {
            // 4 players: 1 Lord, 1 Loyalist, 1 Rebel, 1 Renegade
            [4] = new RoleDistribution
            {
                LordCount = 1,
                LoyalistCount = 1,
                RebelCount = 1,
                RenegadeCount = 1
            },
            // 5 players: 1 Lord, 1 Loyalist, 2 Rebels, 1 Renegade
            [5] = new RoleDistribution
            {
                LordCount = 1,
                LoyalistCount = 1,
                RebelCount = 2,
                RenegadeCount = 1
            },
            // 6 players: 1 Lord, 1 Loyalist, 3 Rebels, 1 Renegade
            [6] = new RoleDistribution
            {
                LordCount = 1,
                LoyalistCount = 1,
                RebelCount = 3,
                RenegadeCount = 1
            },
            // 7 players: 1 Lord, 2 Loyalists, 3 Rebels, 1 Renegade
            [7] = new RoleDistribution
            {
                LordCount = 1,
                LoyalistCount = 2,
                RebelCount = 3,
                RenegadeCount = 1
            },
            // 8 players: 1 Lord, 2 Loyalists, 4 Rebels, 1 Renegade
            [8] = new RoleDistribution
            {
                LordCount = 1,
                LoyalistCount = 2,
                RebelCount = 4,
                RenegadeCount = 1
            },
            // 9 players: 1 Lord, 3 Loyalists, 4 Rebels, 1 Renegade
            [9] = new RoleDistribution
            {
                LordCount = 1,
                LoyalistCount = 3,
                RebelCount = 4,
                RenegadeCount = 1
            },
            // 10 players: 1 Lord, 3 Loyalists, 4 Rebels, 2 Renegades
            [10] = new RoleDistribution
            {
                LordCount = 1,
                LoyalistCount = 3,
                RebelCount = 4,
                RenegadeCount = 2
            }
        };

        _variantDistributions = new Dictionary<int, List<RoleDistribution>>();

        // Add variant configurations
        // 6 players variant: 1 Lord, 1 Loyalist, 2 Rebels, 2 Renegades
        AddVariant(6, new RoleDistribution
        {
            LordCount = 1,
            LoyalistCount = 1,
            RebelCount = 2,
            RenegadeCount = 2
        });

        // 8 players variant: 1 Lord, 1 Loyalist, 3 Rebels, 2 Renegades
        AddVariant(8, new RoleDistribution
        {
            LordCount = 1,
            LoyalistCount = 1,
            RebelCount = 3,
            RenegadeCount = 2
        });
    }

    /// <summary>
    /// Gets the default role distribution for the specified player count.
    /// </summary>
    /// <param name="playerCount">Number of players in the game.</param>
    /// <returns>The role distribution, or null if not supported.</returns>
    public RoleDistribution? GetDefaultDistribution(int playerCount)
    {
        return _defaultDistributions.TryGetValue(playerCount, out var distribution) ? distribution : null;
    }

    /// <summary>
    /// Gets all variant role distributions for the specified player count.
    /// </summary>
    /// <param name="playerCount">Number of players in the game.</param>
    /// <returns>List of variant distributions, or empty list if none exist.</returns>
    public IReadOnlyList<RoleDistribution> GetVariants(int playerCount)
    {
        return _variantDistributions.TryGetValue(playerCount, out var variants)
            ? variants
            : new List<RoleDistribution>();
    }

    /// <summary>
    /// Adds a variant distribution for a specific player count.
    /// </summary>
    /// <param name="playerCount">Number of players.</param>
    /// <param name="distribution">The variant distribution to add.</param>
    private void AddVariant(int playerCount, RoleDistribution distribution)
    {
        if (!_variantDistributions.TryGetValue(playerCount, out var variants))
        {
            variants = new List<RoleDistribution>();
            _variantDistributions[playerCount] = variants;
        }

        variants.Add(distribution);
    }
}

