using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Basic implementation of the resolution stack.
/// Manages resolver execution order and maintains execution history.
/// </summary>
public sealed class BasicResolutionStack : IResolutionStack
{
    private readonly Stack<(IResolver Resolver, ResolutionContext Context)> _stack = new();
    private readonly List<ResolutionRecord> _history = new();

    /// <inheritdoc />
    public void Push(IResolver resolver, ResolutionContext context)
    {
        if (resolver is null) throw new ArgumentNullException(nameof(resolver));
        if (context is null) throw new ArgumentNullException(nameof(context));

        _stack.Push((resolver, context));
    }

    /// <inheritdoc />
    public ResolutionResult Pop()
    {
        if (_stack.Count == 0)
        {
            return ResolutionResult.SuccessResult;
        }

        var (resolver, context) = _stack.Pop();

        // Create a snapshot of the context for history (shallow copy of key fields)
        var contextSnapshot = new ResolutionContext(
            context.Game,
            context.SourcePlayer,
            context.Action,
            context.Choice,
            this, // Use self as stack reference
            context.CardMoveService,
            context.RuleService,
            context.PendingDamage,
            context.LogSink
        );

        // Execute the resolver
        var result = resolver.Resolve(context);

        // Record in history
        _history.Add(new ResolutionRecord(
            resolver.GetType(),
            contextSnapshot,
            result
        ));

        return result;
    }

    /// <inheritdoc />
    public IReadOnlyList<ResolutionRecord> GetHistory()
    {
        return _history.AsReadOnly();
    }

    /// <inheritdoc />
    public bool IsEmpty => _stack.Count == 0;
}

/// <summary>
/// Generic resolver for card usage actions.
/// Handles common flow: validation, card movement, and delegation to specific resolvers.
/// </summary>
public sealed class UseCardResolver : IResolver
{
    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var game = context.Game;
        var sourcePlayer = context.SourcePlayer;
        var action = context.Action;
        var choice = context.Choice;

        if (action is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.usecard.noAction");
        }

        if (choice is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.usecard.noChoice");
        }

        // Extract card and targets from choice
        var selectedCardIds = choice.SelectedCardIds;
        if (selectedCardIds is null || selectedCardIds.Count == 0)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.CardNotFound,
                messageKey: "resolution.usecard.noCardSelected");
        }

        var cardId = selectedCardIds[0];
        var card = sourcePlayer.HandZone.Cards.FirstOrDefault(c => c.Id == cardId);
        if (card is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.CardNotFound,
                messageKey: "resolution.usecard.cardNotFound",
                details: new { CardId = cardId });
        }

        // Final validation using rule service
        var ruleContext = new RuleContext(game, sourcePlayer);
        var validationResult = context.RuleService.ValidateActionBeforeResolve(
            ruleContext,
            action,
            null); // Original request not needed for final validation

        if (!validationResult.IsAllowed)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.RuleValidationFailed,
                messageKey: validationResult.MessageKey,
                details: validationResult.Details);
        }

        // Move card from hand to discard pile
        try
        {
            var cardsToMove = new[] { card };
            context.CardMoveService.DiscardFromHand(game, sourcePlayer, cardsToMove);
        }
        catch (Exception ex)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.usecard.cardMoveFailed",
                details: new { Exception = ex.Message });
        }

        // Push specific resolver based on card type
        IResolver? specificResolver = card.CardSubType switch
        {
            CardSubType.Slash => new SlashResolver(),
            // Other card types can be added here in the future
            _ => null
        };

        if (specificResolver is null)
        {
            // Card type not supported yet, but card was already moved
            // This is acceptable for now as the card was successfully "used"
            return ResolutionResult.SuccessResult;
        }

        // Create new context for the specific resolver
        var newContext = new ResolutionContext(
            game,
            sourcePlayer,
            action,
            choice,
            context.Stack,
            context.CardMoveService,
            context.RuleService,
            context.PendingDamage,
            context.LogSink
        );

        // Push the specific resolver onto the stack
        context.Stack.Push(specificResolver, newContext);

        return ResolutionResult.SuccessResult;
    }
}

