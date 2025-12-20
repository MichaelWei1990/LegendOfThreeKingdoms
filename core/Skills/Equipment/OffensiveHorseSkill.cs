using System;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;

namespace LegendOfThreeKingdoms.Core.Skills.Equipment;

/// <summary>
/// Offensive Horse skill: provides -1 attack distance requirement.
/// When the owner attacks someone, the seat distance requirement is decreased by 1.
/// </summary>
public sealed class OffensiveHorseSkill : BaseSkill, ISeatDistanceModifyingSkill
{
    /// <inheritdoc />
    public override string Id => "offensive_horse";

    /// <inheritdoc />
    public override string Name => "进攻马";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Locked;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.ModifiesRules;

    /// <inheritdoc />
    public int? ModifySeatDistance(int current, Game game, Player from, Player to)
    {
        // Offensive horse decreases the seat distance requirement when the owner attacks someone
        // This means: if 'from' is the owner (attacker), decrease the seat distance by 1
        if (!IsActive(game, from))
            return null;

        // Decrease seat distance by 1 (making it easier to attack from farther away)
        // Ensure the result is at least 1 (distance cannot be negative)
        return Math.Max(1, current - 1);
    }
}

/// <summary>
/// Factory for creating OffensiveHorseSkill instances.
/// </summary>
public sealed class OffensiveHorseSkillFactory : IEquipmentSkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new OffensiveHorseSkill();
    }
}
