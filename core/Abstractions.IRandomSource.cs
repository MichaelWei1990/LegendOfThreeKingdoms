namespace LegendOfThreeKingdoms.Core.Abstractions;

/// <summary>
/// Abstraction over deterministic random number generation used by the core engine.
/// All random decisions (draw, judgement, shuffles, etc.) must go through this interface.
/// </summary>
public interface IRandomSource
{
    /// <summary>
    /// Returns a random integer in the range [minInclusive, maxExclusive).
    /// </summary>
    int NextInt(int minInclusive, int maxExclusive);
}
