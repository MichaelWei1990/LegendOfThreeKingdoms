using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Resolution;

namespace LegendOfThreeKingdoms.Core.Skills.Equipment;

/// <summary>
/// Qinggang Sword skill: provides +1 attack distance and ignores armor effects.
/// When the owner attacks someone, the attack distance is increased by 1.
/// When the owner uses Slash, armor effects (like Renwang Shield) are ignored.
/// </summary>
public sealed class QinggangSwordSkill : BaseSkill, IAttackDistanceModifyingSkill, IArmorIgnoreProvider
{
    /// <inheritdoc />
    public override string Id => "qinggang_sword";

    /// <inheritdoc />
    public override string Name => "青釭剑";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Locked;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.ModifiesRules;

    /// <inheritdoc />
    public int? ModifyAttackDistance(int current, Game game, Player from, Player to)
    {
        // Qinggang Sword increases the attack distance when the owner attacks someone
        // This means: if 'from' is the owner (attacker), increase the attack distance by 1
        if (!IsActive(game, from))
            return null;

        // Increase attack distance by 1 (making it possible to attack from farther away)
        return current + 1;
    }

    /// <inheritdoc />
    public bool ShouldIgnoreArmor(CardEffectContext context)
    {
        // Only ignore armor when the source player (attacker) is the owner
        if (!IsActive(context.Game, context.SourcePlayer))
            return false;

        // Only ignore armor for Slash cards
        if (context.Card.CardSubType != CardSubType.Slash)
            return false;

        // Qinggang Sword ignores armor effects
        return true;
    }
}

/// <summary>
/// Factory for creating QinggangSwordSkill instances.
/// </summary>
public sealed class QinggangSwordSkillFactory : IEquipmentSkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new QinggangSwordSkill();
    }
}
