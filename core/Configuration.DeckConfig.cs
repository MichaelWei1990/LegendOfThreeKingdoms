using System.Collections.Generic;

namespace LegendOfThreeKingdoms.Core.Configuration;

/// <summary>
/// Configuration for deck composition and enabled card packs.
/// </summary>
public sealed class DeckConfig
{
    /// <summary>
    /// Identifiers of enabled card packs (e.g. "Base", "StandardExpansion").
    /// </summary>
    public IList<string> IncludedPacks { get; init; } = new List<string>();

    /// <summary>
    /// Optional overrides to tweak the default deck list for testing or special modes.
    /// </summary>
    public IList<DeckOverride>? Overrides { get; init; }
}

/// <summary>
/// Describes an override to the default deck list (add, remove or change count of a card).
/// </summary>
public sealed class DeckOverride
{
    /// <summary>
    /// Identifier of the card definition to override.
    /// </summary>
    public string CardId { get; init; } = string.Empty;

    /// <summary>
    /// Desired count of this card in the deck. Null means keep existing count.
    /// </summary>
    public int? Count { get; init; }

    /// <summary>
    /// When true, the card is removed from the deck regardless of Count.
    /// </summary>
    public bool Remove { get; init; }
}
