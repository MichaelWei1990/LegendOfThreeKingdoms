using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Modesty (谦逊) skill: Locked skill that prevents the owner from being targeted by ShunshouQianyang and Lebusishu.
/// </summary>
public sealed class ModestySkill : BaseSkill, ITargetFilteringSkill
{
    /// <inheritdoc />
    public override string Id => "modesty";

    /// <inheritdoc />
    public override string Name => "谦逊";

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

        // Only exclude if the potential target is the owner (the one with Modesty skill)
        if (potentialTarget.Seat != owner.Seat)
        {
            return false; // Not targeting the owner, don't exclude
        }

        // Exclude if the card is ShunshouQianyang or Lebusishu
        return card.CardSubType == CardSubType.ShunshouQianyang || card.CardSubType == CardSubType.Lebusishu;
    }
}

/// <summary>
/// Factory for creating ModestySkill instances.
/// </summary>
public sealed class ModestySkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new ModestySkill();
    }
}
