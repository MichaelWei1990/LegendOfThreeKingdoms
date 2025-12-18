using LegendOfThreeKingdoms.Core.Configuration;

namespace LegendOfThreeKingdoms.Core.Abstractions;

/// <summary>
/// Abstraction of a complete game mode (e.g. standard, guozhan).
/// A game mode defines turn structure, faction rules and win conditions.
/// </summary>
public interface IGameMode
{
    /// <summary>
    /// Stable identifier of the mode (e.g. "standard", "guozhan").
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Human-readable name for display purposes.
    /// </summary>
    string DisplayName { get; }

    // Future phases will extend this interface with methods to
    // build initial game state, drive phase progression and
    // evaluate victory conditions based on the current state.
}
