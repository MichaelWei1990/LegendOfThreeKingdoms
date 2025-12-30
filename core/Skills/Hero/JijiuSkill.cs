using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Jijiu (急救) skill: Conversion skill that allows using a red card as Peach (桃) outside your turn.
/// When you need to use Peach (e.g., in a dying rescue window), you can use a red hand card as Peach, but only outside your turn.
/// </summary>
public sealed class JijiuSkill : BaseSkill, ICardConversionSkill
{
    /// <inheritdoc />
    public override string Id => "jijiu";

    /// <inheritdoc />
    public override string Name => "急救";

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

        // Jijiu can only be used outside your turn
        // Rule: Current turn player must not be the owner
        if (game.CurrentPlayerSeat == owner.Seat)
            return null;

        // Jijiu can only convert red cards (Heart or Diamond)
        if (!originalCard.Suit.IsRed())
            return null;

        // Check if card is in hand zone (only hand cards can be converted)
        var handCards = owner.HandZone.Cards;
        if (!handCards.Any(c => c.Id == originalCard.Id))
            return null;

        // Create virtual Peach card
        return new Card
        {
            Id = originalCard.Id, // Keep the same ID for tracking
            DefinitionId = "peach",
            Name = "桃",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Peach,
            Suit = originalCard.Suit, // Keep original suit
            Rank = originalCard.Rank   // Keep original rank
        };
    }
}

/// <summary>
/// Factory for creating JijiuSkill instances.
/// </summary>
public sealed class JijiuSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new JijiuSkill();
    }
}

