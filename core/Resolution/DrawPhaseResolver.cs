using System;
using System.Linq;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Resolver for draw phase: draws cards for the current player.
/// Supports rule modifiers to modify the draw count (e.g., for skills like Yingzi).
/// </summary>
public sealed class DrawPhaseResolver : IResolver
{
    private const int DefaultDrawCount = 2;

    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        var game = context.Game;
        var sourcePlayer = context.SourcePlayer;

        // Calculate draw count with rule modifiers
        var drawCount = CalculateDrawCount(context, sourcePlayer);

        // Draw cards
        try
        {
            context.CardMoveService.DrawCards(game, sourcePlayer, drawCount);
        }
        catch (Exception ex)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.drawphase.drawFailed",
                details: new { Exception = ex.Message });
        }

        return ResolutionResult.SuccessResult;
    }

    private static int CalculateDrawCount(ResolutionContext context, Player player)
    {
        var baseCount = DefaultDrawCount;

        // Apply rule modifiers if available
        if (context.RuleService is RuleService ruleService)
        {
            var modifiers = ruleService.GetModifiersFor(context.Game, player);
            foreach (var modifier in modifiers)
            {
                var modified = modifier.ModifyDrawCount(baseCount, context.Game, player);
                if (modified.HasValue)
                {
                    baseCount = modified.Value;
                }
            }
        }

        return Math.Max(0, baseCount); // Ensure non-negative
    }
}
