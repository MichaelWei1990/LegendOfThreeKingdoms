using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Skills;

namespace LegendOfThreeKingdoms.Core.Skills.Equipment;

/// <summary>
/// Renwang Shield skill: makes black Slash cards ineffective on the owner.
/// Locked skill: when equipped, black Slash cards are invalidated before response windows.
/// </summary>
public sealed class RenwangShieldSkill : BaseSkill, ICardEffectFilteringSkill
{
    /// <inheritdoc />
    public override string Id => "renwang_shield";

    /// <inheritdoc />
    public override string Name => "仁王盾";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Locked;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.ModifiesRules;

    /// <inheritdoc />
    public bool IsEffective(CardEffectContext context, out EffectVetoReason? reason)
    {
        reason = null;

        // Only apply if the skill is active for the target (owner)
        if (!IsActive(context.Game, context.TargetPlayer))
        {
            return true; // Skill not active, effect is effective
        }

        // Only filter Slash cards
        if (context.Card.CardSubType != CardSubType.Slash)
        {
            return true; // Not a Slash, effect is effective
        }

        // Only filter black Slash (Spade or Club)
        if (!context.Card.Suit.IsBlack())
        {
            return true; // Not black, effect is effective
        }

        // Note: Armor ignore check is done by the resolution system before calling this method
        // If armor is ignored, this method won't be called for armor-based filters

        // Black Slash is ineffective on Renwang Shield owner
        reason = new EffectVetoReason(
            Source: "RenwangShield",
            Reason: "Black Slash invalidated by Renwang Shield",
            Details: new
            {
                CardId = context.Card.Id,
                CardSuit = context.Card.Suit.ToString(),
                SourceSeat = context.SourcePlayer.Seat,
                TargetSeat = context.TargetPlayer.Seat
            }
        );
        return false; // Effect is vetoed
    }
}

/// <summary>
/// Factory for creating RenwangShieldSkill instances.
/// </summary>
public sealed class RenwangShieldSkillFactory : IEquipmentSkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new RenwangShieldSkill();
    }
}
