using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// QingGuo (倾国) skill: Conversion skill that allows using a black hand card as Dodge (闪).
/// When you need to use or play Dodge, you can use a black hand card (Spade or Club) as Dodge.
/// </summary>
public sealed class QingGuoSkill : BaseSkill, ICardConversionSkill
{
    /// <inheritdoc />
    public override string Id => "qingguo";

    /// <inheritdoc />
    public override string Name => "倾国";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Active;

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

        // QingGuo can only convert black cards (Spade or Club)
        if (!originalCard.Suit.IsBlack())
            return null;

        // Check if card is in hand zone (only hand cards can be converted)
        var handCards = owner.HandZone.Cards;
        if (!handCards.Any(c => c.Id == originalCard.Id))
            return null;

        // Create virtual Dodge card
        return new Card
        {
            Id = originalCard.Id, // Keep the same ID for tracking
            DefinitionId = "dodge",
            Name = "闪",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Dodge,
            Suit = originalCard.Suit, // Keep original suit
            Rank = originalCard.Rank   // Keep original rank
        };
    }
}

/// <summary>
/// Factory for creating QingGuoSkill instances.
/// </summary>
public sealed class QingGuoSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new QingGuoSkill();
    }
}

