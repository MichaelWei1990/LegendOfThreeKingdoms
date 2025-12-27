using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;

namespace LegendOfThreeKingdoms.Core.Skills.Equipment;

/// <summary>
/// Zhuge Crossbow skill: allows unlimited Slash usage per turn.
/// When equipped, the owner can use Slash cards without any per-turn limit.
/// Attack Range: 1 (base distance)
/// </summary>
public sealed class ZhugeCrossbowSkill : UnlimitedSlashSkillBase, IAttackDistanceModifyingSkill
{
    /// <inheritdoc />
    public override string Id => "zhuge_crossbow";

    /// <inheritdoc />
    public override string Name => "诸葛连弩";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Locked;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.ModifiesRules;

    /// <summary>
    /// The attack range provided by Zhuge Crossbow (base distance of 1).
    /// </summary>
    private const int AttackRange = 1;

    /// <inheritdoc />
    public override int? ModifyAttackDistance(int current, Game game, Player from, Player to)
    {
        // Zhuge Crossbow provides base attack range of 1
        // If current distance is less than 1, set it to 1
        if (!IsActive(game, from))
            return null;

        // Set attack distance to 1 (weapon's fixed range)
        return AttackRange;
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
