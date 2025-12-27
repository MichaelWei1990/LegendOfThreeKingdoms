using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Judgement;

/// <summary>
/// Basic implementation of the judgement service.
/// Handles drawing judgement cards from the draw pile, placing them in JudgementZone, and calculating results.
/// </summary>
public sealed class BasicJudgementService : IJudgementService
{
    private readonly IEventBus? _eventBus;

    /// <summary>
    /// Creates a new BasicJudgementService without event bus.
    /// </summary>
    public BasicJudgementService()
        : this(eventBus: null)
    {
    }

    /// <summary>
    /// Creates a new BasicJudgementService with an optional event bus.
    /// </summary>
    /// <param name="eventBus">The event bus for publishing judgement events.</param>
    public BasicJudgementService(IEventBus? eventBus)
    {
        _eventBus = eventBus;
    }

    /// <summary>
    /// Reveals a judgement card by drawing from the draw pile and placing it in JudgementZone.
    /// This is the first step of judgement execution, before modification window.
    /// </summary>
    public Card RevealJudgementCard(
        Game game,
        Player judgeOwner,
        JudgementRequest request,
        ICardMoveService cardMoveService)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        if (judgeOwner is null) throw new ArgumentNullException(nameof(judgeOwner));
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (cardMoveService is null) throw new ArgumentNullException(nameof(cardMoveService));

        // Publish JudgementStartedEvent
        if (_eventBus is not null)
        {
            var startedEvent = new JudgementStartedEvent(
                game,
                request.JudgementId,
                request.JudgeOwnerSeat,
                request.Reason);
            _eventBus.Publish(startedEvent);
        }

        // Check if draw pile has cards
        if (game.DrawPile.Cards.Count == 0)
        {
            throw new InvalidOperationException("Draw pile is empty, cannot execute judgement.");
        }

        // Engine convention: index 0 is the top of the draw pile
        // Get the card reference before moving (card will be moved by MoveSingle)
        var judgementCard = game.DrawPile.Cards[0];

        // Move the card from DrawPile to JudgementZone using ICardMoveService
        var moveDescriptor = new CardMoveDescriptor(
            SourceZone: game.DrawPile,
            TargetZone: judgeOwner.JudgementZone,
            Cards: new[] { judgementCard },
            Reason: CardMoveReason.Judgement,
            Ordering: CardMoveOrdering.ToTop,
            Game: game);

        cardMoveService.MoveSingle(moveDescriptor);

        // After moving, the card should be in JudgementZone
        // Get the card reference from JudgementZone (in case MoveSingle created a new instance)
        var movedCard = judgeOwner.JudgementZone.Cards.FirstOrDefault(c => c.Id == judgementCard.Id);
        if (movedCard is null)
        {
            throw new InvalidOperationException("Judgement card was not moved to JudgementZone successfully.");
        }
        judgementCard = movedCard;

        // Publish JudgementCardRevealedEvent to allow skills to modify the judgement card
        if (_eventBus is not null)
        {
            var revealedEvent = new JudgementCardRevealedEvent(
                game,
                request.JudgementId,
                request.JudgeOwnerSeat,
                judgementCard,
                request.Reason);
            _eventBus.Publish(revealedEvent);
        }

