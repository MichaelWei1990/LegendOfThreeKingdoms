using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Roar skill: allows unlimited Slash usage per turn.
/// When the owner has this skill, they can use Slash cards without any per-turn limit.
/// This effect stacks with Zhuge Crossbow equipment (both set limit to int.MaxValue).
/// </summary>
public sealed class RoarSkill : UnlimitedSlashSkillBase
{
    /// <inheritdoc />
    public override string Id => "roar";

    /// <inheritdoc />
    public override string Name => "咆哮";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Locked;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.ModifiesRules;
}

/// <summary>
/// Factory for creating RoarSkill instances.
/// </summary>
public sealed class RoarSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new RoarSkill();
    }
}
