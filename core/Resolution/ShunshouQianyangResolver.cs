using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Resolver for Shunshou Qianyang (顺手牵羊 / Steal) immediate trick card.
/// Effect: Obtain one card from a target player within distance 1.
/// </summary>
public sealed class ShunshouQianyangResolver : IResolver
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
                messageKey: "resolution.shunshouqianyang.noChoice");
        }

        // Extract target from choice
        var selectedTargetSeats = choice.SelectedTargetSeats;
        if (selectedTargetSeats is null || selectedTargetSeats.Count == 0)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: "resolution.shunshouqianyang.noTarget");
        }

        var targetSeat = selectedTargetSeats[0];
        var target = game.Players.FirstOrDefault(p => p.Seat == targetSeat);

        if (target is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: "resolution.shunshouqianyang.targetNotFound",
                details: new { TargetSeat = targetSeat });
        }

        // Check if target is alive
        if (!target.IsAlive)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.TargetNotAlive,
                messageKey: "resolution.shunshouqianyang.targetNotAlive",
                details: new { TargetSeat = targetSeat });
        }

        // Check if target is not self
        if (target.Seat == sourcePlayer.Seat)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: "resolution.shunshouqianyang.cannotStealFromSelf",
                details: new { TargetSeat = targetSeat });
        }

        // Validate distance (must be <= 1)
        try
        {
            var rangeRuleService = new RangeRuleService();
            var seatDistance = rangeRuleService.GetSeatDistance(game, sourcePlayer, target);
            if (seatDistance > 1)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidTarget,
                    messageKey: "resolution.shunshouqianyang.targetTooFar",
                    details: new { TargetSeat = target.Seat, Distance = seatDistance });
            }
        }
        catch (Exception ex)
        {
            // If distance calculation fails, return InvalidState
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.shunshouqianyang.distanceCalculationFailed",
                details: new { Exception = ex.Message });
        }

        // Collect obtainable cards from target's zones
        var obtainableCards = new List<Card>();
        if (target.HandZone.Cards is not null)
        {
            obtainableCards.AddRange(target.HandZone.Cards);
        }
        if (target.EquipmentZone.Cards is not null)
        {
            obtainableCards.AddRange(target.EquipmentZone.Cards);
        }
        if (target.JudgementZone.Cards is not null)
        {
            obtainableCards.AddRange(target.JudgementZone.Cards);
        }

        // Check if there are any obtainable cards
        if (obtainableCards.Count == 0)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.shunshouqianyang.noObtainableCards",
                details: new { TargetSeat = target.Seat });
        }

        // Request player to choose one card
        if (context.GetPlayerChoice is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.shunshouqianyang.getPlayerChoiceNotAvailable");
        }

        var choiceRequest = new ChoiceRequest(
            RequestId: Guid.NewGuid().ToString("N"),
            PlayerSeat: sourcePlayer.Seat,
            ChoiceType: ChoiceType.SelectCards,
            TargetConstraints: null,
            AllowedCards: obtainableCards,
            CanPass: false  // Must select one card
        );

        var playerChoice = context.GetPlayerChoice(choiceRequest);

        if (playerChoice is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.shunshouqianyang.playerChoiceNull");
        }

        // Validate the selected card
        var selectedCardIds = playerChoice.SelectedCardIds;
        if (selectedCardIds is null || selectedCardIds.Count == 0)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.CardNotFound,
                messageKey: "resolution.shunshouqianyang.noCardSelected");
        }

        var selectedCardId = selectedCardIds[0];
        var selectedCard = obtainableCards.FirstOrDefault(c => c.Id == selectedCardId);

        if (selectedCard is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.CardNotFound,
                messageKey: "resolution.shunshouqianyang.cardNotFound",
                details: new { CardId = selectedCardId });
        }

        // Determine the source zone of the selected card
        Model.Zones.IZone? sourceZone = null;
        if (target.HandZone.Cards?.Any(c => c.Id == selectedCardId) == true)
        {
            sourceZone = target.HandZone;
        }
        else if (target.EquipmentZone.Cards?.Any(c => c.Id == selectedCardId) == true)
        {
            sourceZone = target.EquipmentZone;
        }
        else if (target.JudgementZone.Cards?.Any(c => c.Id == selectedCardId) == true)
        {
            sourceZone = target.JudgementZone;
        }

        if (sourceZone is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.shunshouqianyang.sourceZoneNotFound",
                details: new { CardId = selectedCardId });
        }

        // Move the card to source player's hand
        try
        {
            var moveDescriptor = new CardMoveDescriptor(
                SourceZone: sourceZone,
                TargetZone: sourcePlayer.HandZone,
                Cards: new[] { selectedCard },
                Reason: CardMoveReason.Play,  // Using Play as there's no Steal reason yet
                Ordering: CardMoveOrdering.ToTop,
                Game: game
            );

            context.CardMoveService.MoveSingle(moveDescriptor);

            // Log the effect if log sink is available
            if (context.LogSink is not null)
            {
                // Use simple message logging similar to WuzhongShengyouResolver
                context.LogSink.Log(new LogEntry
                {
                    EventType = "ShunshouQianyangEffect",
                    Level = "Info",
                    Message = $"Player {sourcePlayer.Seat} obtained card {selectedCard.Id} from player {target.Seat}",
                    Data = new
                    {
                        SourcePlayerSeat = sourcePlayer.Seat,
                        TargetPlayerSeat = target.Seat,
                        CardId = selectedCard.Id,
                        CardSubType = selectedCard.CardSubType
                    }
                });
            }

            return ResolutionResult.SuccessResult;
        }
        catch (Exception ex)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.shunshouqianyang.cardMoveFailed",
                details: new { Exception = ex.Message });
        }
    }
}
