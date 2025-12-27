using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Response;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Skills.Hero;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Resolver for Hujia (护驾) skill assistance flow.
/// Handles asking Wei faction players to assist Cao Cao by playing Dodge on his behalf.
/// </summary>
public sealed class HujiaResolver : IResolver
{
    private readonly Player _beneficiary;
    private readonly ResponseType _responseType;
    private readonly object? _sourceEvent;
    private readonly IResponseAssistanceSkill _hujiaSkill;

    /// <summary>
    /// Creates a new HujiaResolver.
    /// </summary>
    /// <param name="beneficiary">The player who needs the response (Cao Cao).</param>
    /// <param name="responseType">The type of response needed.</param>
    /// <param name="sourceEvent">The source event that triggered the response requirement.</param>
    /// <param name="hujiaSkill">The Hujia skill instance.</param>
    public HujiaResolver(
        Player beneficiary,
        ResponseType responseType,
        object? sourceEvent,
        IResponseAssistanceSkill hujiaSkill)
    {
        _beneficiary = beneficiary ?? throw new ArgumentNullException(nameof(beneficiary));
        _responseType = responseType;
        _sourceEvent = sourceEvent;
        _hujiaSkill = hujiaSkill ?? throw new ArgumentNullException(nameof(hujiaSkill));
    }

    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        var game = context.Game;
        var getPlayerChoice = context.GetPlayerChoice;
        var cardMoveService = context.CardMoveService;

        if (getPlayerChoice is null || cardMoveService is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.hujia.missingServices",
                details: new { Message = "Required services (GetPlayerChoice or CardMoveService) are missing" });
        }

        // Get list of Wei faction assistants
        var assistants = _hujiaSkill.GetAssistants(game, _beneficiary);

        if (assistants.Count == 0)
        {
            // No assistants available, fall back to normal response window
            return PushNormalResponseWindow(context);
        }

        // Try each assistant in seat order
        foreach (var assistant in assistants)
        {
            // Ask assistant if they want to help
            var assistRequest = new ChoiceRequest(
                RequestId: Guid.NewGuid().ToString(),
                PlayerSeat: assistant.Seat,
                ChoiceType: ChoiceType.Confirm,
                TargetConstraints: null,
                AllowedCards: null,
                ResponseWindowId: null,
                CanPass: true // Assistant can choose not to help
            );

            try
            {
                var assistResult = getPlayerChoice(assistRequest);
                if (assistResult?.Confirmed != true)
                {
                    // Assistant declined, try next one
                    continue;
                }
            }
            catch
            {
                // If choice fails, try next assistant
                continue;
            }

            // Assistant wants to help - create response window for them to play Dodge
            // This response window supports card conversion (like QingGuo)
            var assistantResponseContext = new ResolutionContext(
                game,
                assistant, // The assistant is the responder
                Action: null,
                Choice: null,
                context.Stack,
                cardMoveService,
                context.RuleService,
                context.PendingDamage,
                context.LogSink,
                getPlayerChoice,
                context.IntermediateResults,
                context.EventBus,
                context.LogCollector,
                context.SkillManager,
                context.EquipmentSkillRegistry,
                context.JudgementService);

            // Create response window for assistant to play Dodge
            // Note: The response window will support card conversion automatically
            // because ResponseRuleService already checks ICardConversionSkill
            var assistantResponseWindow = assistantResponseContext.CreateJinkResponseWindow(
                targetPlayer: assistant, // Assistant responds for themselves
                sourceEvent: _sourceEvent,
                getPlayerChoice: getPlayerChoice);

            // Push handler resolver first (will execute after response window due to LIFO)
            var handlerContext = new ResolutionContext(
                game,
                _beneficiary, // Beneficiary is Cao Cao
                Action: null,
                Choice: null,
                context.Stack,
                cardMoveService,
                context.RuleService,
                context.PendingDamage,
                context.LogSink,
                getPlayerChoice,
                context.IntermediateResults,
                context.EventBus,
                context.LogCollector,
                context.SkillManager,
                context.EquipmentSkillRegistry,
                context.JudgementService);

            // Push handler that checks if assistant successfully provided Dodge
            context.Stack.Push(new HujiaAssistanceHandlerResolver(_beneficiary, assistant), handlerContext);

            // Push response window for assistant (will execute first due to LIFO)
            context.Stack.Push(assistantResponseWindow, assistantResponseContext);

            // Stop after pushing resolvers for this assistant
            // If assistant provides Dodge, the handler will mark response as successful
            // If assistant fails, we'll fall back to normal response window
            return ResolutionResult.SuccessResult;
        }

        // No assistant was able to help, fall back to normal response window
        return PushNormalResponseWindow(context);
    }

    /// <summary>
    /// Pushes the normal response window for the beneficiary (Cao Cao).
    /// </summary>
    private ResolutionResult PushNormalResponseWindow(ResolutionContext context)
    {
        var game = context.Game;
        var getPlayerChoice = context.GetPlayerChoice;
        var cardMoveService = context.CardMoveService;

        if (getPlayerChoice is null || cardMoveService is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.hujia.missingServices",
                details: new { Message = "Required services are missing" });
        }

        // Create normal response window for beneficiary
        var responseContext = new ResolutionContext(
            game,
            _beneficiary,
            Action: null,
            Choice: null,
            context.Stack,
            cardMoveService,
            context.RuleService,
            context.PendingDamage,
            context.LogSink,
            getPlayerChoice,
            context.IntermediateResults,
            context.EventBus,
            context.LogCollector,
            context.SkillManager,
            context.EquipmentSkillRegistry,
            context.JudgementService);

        var responseWindow = responseContext.CreateJinkResponseWindow(
            targetPlayer: _beneficiary,
            sourceEvent: _sourceEvent,
            getPlayerChoice: getPlayerChoice);

        context.Stack.Push(responseWindow, responseContext);

        return ResolutionResult.SuccessResult;
    }
}

