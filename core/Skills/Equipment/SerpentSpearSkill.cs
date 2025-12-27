using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Skills.Equipment;

/// <summary>
/// Serpent Spear (丈八蛇矛) skill: Active skill that allows using two hand cards as a Slash.
/// When you need to use or play a Slash, you can select two hand cards to convert into a virtual Slash.
/// The color of the virtual Slash is determined by the colors of the two cards:
/// - Both black → black
/// - Both red → red
/// - One red, one black → colorless
/// </summary>
public sealed class SerpentSpearSkill : BaseSkill, IMultiCardConversionSkill
{
    private Game? _game;
    private Player? _owner;
    private IEventBus? _eventBus;

    /// <inheritdoc />
    public override string Id => "serpent_spear";

    /// <inheritdoc />
    public override string Name => "丈八";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Active;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.ModifiesRules;

    /// <inheritdoc />
    public int RequiredCardCount => 2;

    /// <inheritdoc />
    public CardSubType TargetCardSubType => CardSubType.Slash;

    /// <inheritdoc />
    public override void Attach(Game game, Player owner, IEventBus eventBus)
    {
        if (game is null)
            throw new ArgumentNullException(nameof(game));
        if (owner is null)
            throw new ArgumentNullException(nameof(owner));
        if (eventBus is null)
            throw new ArgumentNullException(nameof(eventBus));

        _game = game;
        _owner = owner;
        _eventBus = eventBus;
    }

    /// <inheritdoc />
    public override void Detach(Game game, Player owner, IEventBus eventBus)
    {
        _game = null;
        _owner = null;
        _eventBus = null;
    }

    /// <inheritdoc />
    public Card? CreateVirtualCardFromMultiple(IReadOnlyList<Card> originalCards, Game game, Player owner)
    {
        if (originalCards is null)
            return null;

        // Must have exactly 2 cards
        if (originalCards.Count != RequiredCardCount)
            return null;

        // Check if skill is active
        if (!IsActive(game, owner))
            return null;

        // Verify all cards are from hand zone
        var handCards = owner.HandZone.Cards?.ToList() ?? new List<Card>();
        if (!originalCards.All(c => handCards.Any(h => h.Id == c.Id)))
        {
            return null; // Not all cards are from hand
        }

        // Determine virtual card color
        var color = DetermineVirtualCardColor(originalCards);

        // Create virtual Slash card
        // Use the first card's ID as the virtual card ID (for tracking)
        // Suit will be determined based on color
        var virtualSuit = color switch
        {
            CardColor.Black => Suit.Spade, // Use Spade to represent black
            CardColor.Red => Suit.Heart,   // Use Heart to represent red
            CardColor.None => Suit.Spade,  // Use Spade as default for colorless (will be checked via color)
            _ => Suit.Spade
        };

        // Create virtual card
        var virtualCard = new Card
        {
            Id = originalCards[0].Id, // Use first card's ID for tracking
            DefinitionId = "slash_serpent_spear",
            Name = "杀",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = virtualSuit,
            Rank = originalCards[0].Rank // Use first card's rank
        };

        // Store color information in Flags for later retrieval
        // Note: We'll need to extend Card or use a different mechanism to store color
        // For now, we'll use a convention: if Suit is Spade and both cards are red, it's actually red
        // This is a workaround - ideally Card should have a Color property

        return virtualCard;
    }

    /// <inheritdoc />
    public CardColor DetermineVirtualCardColor(IReadOnlyList<Card> originalCards)
    {
        if (originalCards is null || originalCards.Count != 2)
            return CardColor.None;

        var card1 = originalCards[0];
        var card2 = originalCards[1];

        var card1IsBlack = card1.Suit.IsBlack();
        var card2IsBlack = card2.Suit.IsBlack();
        var card1IsRed = card1.Suit.IsRed();
        var card2IsRed = card2.Suit.IsRed();

        // Both black
        if (card1IsBlack && card2IsBlack)
            return CardColor.Black;

        // Both red
        if (card1IsRed && card2IsRed)
            return CardColor.Red;

        // One red, one black (or one is neither) → colorless
        return CardColor.None;
    }
}

/// <summary>
/// Factory for creating SerpentSpearSkill instances.
/// </summary>
public sealed class SerpentSpearSkillFactory : IEquipmentSkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new SerpentSpearSkill();
    }
}

