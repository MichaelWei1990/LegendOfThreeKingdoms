using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Empty City (空城) skill: Locked skill that prevents the owner from being targeted by Slash and Duel when the owner has no hand cards.
/// </summary>
public sealed class EmptyCitySkill : BaseSkill, ITargetFilteringSkill
{
    /// <inheritdoc />
    public override string Id => "empty_city";

    /// <inheritdoc />
    public override string Name => "空城";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Locked;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.ModifiesRules;

    /// <inheritdoc />
    public bool ShouldExcludeTarget(Game game, Player owner, Card card, Player potentialTarget)
    {
        // Only apply if the skill is active for the owner
        if (!IsActive(game, owner))
        {
            return false; // Skill not active, don't exclude
        }

        // Only exclude if the potential target is the owner (the one with Empty City skill)
        if (potentialTarget.Seat != owner.Seat)
        {
            return false; // Not targeting the owner, don't exclude
        }

        // Check if owner has no hand cards (hand card count = 0)
        var handCardCount = owner.HandZone.Cards.Count;
        if (handCardCount > 0)
        {
            return false; // Owner has hand cards, skill not active
        }

        // Exclude if the card is Slash or Duel
        return card.CardSubType == CardSubType.Slash || card.CardSubType == CardSubType.Duel;
    }
}

/// <summary>
/// Factory for creating EmptyCitySkill instances.
/// </summary>
public sealed class EmptyCitySkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new EmptyCitySkill();
    }
}