        return judgementCard;
    }

    /// <summary>
    /// Calculates the final judgement result based on the current judgement card.
    /// This is called after the modification window (if any).
    /// </summary>
    public JudgementResult CalculateJudgementResult(
        Game game,
        Player judgeOwner,
        JudgementRequest request,
        Card finalCard,
        IReadOnlyList<JudgementModificationRecord> modifications)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        if (judgeOwner is null) throw new ArgumentNullException(nameof(judgeOwner));
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (finalCard is null) throw new ArgumentNullException(nameof(finalCard));
        if (modifications is null) throw new ArgumentNullException(nameof(modifications));

        // Evaluate the judgement result using the final card
        var isSuccess = request.Rule.Evaluate(finalCard);
        var ruleSnapshot = request.Rule.Description;

        // Get original card from modifications or use final card
        var originalCard = modifications.Count > 0 && modifications[0].OriginalCard is not null
            ? modifications[0].OriginalCard!
            : finalCard;

        // Create result
        var result = new JudgementResult(
            JudgementId: request.JudgementId,
            JudgeOwnerSeat: request.JudgeOwnerSeat,
            OriginalCard: originalCard,
            FinalCard: finalCard,
            IsSuccess: isSuccess,
            RuleSnapshot: ruleSnapshot,
            ModifiersApplied: modifications);

        // Publish JudgementCompletedEvent
        if (_eventBus is not null)
        {
            var completedEvent = new JudgementCompletedEvent(
                game,
                request.JudgementId,
                result);
            _eventBus.Publish(completedEvent);
        }

        return result;
    }

    /// <inheritdoc />
    public JudgementResult ExecuteJudgement(
        Game game,
        Player judgeOwner,
        JudgementRequest request,
        ICardMoveService cardMoveService)
    {
        // For backward compatibility, execute judgement without modification window
        var judgementCard = RevealJudgementCard(game, judgeOwner, request, cardMoveService);
        return CalculateJudgementResult(game, judgeOwner, request, judgementCard, Array.Empty<JudgementModificationRecord>());
    }

    /// <inheritdoc />
    public void CompleteJudgement(
        Game game,
        Player judgeOwner,
        Card judgementCard,
        ICardMoveService cardMoveService)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        if (judgeOwner is null) throw new ArgumentNullException(nameof(judgeOwner));
        if (judgementCard is null) throw new ArgumentNullException(nameof(judgementCard));
        if (cardMoveService is null) throw new ArgumentNullException(nameof(cardMoveService));

        // Check if the card is still in JudgementZone
        // If the card has been moved by another skill (e.g., Tiandu), skip moving to discard pile
        if (!judgeOwner.JudgementZone.Cards.Contains(judgementCard))
        {
            // Card has been moved by another skill, skip moving to discard pile
            return;
        }

        // Move the card from JudgementZone to discard pile
        var moveDescriptor = new CardMoveDescriptor(
            SourceZone: judgeOwner.JudgementZone,
            TargetZone: game.DiscardPile,
            Cards: new[] { judgementCard },
            Reason: CardMoveReason.Discard,
            Ordering: CardMoveOrdering.ToTop,
            Game: game);

        cardMoveService.MoveSingle(moveDescriptor);
    }
}

