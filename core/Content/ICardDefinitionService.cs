namespace LegendOfThreeKingdoms.Core.Content;

/// <summary>
/// Service for querying card definitions by definition ID.
/// Provides mapping from definition IDs to card metadata (CardType, CardSubType, Name, etc.).
/// </summary>
public interface ICardDefinitionService
{
    /// <summary>
    /// Gets a card definition by its definition ID.
    /// </summary>
    /// <param name="definitionId">The definition ID (e.g., "Base.Slash", "Trick.WuzhongShengyou").</param>
    /// <returns>The card definition, or null if not found.</returns>
    CardDefinition? GetDefinition(string definitionId);

    /// <summary>
    /// Checks whether a definition ID exists in the service.
    /// </summary>
    /// <param name="definitionId">The definition ID to check.</param>
    /// <returns>True if the definition exists, false otherwise.</returns>
    bool HasDefinition(string definitionId);
}
