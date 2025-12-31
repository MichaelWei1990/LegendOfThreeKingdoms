using System.Collections.Generic;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Character;

/// <summary>
/// Catalog service for querying character definitions.
/// Provides access to character metadata including skills, health, and faction.
/// </summary>
public interface ICharacterCatalog
{
    /// <summary>
    /// Gets a character definition by its ID.
    /// </summary>
    /// <param name="characterId">The unique identifier of the character.</param>
    /// <returns>The character definition, or null if not found.</returns>
    CharacterDefinition? GetCharacter(string characterId);

    /// <summary>
    /// Gets all available character definitions.
    /// </summary>
    /// <returns>An enumerable of all character definitions.</returns>
    IEnumerable<CharacterDefinition> GetAllCharacters();

    /// <summary>
    /// Checks whether a character ID exists in the catalog.
    /// </summary>
    /// <param name="characterId">The character ID to check.</param>
    /// <returns>True if the character exists, false otherwise.</returns>
    bool HasCharacter(string characterId);
}

/// <summary>
/// Service for managing character selection process.
/// Handles offering candidates, validating selections, and registering characters with skills.
/// </summary>
public interface ICharacterSelectionService
{
    /// <summary>
    /// Offers candidate characters to a player for selection.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="playerSeat">The seat index of the player.</param>
    /// <param name="candidateCharacterIds">List of character IDs to offer as candidates.</param>
    /// <exception cref="ArgumentNullException">Thrown if game or candidateCharacterIds is null.</exception>
    /// <exception cref="ArgumentException">Thrown if candidateCharacterIds is empty or contains invalid IDs.</exception>
    void OfferCharacters(Game game, int playerSeat, IReadOnlyList<string> candidateCharacterIds);

    /// <summary>
    /// Allows a player to select a character from their candidate list.
    /// This method will:
    /// - Validate the selection
    /// - Initialize player attributes (HeroId, FactionId, Gender, MaxHealth, CurrentHealth)
    /// - Register skills (including conditional Lord skills)
    /// - Publish selection and registration events
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="playerSeat">The seat index of the player.</param>
    /// <param name="selectedCharacterId">The character ID selected by the player.</param>
    /// <exception cref="ArgumentNullException">Thrown if game is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the selection is invalid (not in candidates, already selected, etc.).</exception>
    void SelectCharacter(Game game, int playerSeat, string selectedCharacterId);
}
