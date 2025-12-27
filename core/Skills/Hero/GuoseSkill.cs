using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Guose (国色) skill: Active skill that allows using a diamond card as Lebusishu (乐不思蜀).
/// </summary>
public sealed class GuoseSkill : BaseSkill, ICardConversionSkill
{
    /// <inheritdoc />
    public override string Id => "guose";

    /// <inheritdoc />
    public override string Name => "国色";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Active;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.ModifiesRules;

    /// <inheritdoc />
    public Card? CreateVirtualCard(Card originalCard, Game game, Player owner)
    {
        if (originalCard is null)
            return null;

        // Guose can only convert diamond cards
        if (originalCard.Suit != Suit.Diamond)
            return null;

        // Check if skill is active
        if (!IsActive(game, owner))
            return null;

        // Create virtual Lebusishu card
        return new Card
        {
            Id = originalCard.Id, // Keep the same ID for tracking
            DefinitionId = "lebusishu",
            Name = "乐不思蜀",
            CardType = CardType.Trick,
            CardSubType = CardSubType.Lebusishu,
            Suit = originalCard.Suit, // Keep original suit
            Rank = originalCard.Rank   // Keep original rank
        };
    }
}

/// <summary>
/// Factory for creating GuoseSkill instances.
/// </summary>
public sealed class GuoseSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new GuoseSkill();
    }
}
