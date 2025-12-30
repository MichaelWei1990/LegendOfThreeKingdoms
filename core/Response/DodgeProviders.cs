using System;
using System.Linq;
using System.Reflection;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Skills.Equipment;

namespace LegendOfThreeKingdoms.Core.Response;

/// <summary>
/// Dodge provider that uses response assistance skills (e.g., Hujia 护驾).
/// Priority: 0 (highest - executed first).
/// </summary>
public sealed class ResponseAssistanceDodgeProvider : IDodgeProvider
{
    /// <inheritdoc />
    public int Priority => 0;

    /// <inheritdoc />
    public bool TryProvideDodge(ResolutionContext context, DodgeRequestContext requestContext)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (requestContext is null) throw new ArgumentNullException(nameof(requestContext));

        // Check if already resolved
        if (requestContext.Resolved)
            return true;

        // Check if SkillManager is available
        if (context.SkillManager is null || context.GetPlayerChoice is null)
            return false;

        // Find response assistance skill for the defender
        var defender = requestContext.Defender;
        var targetSkills = context.SkillManager.GetActiveSkills(context.Game, defender);
        var assistanceSkill = targetSkills.OfType<IResponseAssistanceSkill>()
            .FirstOrDefault();

        if (assistanceSkill is null)
            return false;

        // Check if the skill can provide assistance for this response type
        var sourceEvent = requestContext.SourceEvent;
        if (!assistanceSkill.CanProvideAssistance(context.Game, defender, ResponseType.JinkAgainstSlash, sourceEvent))
            return false;

        // Ask defender if they want to use the assistance skill
        if (!assistanceSkill.ShouldActivate(context.Game, defender, context.GetPlayerChoice))
            return false;

        // Create and push ResponseAssistanceResolver
        var assistanceContext = new ResolutionContext(
            context.Game,
            defender,
            Action: null,
            Choice: null,
            context.Stack,
            context.CardMoveService,
            context.RuleService,
            context.PendingDamage,
            context.LogSink,
            context.GetPlayerChoice,
            context.IntermediateResults,
            context.EventBus,
            context.LogCollector,
            context.SkillManager,
            context.EquipmentSkillRegistry,
            context.JudgementService);

        var assistanceResolver = new ResponseAssistanceResolver(
            beneficiary: defender,
            responseType: ResponseType.JinkAgainstSlash,
            sourceEvent: sourceEvent,
            assistanceSkill: assistanceSkill);

        // Store DodgeRequestContext in IntermediateResults so ResponseAssistanceResolver can update it
        if (context.IntermediateResults is not null)
        {
            context.IntermediateResults["DodgeRequestContext"] = requestContext;
        }

        // Push resolver onto stack
        context.Stack.Push(assistanceResolver, assistanceContext);

        // Mark that high-priority provider has been activated
        // This prevents lower-priority providers (Bagua Array, Manual Dodge) from executing
        requestContext.HighPriorityProviderActivated = true;

        // Note: We don't set Resolved here because the resolver will handle it asynchronously
        // The resolver will update the context when it completes
        return false; // Return false - resolver will handle resolution asynchronously
    }
}

/// <summary>
/// Dodge provider that uses Bagua Array (八卦阵) equipment skill.
/// Priority: 1 (executed after response assistance, before manual Dodge).
/// </summary>
public sealed class BaguaArrayDodgeProvider : IDodgeProvider
{
    /// <inheritdoc />
    public int Priority => 1;

    /// <inheritdoc />
    public bool TryProvideDodge(ResolutionContext context, DodgeRequestContext requestContext)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (requestContext is null) throw new ArgumentNullException(nameof(requestContext));

        // Check if already resolved
        if (requestContext.Resolved)
            return true;

        // Check if high-priority provider (e.g., Response Assistance) has been activated
        // If so, skip this provider (Bagua Array has lower priority)
        if (requestContext.HighPriorityProviderActivated)
            return false;

