using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Content;

/// <summary>
/// Definition of a card in the game.
/// Contains metadata about a card type, including its type, subtype, and display name.
/// </summary>
public sealed class CardDefinition
{
    /// <summary>
    /// The definition ID of this card (e.g., "Base.Slash", "Trick.WuzhongShengyou").
    /// </summary>
    public required string DefinitionId { get; init; }

    /// <summary>
    /// Display name of the card (e.g., "杀", "无中生有", "诸葛连弩").
    /// Used for UI display purposes.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// High level type of the card (Basic, Trick, Equip).
    /// </summary>
    public required CardType CardType { get; init; }

    /// <summary>
    /// Fine-grained subtype of the card (Slash, Dodge, Peach, Weapon, etc.).
    /// </summary>
    public required CardSubType CardSubType { get; init; }

    /// <summary>
    /// Optional default suit for the card.
    /// Some cards may have a specific suit in the standard deck.
    /// </summary>
    public Suit? DefaultSuit { get; init; }
}
