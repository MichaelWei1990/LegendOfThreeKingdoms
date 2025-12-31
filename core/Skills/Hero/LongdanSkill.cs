using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Longdan (龙胆) skill: Locked skill that allows converting Slash to Dodge and Dodge to Slash.
/// You can use Slash as Dodge or Dodge as Slash.
/// </summary>
public sealed class LongdanSkill : BaseSkill, ICardConversionSkill
{
    /// <inheritdoc />
    public override string Id => "longdan";

    /// <inheritdoc />
    public override string Name => "龙胆";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Locked;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.ModifiesRules;

    /// <inheritdoc />
    public Card? CreateVirtualCard(Card originalCard, Game game, Player owner)
    {
        if (originalCard is null)
            return null;

        // Check if skill is active
        if (!IsActive(game, owner))
            return null;

        // If original card is Slash, convert to Dodge
        if (originalCard.CardSubType == CardSubType.Slash)
        {
            return CreateVirtualDodge(originalCard);
        }

        // If original card is Dodge, convert to Slash
        if (originalCard.CardSubType == CardSubType.Dodge)
        {
            return CreateVirtualSlash(originalCard);
        }

        // Other card types are not supported for conversion
        return null;
    }

    /// <summary>
    /// Creates a virtual Dodge card from the original Slash card.
    /// </summary>
    private static Card CreateVirtualDodge(Card originalCard)
    {
        return new Card
        {
            Id = originalCard.Id, // Keep the same ID for tracking
            DefinitionId = "dodge",
            Name = "闪",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Dodge,
            Suit = originalCard.Suit, // Inherit suit
            Rank = originalCard.Rank   // Inherit rank
        };
    }

    /// <summary>
    /// Creates a virtual Slash card from the original Dodge card.
    /// </summary>
    private static Card CreateVirtualSlash(Card originalCard)
    {
        return new Card
        {
            Id = originalCard.Id, // Keep the same ID for tracking
            DefinitionId = "slash",
            Name = "杀",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = originalCard.Suit, // Inherit suit
            Rank = originalCard.Rank   // Inherit rank
        };
    }
}

/// <summary>
/// Factory for creating LongdanSkill instances.
/// </summary>
public sealed class LongdanSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new LongdanSkill();
    }
}