        // Check if required services are available
        if (context.SkillManager is null || 
            context.JudgementService is null || 
            context.CardMoveService is null || 
            context.GetPlayerChoice is null)
            return false;

        var defender = requestContext.Defender;
        var game = context.Game;

        // Find Bagua Array skill
        var defenderSkills = context.SkillManager.GetActiveSkills(game, defender);
        var baguaSkill = defenderSkills.OfType<IResponseEnhancementSkill>()
            .FirstOrDefault(s => s.Id == "bagua_array");

        if (baguaSkill is null)
            return false;

        // Check if Bagua Array can provide response
        var sourceEvent = requestContext.SourceEvent;
        if (!baguaSkill.CanProvideResponse(game, defender, ResponseType.JinkAgainstSlash, sourceEvent))
            return false;

        // Ask defender if they want to use Bagua Array
        var choiceRequest = new ChoiceRequest(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: defender.Seat,
            ChoiceType: ChoiceType.Confirm,
            TargetConstraints: null,
            AllowedCards: null,
            ResponseWindowId: null,
            CanPass: true);

        try
        {
            var choice = context.GetPlayerChoice(choiceRequest);
            if (choice?.Confirmed != true)
                return false;
        }
        catch
        {
            return false;
        }

        // Execute Bagua Array judgement
        var success = baguaSkill.ExecuteAlternativeResponse(
            game,
            defender,
            ResponseType.JinkAgainstSlash,
            sourceEvent,
            context.GetPlayerChoice,
            context.JudgementService,
            context.CardMoveService);

        if (success)
        {
            // Bagua Array succeeded - mark as resolved
            requestContext.Resolved = true;
            requestContext.ProvidedBy = defender;
            // Note: ProvidedCard is null for virtual Dodge from Bagua Array
            return true;
        }

        return false;
    }
}

/// <summary>
/// Dodge provider that uses manual Dodge card from hand.
/// Priority: 2 (lowest - executed last, as fallback).
/// </summary>
public sealed class ManualDodgeProvider : IDodgeProvider
{
    /// <inheritdoc />
    public int Priority => 2;

    /// <inheritdoc />
    public bool TryProvideDodge(ResolutionContext context, DodgeRequestContext requestContext)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (requestContext is null) throw new ArgumentNullException(nameof(requestContext));

        // Check if already resolved
        if (requestContext.Resolved)
            return true;

        // Check if high-priority provider (e.g., Response Assistance) has been activated
        // If so, skip this provider (Manual Dodge has lowest priority)
        if (requestContext.HighPriorityProviderActivated)
            return false;

        // Check if required services are available
        if (context.GetPlayerChoice is null)
            return false;

        var defender = requestContext.Defender;
        var attacker = requestContext.Attacker;
        var sourceEvent = requestContext.SourceEvent;

        // Calculate required Jink count (check if attacker has Wushuang or similar skills)
        int requiredCount = 1;
        if (sourceEvent is not null)
        {
            // Try to extract SlashCard from sourceEvent
            var slashCardProperty = sourceEvent.GetType().GetProperty("SlashCard");
            if (slashCardProperty?.GetValue(sourceEvent) is Card slashCard && context.SkillManager is not null)
            {
                requiredCount = ResponseRequirementCalculator.CalculateJinkRequirementForSlash(
                    context.Game,
                    attacker,
                    defender,
                    slashCard,
                    context.SkillManager);
            }
        }

        // Create normal response window for manual Dodge
        var responseWindow = context.CreateJinkResponseWindow(
            targetPlayer: defender,
            sourceEvent: sourceEvent,
            getPlayerChoice: context.GetPlayerChoice,
            requiredCount: requiredCount);

        // Push response window onto stack
        context.Stack.Push(responseWindow, context);

        // Note: We don't set Resolved here because the response window will handle it asynchronously
        return false; // Return false - this is the fallback, always push the window
    }
}

