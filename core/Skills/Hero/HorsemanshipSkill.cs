using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Horsemanship skill: provides -1 attack distance requirement.
/// When the owner attacks someone, the seat distance requirement is decreased by 1.
/// This effect stacks with offensive horse equipment (total effect: -2).
/// </summary>
public sealed class HorsemanshipSkill : OffensiveDistanceModifyingSkillBase
{
    /// <inheritdoc />
    public override string Id => "horsemanship";

    /// <inheritdoc />
    public override string Name => "马术";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Locked;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.ModifiesRules;
}

/// <summary>
/// Factory for creating HorsemanshipSkill instances.
/// </summary>
public sealed class HorsemanshipSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new HorsemanshipSkill();
    }
}

