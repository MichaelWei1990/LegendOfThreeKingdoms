using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;

namespace LegendOfThreeKingdoms.Core.Skills.Equipment;

/// <summary>
/// Fang Tian Hua Ji (方天画戟) skill: allows using Slash with up to 3 targets when it's the last hand card.
/// When equipped, if the owner uses a Slash card from hand and it's their last hand card (after paying cost),
/// they can select up to 3 targets instead of 1.
/// Attack Range: 4
/// </summary>
public sealed class FangTianHuaJiSkill : RuleModifyingSkillBase, IAttackDistanceModifyingSkill
{
    /// <inheritdoc />
    public override string Id => "fang_tian_hua_ji";

    /// <inheritdoc />
    public override string Name => "方天画戟";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Locked;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.ModifiesRules;

    /// <summary>
    /// The attack range provided by Fang Tian Hua Ji.
    /// </summary>
    private const int AttackRange = 4;

    /// <summary>
    /// The additional targets allowed when using Slash as the last hand card.
    /// Base max targets for Slash is 1, so adding 2 allows up to 3 targets total.
    /// </summary>
    private const int AdditionalTargets = 2;

    /// <inheritdoc />
    public override int? ModifyAttackDistance(int current, Game game, Player from, Player to)
    {
        if (!IsActive(game, from))
            return null;

        // Fang Tian Hua Ji provides attack range of 4
        return AttackRange;
    }

    /// <inheritdoc />
    public override int? ModifyMaxTargets(int current, CardUsageContext context)
    {
        if (!IsActive(context.Game, context.SourcePlayer))
            return null;

        // Only applies to Slash cards
        if (context.Card.CardSubType != CardSubType.Slash)
            return null;

        // Check if the card is from hand
        // We need to check if the card is in the player's hand zone
        var sourcePlayer = context.SourcePlayer;
        var card = context.Card;
        var isFromHand = sourcePlayer.HandZone.Cards?.Any(c => c.Id == card.Id) == true;

        if (!isFromHand)
            return null;

        // Check if this would be the last hand card after paying cost
        // Calculate hand count after using this card
        var currentHandCount = sourcePlayer.HandZone.Cards?.Count ?? 0;
        var handCountAfterCost = currentHandCount - 1; // Subtract 1 for the card being used

        // If hand count after cost is 0, this is the last hand card
        if (handCountAfterCost == 0)
        {
            // Allow up to 3 targets (current + 2 additional)
            return current + AdditionalTargets;
        }

        return null;
    }
}

/// <summary>
/// Factory for creating FangTianHuaJiSkill instances.
/// </summary>
public sealed class FangTianHuaJiSkillFactory : IEquipmentSkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new FangTianHuaJiSkill();
    }
}
