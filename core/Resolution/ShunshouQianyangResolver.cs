using System;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Rules;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Resolver for Shunshou Qianyang (顺手牵羊 / Steal) immediate trick card.
/// Effect: Obtain one card from a target player within distance 1.
/// </summary>
public sealed class ShunshouQianyangResolver : TargetedTrickResolverBase
{
    /// <inheritdoc />
    protected override string MessageKeyPrefix => "resolution.shunshouqianyang";

    /// <inheritdoc />
    protected override string EffectKey => "ShunshouQianyang.Resolve";

    /// <inheritdoc />
    protected override string NullificationResultKeyPrefix => "ShunshouQianyangNullification";

    /// <inheritdoc />
    protected override string CannotTargetSelfMessageKey => "resolution.shunshouqianyang.cannotStealFromSelf";

    /// <inheritdoc />
    protected override string NoSelectableCardsMessageKey => "resolution.shunshouqianyang.noObtainableCards";

    /// <inheritdoc />
    protected override ResolutionResult? ValidateTarget(
        ResolutionContext context,
        Player sourcePlayer,
        Player target)
    {
        // First do base validation (alive and not self)
        var baseResult = base.ValidateTarget(context, sourcePlayer, target);
        if (baseResult is not null)
        {
            return baseResult;
        }

        // Additional validation: distance must be <= 1
        try
        {
            var rangeRuleService = new RangeRuleService();
            var seatDistance = rangeRuleService.GetSeatDistance(context.Game, sourcePlayer, target);
            if (seatDistance > 1)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidTarget,
                    messageKey: $"{MessageKeyPrefix}.targetTooFar",
                    details: new { TargetSeat = target.Seat, Distance = seatDistance });
            }
        }
        catch (Exception ex)
        {
            // If distance calculation fails, return InvalidState
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: $"{MessageKeyPrefix}.distanceCalculationFailed",
                details: new { Exception = ex.Message });
        }

        return null; // Validation passed
    }

    /// <inheritdoc />
    protected override IResolver CreateEffectHandlerResolver(
        Player target,
        Card selectedCard,
        IZone sourceZone,
        ResolutionContext context)
    {
        return new ShunshouQianyangEffectHandlerResolver(target, selectedCard, sourceZone, context.SourcePlayer);
    }
}