/// <summary>
/// Resolver that executes a judgement as part of the resolution pipeline.
/// This allows judgements to be pushed onto the resolution stack.
/// Supports modification window if AllowModify is true.
/// </summary>
public sealed class JudgementResolver : IResolver
{
    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        // Extract JudgementRequest from IntermediateResults
        // The key "JudgementRequest" should be set by the caller
        if (context.IntermediateResults is null || !context.IntermediateResults.TryGetValue("JudgementRequest", out var requestObj))
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.judgement.requestNotFound",
                details: new { Message = "JudgementRequest not found in IntermediateResults" });
        }

        if (requestObj is not JudgementRequest request)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.judgement.invalidRequest",
                details: new { Message = "JudgementRequest has invalid type" });
        }

        // Get judgement service from context or create a default one
        var judgementService = context.JudgementService ?? new BasicJudgementService(context.EventBus);

        // Find the judge owner
        var judgeOwner = context.Game.Players.FirstOrDefault(p => p.Seat == request.JudgeOwnerSeat);
        if (judgeOwner is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: "resolution.judgement.playerNotFound",
                details: new { JudgeOwnerSeat = request.JudgeOwnerSeat });
        }

        // If modification is not allowed, execute judgement directly (backward compatibility)
        if (!request.AllowModify)
        {
            try
            {
                var result = judgementService.ExecuteJudgement(
                    context.Game,
                    judgeOwner,
                    request,
                    context.CardMoveService);

                // Store result in IntermediateResults for subsequent resolvers
                if (context.IntermediateResults is not null)
                {
                    context.IntermediateResults["JudgementResult"] = result;
                }

                return ResolutionResult.SuccessResult;
            }
            catch (Exception ex)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "resolution.judgement.executionFailed",
                    details: new { Exception = ex.Message });
            }
        }

        // Modification is allowed - use modification window flow
        try
        {
            // Step 1: Reveal the judgement card
            var basicService = judgementService as BasicJudgementService;
            if (basicService is null)
            {
                // If service is not BasicJudgementService, fall back to ExecuteJudgement
                var result = judgementService.ExecuteJudgement(
                    context.Game,
                    judgeOwner,
                    request,
                    context.CardMoveService);

                if (context.IntermediateResults is not null)
                {
                    context.IntermediateResults["JudgementResult"] = result;
                }

                return ResolutionResult.SuccessResult;
            }

            var originalCard = basicService.RevealJudgementCard(
                context.Game,
                judgeOwner,
                request,
                context.CardMoveService);

            // Step 2: Create JudgementContext
            var judgementContext = new JudgementContext(
                context.Game,
                judgeOwner,
                originalCard,
                request);

            // Step 3: Store JudgementContext in IntermediateResults for modification window resolver
            // Use the same dictionary reference so modifications are reflected back
            var intermediateResults = context.IntermediateResults ?? new Dictionary<string, object>();
            intermediateResults["JudgementContext"] = judgementContext;

            // Step 4: Push result calculation resolver (executes after modification window)
            var resultCalculationContext = new ResolutionContext(
                context.Game,
                judgeOwner,
                context.Action,
                context.Choice,
                context.Stack,
                context.CardMoveService,
                context.RuleService,
                context.PendingDamage,
                context.LogSink,
                context.GetPlayerChoice,
                intermediateResults,
                context.EventBus,
                context.LogCollector,
                context.SkillManager,
                context.EquipmentSkillRegistry,
                judgementService);
            context.Stack.Push(new JudgementResultCalculationResolver(), resultCalculationContext);

            // Step 5: Push modification window resolver (executes first)
            var modificationContext = new ResolutionContext(
                context.Game,
                judgeOwner,
                context.Action,
                context.Choice,
                context.Stack,
                context.CardMoveService,
                context.RuleService,
                context.PendingDamage,
                context.LogSink,
                context.GetPlayerChoice,
                intermediateResults,
                context.EventBus,
                context.LogCollector,
                context.SkillManager,
                context.EquipmentSkillRegistry,
                judgementService);
            context.Stack.Push(new JudgementModificationWindowResolver(), modificationContext);

            return ResolutionResult.SuccessResult;
        }
        catch (Exception ex)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.judgement.executionFailed",
                details: new { Exception = ex.Message });
        }
    }
}

/// <summary>
/// Resolver that calculates the final judgement result after modification window.
/// </summary>
internal sealed class JudgementResultCalculationResolver : IResolver
{
    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        // Extract JudgementContext from IntermediateResults
        if (context.IntermediateResults is null || !context.IntermediateResults.TryGetValue("JudgementContext", out var ctxObj))
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.judgement.resultCalculation.noContext",
                details: new { Message = "JudgementContext not found in IntermediateResults" });
        }

        if (ctxObj is not JudgementContext judgementContext)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.judgement.resultCalculation.invalidContext",
                details: new { Message = "JudgementContext has invalid type" });
        }

        // Get judgement service
        var judgementService = context.JudgementService ?? new BasicJudgementService(context.EventBus);
        var basicService = judgementService as BasicJudgementService;
        if (basicService is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.judgement.resultCalculation.invalidService",
                details: new { Message = "JudgementService is not BasicJudgementService" });
        }

        // Calculate final result using current judgement card
        var result = basicService.CalculateJudgementResult(
            context.Game,
            judgementContext.JudgeTarget,
            judgementContext.Request,
            judgementContext.CurrentJudgementCard,
            judgementContext.Modifications);

        // Store result in IntermediateResults for subsequent resolvers
        if (context.IntermediateResults is not null)
        {
            context.IntermediateResults["JudgementResult"] = result;
        }

        return ResolutionResult.SuccessResult;
    }
}
