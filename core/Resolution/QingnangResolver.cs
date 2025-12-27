using System;
using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Resolver for Qingnang (青囊) skill.
/// Effect: Discard 1 hand card, then heal a target by 1 HP.
/// </summary>
public sealed class QingnangResolver : IResolver
{
    private const int HealAmount = 1;

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
                messageKey: "Qingnang requires a choice with selected card and target");
        }

        // Get selected card
        if (choice.SelectedCardIds is null || choice.SelectedCardIds.Count == 0)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "Qingnang requires a selected card");
        }

        var cardId = choice.SelectedCardIds[0];
        var selectedCard = sourcePlayer.HandZone.Cards.FirstOrDefault(c => c.Id == cardId);
        if (selectedCard is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.CardNotFound,
                messageKey: $"Selected card {cardId} not found in player's hand");
        }

        // Get selected target
        if (choice.SelectedTargetSeats is null || choice.SelectedTargetSeats.Count == 0)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "Qingnang requires a selected target");
        }

        var targetSeat = choice.SelectedTargetSeats[0];
        var target = game.Players.FirstOrDefault(p => p.Seat == targetSeat);
        if (target is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: $"Target player at seat {targetSeat} not found");
        }

        if (!target.IsAlive)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.TargetNotAlive,
                messageKey: "Target player must be alive");
        }

        // Discard the selected card
        if (context.CardMoveService is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "CardMoveService is required for Qingnang");
        }

        try
        {
            // Discard card from hand
            context.CardMoveService.DiscardFromHand(game, sourcePlayer, new[] { selectedCard });
        }
        catch (Exception ex)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: $"Failed to discard card: {ex.Message}");
        }

        // Mark skill as used this turn
        var usageKey = $"qingnang_used_turn_{game.TurnNumber}_seat_{game.CurrentPlayerSeat}";
        sourcePlayer.Flags[usageKey] = true;

        // Heal the target
        var previousHealth = target.CurrentHealth;
        target.CurrentHealth = Math.Min(target.CurrentHealth + HealAmount, target.MaxHealth);
        var actualHealAmount = target.CurrentHealth - previousHealth;

        // Log the effect if log sink is available
        if (context.LogSink is not null && actualHealAmount > 0)
        {
            var logEntry = new LogEntry
            {
                EventType = "QingnangEffect",
                Level = "Info",
                Message = $"Qingnang: Player {sourcePlayer.Seat} healed player {target.Seat} by {actualHealAmount} HP",
                Data = new
                {
                    SourcePlayerSeat = sourcePlayer.Seat,
                    TargetPlayerSeat = target.Seat,
                    DiscardedCardId = selectedCard.Id,
                    PreviousHealth = previousHealth,
                    NewHealth = target.CurrentHealth,
                    ActualHealAmount = actualHealAmount
                }
            };
            context.LogSink.Log(logEntry);
        }

        // Publish event if available
        if (context.EventBus is not null && actualHealAmount > 0)
        {
            // TODO: Create HealthRestoredEvent if needed
            // For now, we can use existing events or just log
        }

        return ResolutionResult.SuccessResult;
    }
}
