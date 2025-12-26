using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Qicai (奇才) skill: Locked skill that removes distance restrictions when using trick cards.
/// </summary>
public sealed class QicaiSkill : RuleModifyingSkillBase
{
    /// <inheritdoc />
    public override string Id => "qicai";

    /// <inheritdoc />
    public override string Name => "奇才";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Locked;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.ModifiesRules;
}

/// <summary>
/// Factory for creating QicaiSkill instances.
/// </summary>
public sealed class QicaiSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new QicaiSkill();
    }
}