/// <summary>
/// Handler resolver that checks if Hujia assistance was successful.
/// If the assistant successfully provided Dodge, marks the response as satisfied.
/// Otherwise, continues to next assistant or falls back to normal response.
/// </summary>
internal sealed class HujiaAssistanceHandlerResolver : IResolver
{
    private readonly Player _beneficiary;
    private readonly Player _assistant;

    /// <summary>
    /// Creates a new HujiaAssistanceHandlerResolver.
    /// </summary>
    /// <param name="beneficiary">The player who needed the response (Cao Cao).</param>
    /// <param name="assistant">The assistant who attempted to provide the response.</param>
    public HujiaAssistanceHandlerResolver(Player beneficiary, Player assistant)
    {
        _beneficiary = beneficiary ?? throw new ArgumentNullException(nameof(beneficiary));
        _assistant = assistant ?? throw new ArgumentNullException(nameof(assistant));
    }

    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        // Check if response window result exists
        // The response window should have stored its result in IntermediateResults
        if (context.IntermediateResults is null)
        {
            // No intermediate results, assume failure
            return ResolutionResult.SuccessResult;
        }

        // Look for response window result
        // ResponseWindowResolver stores result with key "LastResponseResult"
        if (!context.IntermediateResults.TryGetValue("LastResponseResult", out var resultObj))
        {
            // Result not found, assume failure
            return ResolutionResult.SuccessResult;
        }

        if (resultObj is not ResponseWindowResult responseResult)
        {
            // Invalid result type, assume failure
            return ResolutionResult.SuccessResult;
        }

        // Check if assistant successfully provided Dodge
        if (responseResult.State == ResponseWindowState.ResponseSuccess)
        {
            // Assistant successfully provided Dodge - mark response as satisfied for beneficiary
            // Store result so SlashResponseHandlerResolver can find it
            // The key "LastResponseResult" will be used by SlashResponseHandlerResolver
            // Mark that Hujia was used successfully
            context.IntermediateResults["HujiaAssistanceUsed"] = true;
            context.IntermediateResults["HujiaAssistantSeat"] = _assistant.Seat;

            return ResolutionResult.SuccessResult;
        }

        // Assistant failed to provide Dodge
        // The HujiaResolver will have already tried the next assistant or fallen back
        return ResolutionResult.SuccessResult;
    }
}

