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
/// Resolver for Guohe Chaiqiao (过河拆桥 / Dismantle) immediate trick card.
/// Effect: Discard one card from a target player (no distance restriction).
/// </summary>
public sealed class GuoheChaiqiaoResolver : IResolver
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
                messageKey: "resolution.guohechaiqiao.noChoice");
        }

        // Extract target from choice
        var selectedTargetSeats = choice.SelectedTargetSeats;
        if (selectedTargetSeats is null || selectedTargetSeats.Count == 0)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: "resolution.guohechaiqiao.noTarget");
        }

        var targetSeat = selectedTargetSeats[0];
        var target = game.Players.FirstOrDefault(p => p.Seat == targetSeat);

        if (target is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: "resolution.guohechaiqiao.targetNotFound",
                details: new { TargetSeat = targetSeat });
        }

        // Check if target is alive
        if (!target.IsAlive)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.TargetNotAlive,
                messageKey: "resolution.guohechaiqiao.targetNotAlive",
                details: new { TargetSeat = targetSeat });
        }

        // Check if target is not self
        if (target.Seat == sourcePlayer.Seat)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: "resolution.guohechaiqiao.cannotDismantleSelf",
                details: new { TargetSeat = targetSeat });
        }

        // Note: GuoheChaiqiao has NO distance restriction (unlike ShunshouQianyang)

        // Collect discardable cards from target's zones
        var discardableCards = new List<Card>();
        if (target.HandZone.Cards is not null)
        {
            discardableCards.AddRange(target.HandZone.Cards);
        }
        if (target.EquipmentZone.Cards is not null)
        {
            discardableCards.AddRange(target.EquipmentZone.Cards);
        }
        if (target.JudgementZone.Cards is not null)
        {
            discardableCards.AddRange(target.JudgementZone.Cards);
        }

        // Check if there are any discardable cards
        if (discardableCards.Count == 0)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.guohechaiqiao.noDiscardableCards",
                details: new { TargetSeat = target.Seat });
        }

        // Request player to choose one card
        if (context.GetPlayerChoice is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.guohechaiqiao.getPlayerChoiceNotAvailable");
        }

        var choiceRequest = new ChoiceRequest(
            RequestId: Guid.NewGuid().ToString("N"),
            PlayerSeat: sourcePlayer.Seat,
            ChoiceType: ChoiceType.SelectCards,
            TargetConstraints: null,
            AllowedCards: discardableCards,
            CanPass: false  // Must select one card
        );

        var playerChoice = context.GetPlayerChoice(choiceRequest);

        if (playerChoice is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.guohechaiqiao.playerChoiceNull");
        }

        // Validate the selected card
        var selectedCardIds = playerChoice.SelectedCardIds;
        if (selectedCardIds is null || selectedCardIds.Count == 0)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.CardNotFound,
                messageKey: "resolution.guohechaiqiao.noCardSelected");
        }

        var selectedCardId = selectedCardIds[0];
        var selectedCard = discardableCards.FirstOrDefault(c => c.Id == selectedCardId);

        if (selectedCard is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.CardNotFound,
                messageKey: "resolution.guohechaiqiao.cardNotFound",
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
                messageKey: "resolution.guohechaiqiao.sourceZoneNotFound",
                details: new { CardId = selectedCardId });
        }

        // Move the card to discard pile
        try
        {
            var moveDescriptor = new CardMoveDescriptor(
                SourceZone: sourceZone,
                TargetZone: game.DiscardPile,
                Cards: new[] { selectedCard },
                Reason: CardMoveReason.Discard,
                Ordering: CardMoveOrdering.ToTop,
                Game: game
            );

            context.CardMoveService.MoveSingle(moveDescriptor);

            // Log the effect if log sink is available
            if (context.LogSink is not null)
            {
                context.LogSink.Log(new LogEntry
                {
                    EventType = "GuoheChaiqiaoEffect",
                    Level = "Info",
                    Message = $"Player {sourcePlayer.Seat} discarded card {selectedCard.Id} from player {target.Seat}",
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
                messageKey: "resolution.guohechaiqiao.cardMoveFailed",
                details: new { Exception = ex.Message });
        }
    }
}
