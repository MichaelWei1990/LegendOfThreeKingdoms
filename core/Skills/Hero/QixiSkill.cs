using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Qixi (奇袭) skill: Active skill that allows using a black card as GuoheChaiqiao (过河拆桥).
/// </summary>
public sealed class QixiSkill : BaseSkill, ICardConversionSkill
{
    /// <inheritdoc />
    public override string Id => "qixi";

    /// <inheritdoc />
    public override string Name => "奇袭";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Active;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.ModifiesRules;

    /// <inheritdoc />
    public Card? CreateVirtualCard(Card originalCard, Game game, Player owner)
    {
        if (originalCard is null)
            return null;

        // Qixi can only convert black cards
        if (!originalCard.Suit.IsBlack())
            return null;

        // Check if skill is active
        if (!IsActive(game, owner))
            return null;

        // Create virtual GuoheChaiqiao card
        return new Card
        {
            Id = originalCard.Id, // Keep the same ID for tracking
            DefinitionId = "guohe_chaiqiao",
            Name = "过河拆桥",
            CardType = CardType.Trick,
            CardSubType = CardSubType.GuoheChaiqiao,
            Suit = originalCard.Suit, // Keep original suit
            Rank = originalCard.Rank   // Keep original rank
        };
    }
}

/// <summary>
/// Factory for creating QixiSkill instances.
/// </summary>
public sealed class QixiSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new QixiSkill();
    }
}
