using System.Collections.Concurrent;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Rules;

/// <summary>
/// Service for mapping card subtypes to action IDs.
/// Supports dynamic registration of new mappings for extension packs.
/// </summary>
public sealed class ActionIdMapper
{
    // Cache for action ID mappings
    // Using ConcurrentDictionary for thread-safety
    private static readonly ConcurrentDictionary<CardSubType, string> _cardSubTypeToActionId = new();
    private static readonly ConcurrentDictionary<string, CardSubType> _actionIdToCardSubType = new();

    static ActionIdMapper()
    {
        // Initialize default mappings
        Register(CardSubType.Slash, "UseSlash");
        Register(CardSubType.Peach, "UsePeach");
        Register(CardSubType.GuoheChaiqiao, "UseGuoheChaiqiao");
        Register(CardSubType.Lebusishu, "UseLebusishu");
        // Add more default mappings as needed
    }

    /// <summary>
    /// Gets the action ID for a given card subtype.
    /// Returns null if the card subtype does not have a corresponding action.
    /// </summary>
    /// <param name="cardSubType">The card subtype.</param>
    /// <returns>The action ID, or null if not mapped.</returns>
    public static string? GetActionIdForCardSubType(CardSubType cardSubType)
    {
        return _cardSubTypeToActionId.TryGetValue(cardSubType, out var actionId) ? actionId : null;
    }

    /// <summary>
    /// Gets the card subtype for a given action ID.
    /// Returns null if the action ID does not have a corresponding card subtype.
    /// </summary>
    /// <param name="actionId">The action ID.</param>
    /// <returns>The card subtype, or null if not mapped.</returns>
    public static CardSubType? GetCardSubTypeForActionId(string actionId)
    {
        return _actionIdToCardSubType.TryGetValue(actionId, out var cardSubType) ? cardSubType : null;
    }

    /// <summary>
    /// Registers a new mapping between a card subtype and an action ID.
    /// This allows extension packs to register new card types dynamically.
    /// </summary>
    /// <param name="cardSubType">The card subtype.</param>
    /// <param name="actionId">The action ID.</param>
    public static void Register(CardSubType cardSubType, string actionId)
    {
        if (string.IsNullOrWhiteSpace(actionId))
            throw new ArgumentException("Action ID must not be null or empty.", nameof(actionId));

        _cardSubTypeToActionId.AddOrUpdate(cardSubType, actionId, (key, oldValue) => actionId);
        _actionIdToCardSubType.AddOrUpdate(actionId, cardSubType, (key, oldValue) => cardSubType);
    }

    /// <summary>
    /// Unregisters a mapping for a card subtype.
    /// </summary>
    /// <param name="cardSubType">The card subtype to unregister.</param>
    /// <returns>True if the mapping was removed, false if it didn't exist.</returns>
    public static bool Unregister(CardSubType cardSubType)
    {
        if (_cardSubTypeToActionId.TryRemove(cardSubType, out var actionId))
        {
            _actionIdToCardSubType.TryRemove(actionId, out _);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Clears all mappings. Useful for testing.
    /// </summary>
    public static void Clear()
    {
        _cardSubTypeToActionId.Clear();
        _actionIdToCardSubType.Clear();
    }
}
