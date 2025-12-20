using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Example skill: Extra Slash - allows the owner to use one additional Slash per turn.
/// This is a locked skill that modifies rules.
/// </summary>
public sealed class ExtraSlashSkill : RuleModifyingSkillBase
{
    /// <inheritdoc />
    public override string Id => "extra_slash";

    /// <inheritdoc />
    public override string Name => "Extra Slash";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Locked;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.ModifiesRules;

    /// <inheritdoc />
    public override int? ModifyMaxSlashPerTurn(int current, Game game, Player owner)
    {
        if (!IsActive(game, owner))
            return null;

        // Increase the limit by 1
        return current + 1;
    }
}

/// <summary>
/// Factory for creating ExtraSlashSkill instances.
/// </summary>
public sealed class ExtraSlashSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new ExtraSlashSkill();
    }
}
