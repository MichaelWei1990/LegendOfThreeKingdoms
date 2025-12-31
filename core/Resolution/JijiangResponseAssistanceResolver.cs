using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Response;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Resolver for Jijiang (激将) response assistance skill.
/// Handles asking other Shu faction players to assist the beneficiary by playing Slash on their behalf.
/// The Slash is considered as used/played by the beneficiary (Lord), not by the assistant.
/// </summary>
public sealed class JijiangResponseAssistanceResolver : IResolver
{
    private readonly Player _beneficiary;
    private readonly ResponseType _responseType;
    private readonly object? _sourceEvent;
    private readonly IResponseAssistanceSkill _assistanceSkill;

    /// <summary>
    /// Creates a new JijiangResponseAssistanceResolver.
    /// </summary>
    /// <param name="beneficiary">The player who needs the Slash response (Lord).</param>
    /// <param name="responseType">The type of Slash response needed (SlashAgainstDuel or SlashAgainstNanmanRushin).</param>
    /// <param name="sourceEvent">The source event that triggered the response requirement.</param>
    /// <param name="assistanceSkill">The Jijiang skill instance.</param>
    public JijiangResponseAssistanceResolver(
        Player beneficiary,
        ResponseType responseType,
        object? sourceEvent,
        IResponseAssistanceSkill assistanceSkill)
    {
        _beneficiary = beneficiary ?? throw new ArgumentNullException(nameof(beneficiary));
        _responseType = responseType;
        _sourceEvent = sourceEvent;
        _assistanceSkill = assistanceSkill ?? throw new ArgumentNullException(nameof(assistanceSkill));
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
                messageKey: "resolution.jijiang.missingServices",
                details: new { Message = "Required services (GetPlayerChoice or CardMoveService) are missing" });
        }

        // Get list of assistants
        var assistants = _assistanceSkill.GetAssistants(game, _beneficiary);

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

            // Assistant wants to help - create response window for them to play Slash
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

            // Create response window for assistant to play Slash
            // Note: The response window will support card conversion automatically
            // because ResponseRuleService already checks ICardConversionSkill
            var responderOrder = new[] { assistant };
            var assistantResponseWindow = assistantResponseContext.CreateResponseWindow(
                responseType: _responseType,
                responderOrder: responderOrder,
                sourceEvent: _sourceEvent,
                getPlayerChoice: getPlayerChoice);

            // Push handler resolver first (will execute after response window due to LIFO)
            var handlerContext = new ResolutionContext(
                game,
                _beneficiary, // Beneficiary is the Lord
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

            // Push handler that checks if assistant successfully provided response
            // and creates virtual Slash card considered as used/played by beneficiary
            context.Stack.Push(new JijiangResponseAssistanceHandlerResolver(_beneficiary, assistant), handlerContext);

            // Push response window for assistant (will execute first due to LIFO)
            context.Stack.Push(assistantResponseWindow, assistantResponseContext);

            // Stop after pushing resolvers for this assistant
            // If assistant provides Slash, the handler will create virtual Slash and mark response as successful
            // If assistant fails, we'll fall back to normal response window
            return ResolutionResult.SuccessResult;
        }

        // No assistant was able to help, fall back to normal response window
        return PushNormalResponseWindow(context);
    }

    /// <summary>
    /// Pushes the normal response window for the beneficiary (Lord).
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
                messageKey: "resolution.jijiang.missingServices",
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

        var responderOrder = new[] { _beneficiary };
        var responseWindow = responseContext.CreateResponseWindow(
            responseType: _responseType,
            responderOrder: responderOrder,
            sourceEvent: _sourceEvent,
            getPlayerChoice: getPlayerChoice);

        context.Stack.Push(responseWindow, responseContext);

        return ResolutionResult.SuccessResult;
    }
}

