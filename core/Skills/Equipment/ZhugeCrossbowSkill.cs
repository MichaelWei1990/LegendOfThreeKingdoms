using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;

namespace LegendOfThreeKingdoms.Core.Skills.Equipment;

/// <summary>
/// Zhuge Crossbow skill: allows unlimited Slash usage per turn.
/// When equipped, the owner can use Slash cards without any per-turn limit.
/// </summary>
public sealed class ZhugeCrossbowSkill : RuleModifyingSkillBase
{
    /// <inheritdoc />
    public override string Id => "zhuge_crossbow";

    /// <inheritdoc />
    public override string Name => "诸葛连弩";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Locked;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.ModifiesRules;

    /// <inheritdoc />
    public override int? ModifyMaxSlashPerTurn(int current, Game game, Player owner)
    {
        if (!IsActive(game, owner))
            return null;

        // Return int.MaxValue to represent unlimited Slash usage
        return int.MaxValue;
    }
}

/// <summary>
/// Factory for creating ZhugeCrossbowSkill instances.
/// </summary>
public sealed class ZhugeCrossbowSkillFactory : IEquipmentSkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new ZhugeCrossbowSkill();
    }
}
