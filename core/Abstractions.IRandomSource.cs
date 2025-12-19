using System;

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

/// <summary>
/// Deterministic random source that uses a seed for reproducible random number generation.
/// </summary>
public sealed class SeededRandomSource : IRandomSource
{
    private readonly Random _random;

    /// <summary>
    /// Creates a new SeededRandomSource with the given seed.
    /// </summary>
    public SeededRandomSource(int seed)
    {
        _random = new Random(seed);
    }

    /// <inheritdoc />
    public int NextInt(int minInclusive, int maxExclusive)
    {
        return _random.Next(minInclusive, maxExclusive);
    }
}