/// <summary>
/// Handler resolver that checks if Jijiang response assistance was successful.
/// If the assistant successfully provided the Slash, creates a virtual Slash card
/// considered as used/played by the beneficiary (Lord), not by the assistant.
/// </summary>
internal sealed class JijiangResponseAssistanceHandlerResolver : IResolver
{
    private readonly Player _beneficiary;
    private readonly Player _assistant;

    /// <summary>
    /// Creates a new JijiangResponseAssistanceHandlerResolver.
    /// </summary>
    /// <param name="beneficiary">The player who needed the Slash response (Lord).</param>
    /// <param name="assistant">The assistant who attempted to provide the Slash.</param>
    public JijiangResponseAssistanceHandlerResolver(Player beneficiary, Player assistant)
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
        if (context.IntermediateResults is null)
        {
            // No intermediate results, assume failure
            return ResolutionResult.SuccessResult;
        }

        // Look for response window result
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

        // Check if assistant successfully provided Slash
        if (responseResult.State == ResponseWindowState.ResponseSuccess && responseResult.ResponseCard is not null)
        {
            // Assistant successfully provided Slash - create virtual Slash card
            // The virtual Slash is considered as used/played by the beneficiary (Lord), not by the assistant
            var assistantSlashCard = responseResult.ResponseCard;

            // Create virtual Slash card
            var virtualSlashCard = CreateVirtualSlashCard(assistantSlashCard);

            // Mark that Jijiang response assistance was used successfully
            context.IntermediateResults["JijiangResponseAssistanceUsed"] = true;
            context.IntermediateResults["JijiangAssistantSeat"] = _assistant.Seat;
            context.IntermediateResults["JijiangVirtualSlashCard"] = virtualSlashCard;
            context.IntermediateResults["JijiangMaterialCard"] = assistantSlashCard;

            // Store the virtual Slash as the response card for the beneficiary
            // This ensures that the response is considered as provided by the beneficiary
            var beneficiaryResponseResult = new ResponseWindowResult(
                State: ResponseWindowState.ResponseSuccess,
                Responder: _beneficiary, // Beneficiary is the responder
                ResponseCard: virtualSlashCard, // Virtual Slash card
                Choice: responseResult.Choice,
                ResponseUnitsProvided: responseResult.ResponseUnitsProvided);

            // Store the beneficiary's response result
            context.IntermediateResults["LastResponseResult"] = beneficiaryResponseResult;

            // Log the event if available
            if (context.LogSink is not null)
            {
                var logEntry = new LogEntry
                {
                    EventType = "JijiangResponseAssistanceSuccess",
                    Level = "Info",
                    Message = $"Player {_assistant.Seat} provided Slash for Lord {_beneficiary.Seat} via Jijiang",
                    Data = new
                    {
                        BeneficiarySeat = _beneficiary.Seat,
                        AssistantSeat = _assistant.Seat,
                        MaterialCardId = assistantSlashCard.Id,
                        VirtualCardId = virtualSlashCard.Id
                    }
                };
                context.LogSink.Log(logEntry);
            }

            return ResolutionResult.SuccessResult;
        }

        // Assistant failed to provide Slash
        // The JijiangResponseAssistanceResolver will have already tried the next assistant or fallen back
        return ResolutionResult.SuccessResult;
    }

    /// <summary>
    /// Creates a virtual Slash card from the assistant's Slash card.
    /// The virtual card is considered as used/played by the beneficiary (Lord).
    /// </summary>
    private static Card CreateVirtualSlashCard(Card materialCard)
    {
        // Use a unique negative ID for virtual cards to avoid conflicts
        var virtualCardId = -Guid.NewGuid().GetHashCode();
        if (virtualCardId > 0)
            virtualCardId = -virtualCardId;

        return new Card
        {
            Id = virtualCardId,
            DefinitionId = "slash",
            Name = "杀",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = materialCard.Suit, // Keep original suit
            Rank = materialCard.Rank   // Keep original rank
        };
    }
}

