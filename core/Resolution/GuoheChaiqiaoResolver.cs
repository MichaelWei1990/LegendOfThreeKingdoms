using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Resolver for Guohe Chaiqiao (过河拆桥 / Dismantle) immediate trick card.
/// Effect: Discard one card from a target player (no distance restriction).
/// </summary>
public sealed class GuoheChaiqiaoResolver : TargetedTrickResolverBase
{
    /// <inheritdoc />
    protected override string MessageKeyPrefix => "resolution.guohechaiqiao";

    /// <inheritdoc />
    protected override string EffectKey => "GuoheChaiqiao.Resolve";

    /// <inheritdoc />
    protected override string NullificationResultKeyPrefix => "GuoheChaiqiaoNullification";

    /// <inheritdoc />
    protected override string CannotTargetSelfMessageKey => "resolution.guohechaiqiao.cannotDismantleSelf";

    /// <inheritdoc />
    protected override string NoSelectableCardsMessageKey => "resolution.guohechaiqiao.noDiscardableCards";

    /// <inheritdoc />
    protected override ResolutionResult? ValidateTarget(
        ResolutionContext context,
        Player sourcePlayer,
        Player target)
    {
        // Note: GuoheChaiqiao has NO distance restriction (unlike ShunshouQianyang)
        // Just use base validation (alive and not self)
        return base.ValidateTarget(context, sourcePlayer, target);
    }

    /// <inheritdoc />
    protected override IResolver CreateEffectHandlerResolver(
        Player target,
        Card selectedCard,
        IZone sourceZone,
        ResolutionContext context)
    {
        return new GuoheChaiqiaoEffectHandlerResolver(target, selectedCard, sourceZone);
    }
}
