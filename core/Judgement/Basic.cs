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

    /// <inheritdoc />
    public JudgementResult ExecuteJudgement(
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

        // Evaluate the judgement result
        var isSuccess = request.Rule.Evaluate(judgementCard);
        var ruleSnapshot = request.Rule.Description;

        // Create result (no modifications applied in this phase)
        var result = new JudgementResult(
            JudgementId: request.JudgementId,
            JudgeOwnerSeat: request.JudgeOwnerSeat,
            OriginalCard: judgementCard,
            FinalCard: judgementCard, // No modifications in this phase
            IsSuccess: isSuccess,
            RuleSnapshot: ruleSnapshot,
            ModifiersApplied: Array.Empty<JudgementModificationRecord>());

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

        // Verify the card is in JudgementZone
        if (!judgeOwner.JudgementZone.Cards.Contains(judgementCard))
        {
            throw new InvalidOperationException($"Judgement card {judgementCard.Id} is not in player {judgeOwner.Seat}'s JudgementZone.");
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

        // Execute the judgement
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

            // TODO: If AllowModify = true, push modification window resolver (reserved for future implementation)
            // For now, we skip the modification window

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
