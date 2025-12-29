using System;
using System.Linq;
using LegendOfThreeKingdoms.Core.Logging;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Resolver for Peach card when used normally in play phase.
/// Effect: Target recovers 1 HP.
/// </summary>
public sealed class PeachResolver : IResolver
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
                messageKey: "resolution.peach.choiceRequired");
        }

        // Get target from choice
        // For Peach, target can be self or a dying character
        var targetSeats = choice.SelectedTargetSeats;
        if (targetSeats is null || targetSeats.Count == 0)
        {
            // Default to self if no target selected
            var selfTarget = sourcePlayer;
            
            // Validate target can receive recovery
            // Rule: Cannot use Peach on self if no health loss (CurrentHealth >= MaxHealth)
            if (selfTarget.CurrentHealth >= selfTarget.MaxHealth && selfTarget.CurrentHealth > 0)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidTarget,
                    messageKey: "resolution.peach.targetNotInjuredOrDying");
            }

            // Apply recovery
            ApplyRecovery(selfTarget);
            return ResolutionResult.SuccessResult;
        }

        // Get target player
        var targetSeat = targetSeats[0];
        var target = game.Players.FirstOrDefault(p => p.Seat == targetSeat);

        if (target is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: "resolution.peach.targetNotFound");
        }

        // Validate target can receive recovery based on Peach rules
        // Rule: Peach can be used on:
        // 1. Injured self (CurrentHealth < MaxHealth)
        // 2. Any character in dying state (CurrentHealth <= 0)
        // Rule: Cannot use Peach on self if no health loss (CurrentHealth >= MaxHealth)
        // The resolver validates that the selected target is either injured or in dying state.
        if (target.CurrentHealth >= target.MaxHealth && target.CurrentHealth > 0)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: "resolution.peach.targetNotInjuredOrDying");
        }

        // Apply recovery
        ApplyRecovery(target);

        return ResolutionResult.SuccessResult;
    }

    /// <summary>
    /// Applies recovery to the target player.
    /// </summary>
    private static void ApplyRecovery(Player target)
    {
        if (target is null) throw new ArgumentNullException(nameof(target));

        var previousHealth = target.CurrentHealth;
        
        // Recover 1 HP, but cap at max health
        target.CurrentHealth = Math.Min(target.CurrentHealth + 1, target.MaxHealth);
        
        var actualRecover = target.CurrentHealth - previousHealth;

        // Note: Logging would be done by the caller if LogSink is available
        // For now, we just apply the recovery
    }
}

