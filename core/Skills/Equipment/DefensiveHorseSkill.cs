using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;

namespace LegendOfThreeKingdoms.Core.Skills.Equipment;

/// <summary>
/// Defensive Horse skill: provides +1 defense distance.
/// When someone attacks the owner, the seat distance requirement is increased by 1.
/// </summary>
public sealed class DefensiveHorseSkill : BaseSkill, ISeatDistanceModifyingSkill
{
    /// <inheritdoc />
    public override string Id => "defensive_horse";

    /// <inheritdoc />
    public override string Name => "防御马";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Locked;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.ModifiesRules;

    /// <inheritdoc />
    public int? ModifySeatDistance(int current, Game game, Player from, Player to)
    {
        // Defensive horse increases the seat distance requirement when someone attacks the owner
        // This means: if 'to' is the owner (defender), increase the seat distance by 1
        if (!IsActive(game, to))
            return null;

        // Increase seat distance by 1 (making it harder to attack the owner)
        return current + 1;
    }
}

/// <summary>
/// Factory for creating DefensiveHorseSkill instances.
/// </summary>
public sealed class DefensiveHorseSkillFactory : IEquipmentSkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new DefensiveHorseSkill();
    }
}
