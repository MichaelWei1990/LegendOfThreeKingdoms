using System.Collections.Generic;

namespace LegendOfThreeKingdoms.Core.Configuration;

/// <summary>
/// Game-level configuration for a single match.
/// This type is a pure data container and should not contain behavior.
/// </summary>
public sealed class GameConfig
{
    /// <summary>
    /// Player configurations in seat order starting from 0.
    /// </summary>
    public required IList<PlayerConfig> PlayerConfigs { get; init; }

    /// <summary>
    /// Deck composition and enabled packs for this match.
    /// </summary>
    public required DeckConfig DeckConfig { get; init; }

    /// <summary>
    /// Number of cards each player should draw as their initial hand
    /// during game setup. Defaults to 4 which matches the standard mode.
    /// </summary>
    public int InitialHandCardCount { get; init; } = 4;

    /// <summary>
    /// Optional random seed that will be passed into the random source.
    /// Same seed + same choices should lead to deterministic results.
    /// </summary>
    public int? Seed { get; init; }

    /// <summary>
    /// Identifier of the game mode to be used (e.g. "standard", "guozhan").
    /// </summary>
    public required string GameModeId { get; init; }

    /// <summary>
    /// Optional mode-specific options for variants and toggles.
    /// Keys and value semantics are interpreted by the selected game mode.
    /// </summary>
    public IDictionary<string, object?>? GameVariantOptions { get; init; }
}
