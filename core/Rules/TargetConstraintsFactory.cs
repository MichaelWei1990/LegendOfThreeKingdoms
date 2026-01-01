using System.Collections.Concurrent;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Rules;

/// <summary>
/// Factory for creating and caching TargetConstraints instances for card subtypes.
/// This factory provides thread-safe caching and supports dynamic registration of new card type constraints.
/// </summary>
public sealed class TargetConstraintsFactory
{
    // Cache for TargetConstraints instances to avoid repeated allocations
    // Using ConcurrentDictionary for thread-safety in parallel test execution
    private static readonly ConcurrentDictionary<CardSubType, TargetConstraints> _cache = new();

    /// <summary>
    /// Gets the target constraints for a given card subtype.
    /// Returns null if the card subtype does not require targets or is not supported.
    /// Uses a cache to avoid repeated allocations of identical TargetConstraints instances.
    /// </summary>
    /// <param name="cardSubType">The card subtype to get constraints for.</param>
    /// <returns>The target constraints, or null if not supported.</returns>
    public static TargetConstraints? GetTargetConstraintsForCardSubType(CardSubType cardSubType)
    {
        // Use GetOrAdd for atomic check-and-add operation to avoid race conditions
        // This ensures only one thread will create the value, even if multiple threads
        // call this method concurrently for the same cardSubType
        return _cache.GetOrAdd(cardSubType, key =>
        {
            // Factory function: create new instance based on card subtype
            return key switch
            {
                CardSubType.Slash => new TargetConstraints(
                    MinTargets: 1,
                    MaxTargets: 1,
                    FilterType: TargetFilterType.Enemies),
                CardSubType.Peach => new TargetConstraints(
                    MinTargets: 0,
                    MaxTargets: 0,
                    FilterType: TargetFilterType.SelfOrFriends),
                CardSubType.GuoheChaiqiao => new TargetConstraints(
                    MinTargets: 1,
                    MaxTargets: 1,
                    FilterType: TargetFilterType.Any),
                CardSubType.Lebusishu => new TargetConstraints(
                    MinTargets: 1,
                    MaxTargets: 1,
                    FilterType: TargetFilterType.Any),
                // Add more mappings as needed
                _ => null
            };
        });
    }

    /// <summary>
    /// Registers a new target constraints mapping for a card subtype.
    /// This allows extension packs to register new card types dynamically.
    /// </summary>
    /// <param name="cardSubType">The card subtype to register.</param>
    /// <param name="constraints">The target constraints for this card subtype.</param>
    public static void Register(CardSubType cardSubType, TargetConstraints constraints)
    {
        _cache.AddOrUpdate(cardSubType, constraints, (key, oldValue) => constraints);
    }

    /// <summary>
    /// Clears the cache. Useful for testing or when card definitions change.
    /// </summary>
    public static void ClearCache()
    {
        _cache.Clear();
    }
}
