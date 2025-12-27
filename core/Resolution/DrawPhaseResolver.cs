using System;
using System.Linq;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Resolver for draw phase: draws cards for the current player.
/// Supports rule modifiers to modify the draw count (e.g., for skills like Yingzi).
/// Also checks for skills that can replace the draw phase (e.g., Tuxi).
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

        // Check for skills that can replace the draw phase
        if (context.SkillManager is not null && context.GetPlayerChoice is not null)
        {
            var replacementSkill = FindDrawPhaseReplacementSkill(context.SkillManager, game, sourcePlayer);
            if (replacementSkill is not null)
            {
                // Ask player if they want to replace draw phase
                var shouldReplace = replacementSkill.ShouldReplaceDrawPhase(
                    game,
                    sourcePlayer,
                    context.GetPlayerChoice);

                if (shouldReplace)
                {
                    // Execute replacement logic
                    var wasEmptyBefore = context.Stack.IsEmpty;
                    replacementSkill.ExecuteDrawPhaseReplacement(
                        game,
                        sourcePlayer,
                        context.GetPlayerChoice,
                        context.CardMoveService,
                        context.EventBus,
                        context.Stack,
                        context);

                    // If stack is still empty (or was empty and is still empty), no replacement was executed
                    // This happens when player confirms but selects 0 targets, or when no valid targets exist
                    // In this case, fall back to normal draw
                    if (context.Stack.IsEmpty && wasEmptyBefore)
                    {
                        // No replacement was actually executed, continue with normal draw
                        // (This happens when player confirms but selects 0 targets)
                    }
                    else
                    {
                        // Replacement was executed (resolver was pushed), return success
                        return ResolutionResult.SuccessResult;
                    }
                }
            }
        }

        // Check for skills that can modify the draw count (e.g., LuoYi)
        int drawCountModification = 0;
        if (context.SkillManager is not null && context.GetPlayerChoice is not null)
        {
            var modifyingSkill = FindDrawPhaseModifyingSkill(context.SkillManager, game, sourcePlayer);
            if (modifyingSkill is not null)
            {
                // Ask player if they want to modify draw phase
                var shouldModify = modifyingSkill.ShouldModifyDrawPhase(
                    game,
                    sourcePlayer,
                    context.GetPlayerChoice);

                if (shouldModify)
                {
                    drawCountModification = modifyingSkill.GetDrawCountModification(game, sourcePlayer);
                    // Activate the modification effect (e.g., set up turn-based buff)
                    modifyingSkill.OnDrawPhaseModified(game, sourcePlayer, context.EventBus);
                }
            }
        }

        // Normal draw phase: calculate draw count with rule modifiers
        var drawCount = CalculateDrawCount(context, sourcePlayer) + drawCountModification;

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

    private static Skills.IDrawPhaseReplacementSkill? FindDrawPhaseReplacementSkill(
        Skills.SkillManager skillManager,
        Game game,
        Player player)
    {
        var skills = skillManager.GetAllSkills(player);
        foreach (var skill in skills)
        {
            if (skill is Skills.IDrawPhaseReplacementSkill replacementSkill &&
                replacementSkill.CanReplaceDrawPhase(game, player))
            {
                return replacementSkill;
            }
        }

        return null;
    }

    private static Skills.IDrawPhaseModifyingSkill? FindDrawPhaseModifyingSkill(
        Skills.SkillManager skillManager,
        Game game,
        Player player)
    {
        var skills = skillManager.GetAllSkills(player);
        foreach (var skill in skills)
        {
            if (skill is Skills.IDrawPhaseModifyingSkill modifyingSkill &&
                modifyingSkill.CanModifyDrawPhase(game, player))
            {
                return modifyingSkill;
            }
        }

        return null;
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
