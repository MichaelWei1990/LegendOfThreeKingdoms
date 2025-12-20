using LegendOfThreeKingdoms.Core.Configuration;

namespace LegendOfThreeKingdoms.Core.Model;

/// <summary>
/// Runtime instance of a single card in a game.
/// This is a pure data object and does not contain rule logic.
/// </summary>
public sealed class Card
{
    /// <summary>
    /// Unique identifier of this card instance within a single game.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Identifier of the card definition from content (e.g. a specific Slash or equipment).
    /// </summary>
    public string DefinitionId { get; init; } = string.Empty;

    /// <summary>
    /// Display name of the card (e.g. "赤兔", "的卢").
    /// Used for UI display purposes.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Suit of the card, used by many judgements and skills.
    /// </summary>
    public Suit Suit { get; init; }

    /// <summary>
    /// Rank (number) of the card, typically 1-13.
    /// </summary>
    public int Rank { get; init; }

    /// <summary>
    /// High level type of the card (basic, trick, equip).
    /// </summary>
    public CardType CardType { get; init; }

    /// <summary>
    /// Fine-grained subtype of the card (Slash, Dodge, Peach, Weapon, etc.).
    /// </summary>
    public CardSubType CardSubType { get; init; } = CardSubType.Unknown;
}
