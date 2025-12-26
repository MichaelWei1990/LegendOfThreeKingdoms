using System;
using System.Linq;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Logging;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Tricks;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Resolver for immediate trick cards.
/// Handles immediate resolution of trick cards that take effect right after being used.
/// </summary>
public sealed class ImmediateTrickResolver : IResolver
{
    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var choice = context.Choice;
        if (choice is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.immediatetrick.noChoice");
        }

        // Extract card from choice
        var selectedCardIds = choice.SelectedCardIds;
        if (selectedCardIds is null || selectedCardIds.Count == 0)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.CardNotFound,
                messageKey: "resolution.immediatetrick.noCardSelected");
        }

        var cardId = selectedCardIds[0];
        Card? card = null;

        // Try to find card from Action.CardCandidates first
        if (context.Action?.CardCandidates is not null)
        {
            card = context.Action.CardCandidates.FirstOrDefault(c => c.Id == cardId);
        }

        // If not found, try to find from source player's hand (card might have been moved to discard already)
        if (card is null && context.SourcePlayer.HandZone.Cards is not null)
        {
            card = context.SourcePlayer.HandZone.Cards.FirstOrDefault(c => c.Id == cardId);
        }

        if (card is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.CardNotFound,
                messageKey: "resolution.immediatetrick.cardNotFound",
                details: new { CardId = cardId });
        }

        // Dispatch to specific resolver based on CardSubType
        IResolver? specificResolver = card.CardSubType switch
        {
            CardSubType.WuzhongShengyou => new WuzhongShengyouResolver(),
            CardSubType.TaoyuanJieyi => new TaoyuanJieyiResolver(),
            CardSubType.ShunshouQianyang => new ShunshouQianyangResolver(),
            CardSubType.GuoheChaiqiao => new GuoheChaiqiaoResolver(),
            CardSubType.WanjianQifa => new WanjianqifaResolver(),
            CardSubType.NanmanRushin => new NanmanRushinResolver(),
            CardSubType.Duel => new DuelResolver(),
            CardSubType.ImmediateTrick => null,   // Generic immediate trick - no specific resolver yet
            _ => null
        };

        if (specificResolver is null)
        {
            // No specific resolver for this immediate trick yet
            // Card was already moved to discard pile by UseCardResolver
            // This is acceptable for now as the card was successfully "used"
            return ResolutionResult.SuccessResult;
        }

        // Create new context for the specific resolver
        var newContext = new ResolutionContext(
            context.Game,
            context.SourcePlayer,
            context.Action,
            context.Choice,
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
            context.JudgementService
        );

        // Push the specific resolver onto the stack
        context.Stack.Push(specificResolver, newContext);

        return ResolutionResult.SuccessResult;
    }
}

/// <summary>
/// Resolver for delayed trick cards.
/// Handles placement of delayed tricks into the target player's judgement zone.
/// </summary>
public sealed class DelayedTrickResolver : IResolver
{
    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var game = context.Game;
        var sourcePlayer = context.SourcePlayer;
        var choice = context.Choice;

        if (choice is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.delayedtrick.noChoice");
        }

        // Extract card from choice
        var selectedCardIds = choice.SelectedCardIds;
        if (selectedCardIds is null || selectedCardIds.Count == 0)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.CardNotFound,
                messageKey: "resolution.delayedtrick.noCardSelected");
        }

        var cardId = selectedCardIds[0];
        Card? card = null;

        // Try to find card from Action.CardCandidates first
        if (context.Action?.CardCandidates is not null)
        {
            card = context.Action.CardCandidates.FirstOrDefault(c => c.Id == cardId);
        }

        // If not found, try to find from source player's hand
        if (card is null && sourcePlayer.HandZone.Cards is not null)
        {
            card = sourcePlayer.HandZone.Cards.FirstOrDefault(c => c.Id == cardId);
        }

        if (card is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.CardNotFound,
                messageKey: "resolution.delayedtrick.cardNotFound",
                details: new { CardId = cardId });
        }

        // Extract target from choice
        var selectedTargetSeats = choice.SelectedTargetSeats;
        if (selectedTargetSeats is null || selectedTargetSeats.Count == 0)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: "resolution.delayedtrick.noTarget");
        }

        var targetSeat = selectedTargetSeats[0];
        var target = game.Players.FirstOrDefault(p => p.Seat == targetSeat);

        if (target is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: "resolution.delayedtrick.targetNotFound",
                details: new { TargetSeat = targetSeat });
        }

        // Check if target is alive
        if (!target.IsAlive)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.TargetNotAlive,
                messageKey: "resolution.delayedtrick.targetNotAlive",
                details: new { TargetSeat = targetSeat });
        }

        // Move the card from hand to target's judgement zone
        // Note: UseCardResolver does not move delayed tricks to discard pile,
        // so the card should still be in the source player's hand.
        
        try
        {
            // Find card in hand (UseCardResolver doesn't move delayed tricks to discard)
            var cardInHand = sourcePlayer.HandZone.Cards.FirstOrDefault(c => c.Id == cardId);
            if (cardInHand is null)
            {
                // Card might have been moved already, try discard pile as fallback
                cardInHand = game.DiscardPile.Cards.FirstOrDefault(c => c.Id == cardId);
            }

            if (cardInHand is null)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.CardNotFound,
                    messageKey: "resolution.delayedtrick.cardNotFoundInZones",
                    details: new { CardId = cardId });
            }

            // Determine source zone
            var sourceZone = sourcePlayer.HandZone.Cards.Contains(cardInHand)
                ? sourcePlayer.HandZone
                : game.DiscardPile;

            // Move card to target's judgement zone
            var moveDescriptor = new CardMoveDescriptor(
                SourceZone: sourceZone,
                TargetZone: target.JudgementZone,
                Cards: new[] { cardInHand },
                Reason: CardMoveReason.Judgement,
                Ordering: CardMoveOrdering.ToTop,
                Game: game);

            context.CardMoveService.MoveSingle(moveDescriptor);

            // Log delayed trick placement if log collector is available
            if (context.LogCollector is not null)
            {
                var sequenceNumber = context.LogCollector.GetNextSequenceNumber();
                var logEvent = new CardUsedLogEvent(
                    DateTime.UtcNow,
                    sequenceNumber,
                    game,
                    sourcePlayer.Seat,
                    cardInHand.Id,
                    cardInHand.CardSubType,
                    new[] { targetSeat }
                );
                context.LogCollector.Collect(logEvent);
            }

            // Publish event if available
            if (context.EventBus is not null)
            {
                var placedEvent = new Events.DelayedTrickPlacedEvent(
                    game,
                    sourcePlayer.Seat,
                    targetSeat,
                    cardInHand.Id,
                    cardInHand.CardSubType
                );
                context.EventBus.Publish(placedEvent);
            }

            return ResolutionResult.SuccessResult;
        }
        catch (Exception ex)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.delayedtrick.cardMoveFailed",
                details: new { Exception = ex.Message });
        }
    }
}
