using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Yingzi skill: increases draw count by 1 during draw phase.
/// When the owner has this skill, they draw one additional card during draw phase (2 base + 1 = 3 cards total).
/// </summary>
public sealed class YingziSkill : RuleModifyingSkillBase
{
    /// <inheritdoc />
    public override string Id => "yingzi";

    /// <inheritdoc />
    public override string Name => "英姿";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Locked;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.ModifiesRules;

    /// <inheritdoc />
    public override int? ModifyDrawCount(int current, Game game, Player owner)
    {
        if (!IsActive(game, owner))
            return null;

        // Increase draw count by 1
        return current + 1;
    }
}

/// <summary>
/// Factory for creating YingziSkill instances.
/// </summary>
public sealed class YingziSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new YingziSkill();
    }
}