/// <summary>
/// Resolver for Slash card usage.
/// Handles the special resolution flow for Slash cards.
/// </summary>
public sealed class SlashResolver : IResolver
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
                messageKey: "resolution.slash.noChoice");
        }

        // Extract target from choice
        var selectedTargetSeats = choice.SelectedTargetSeats;
        if (selectedTargetSeats is null || selectedTargetSeats.Count == 0)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: "resolution.slash.noTarget");
        }

        var targetSeat = selectedTargetSeats[0];
        var target = game.Players.FirstOrDefault(p => p.Seat == targetSeat);

        if (target is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: "resolution.slash.targetNotFound",
                details: new { TargetSeat = targetSeat });
        }

        // Check if target is alive
        if (!target.IsAlive)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.TargetNotAlive,
                messageKey: "resolution.slash.targetNotAlive",
                details: new { TargetSeat = targetSeat });
        }

        // TODO: In future, this will trigger a response window for Jink/Dodge
        // For now, we assume the Slash hits directly
        // Damage resolution will be handled by DamageResolver (step 10)

        // Create damage descriptor
        var damage = new DamageDescriptor(
            SourceSeat: sourcePlayer.Seat,
            TargetSeat: target.Seat,
            Amount: 1,  // Basic Slash deals 1 damage
            Type: DamageType.Normal,
            Reason: "Slash"
        );

        // Create new context with pending damage
        var damageContext = new ResolutionContext(
            context.Game,
            context.SourcePlayer,
            context.Action,
            context.Choice,
            context.Stack,
            context.CardMoveService,
            context.RuleService,
            PendingDamage: damage,
            LogSink: context.LogSink
        );

        // Push DamageResolver onto the stack
        context.Stack.Push(new DamageResolver(), damageContext);

        return ResolutionResult.SuccessResult;
    }
}

/// <summary>
/// Resolver for damage resolution.
/// Handles damage application: reduces target health and records damage event.
/// </summary>
public sealed class DamageResolver : IResolver
{
    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var game = context.Game;
        var damage = context.PendingDamage;

        if (damage is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.damage.noPendingDamage");
        }

        // Validate damage descriptor
        try
        {
            damage.Validate();
        }
        catch (ArgumentException ex)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.damage.invalidDescriptor",
                details: new { Exception = ex.Message });
        }

        // Find target player
        var target = game.Players.FirstOrDefault(p => p.Seat == damage.TargetSeat);
        if (target is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: "resolution.damage.targetNotFound",
                details: new { TargetSeat = damage.TargetSeat });
        }

        // Check if target is alive
        if (!target.IsAlive)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.TargetNotAlive,
                messageKey: "resolution.damage.targetNotAlive",
                details: new { TargetSeat = damage.TargetSeat });
        }

        // Apply damage: reduce health (cannot go below 0)
        var previousHealth = target.CurrentHealth;
        target.CurrentHealth = Math.Max(0, target.CurrentHealth - damage.Amount);

        // Update alive status if health reaches 0 or below
        if (target.CurrentHealth <= 0)
        {
            target.IsAlive = false;
        }

        // Log damage event if log sink is available
        if (context.LogSink is not null)
        {
            var logEntry = new LogEntry
            {
                EventType = "DamageApplied",
                Level = "Info",
                Message = $"Player {damage.SourceSeat} dealt {damage.Amount} {damage.Type} damage to player {damage.TargetSeat}",
                Data = new
                {
                    SourceSeat = damage.SourceSeat,
                    TargetSeat = damage.TargetSeat,
                    Amount = damage.Amount,
                    Type = damage.Type.ToString(),
                    Reason = damage.Reason,
                    PreviousHealth = previousHealth,
                    CurrentHealth = target.CurrentHealth,
                    IsAlive = target.IsAlive
                }
            };
            context.LogSink.Log(logEntry);
        }

        return ResolutionResult.SuccessResult;
    }
}
