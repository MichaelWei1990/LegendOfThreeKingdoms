using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Logging;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Response;
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
            context.LogSink,
            context.GetPlayerChoice,
            context.IntermediateResults,
            context.EventBus,
            context.LogCollector
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

        // Log card usage event if log collector is available
        if (context.LogCollector is not null)
        {
            var sequenceNumber = context.LogCollector.GetNextSequenceNumber();
            var targetSeats = choice.SelectedTargetSeats?.ToList();
            var logEvent = new CardUsedLogEvent(
                DateTime.UtcNow,
                sequenceNumber,
                game,
                sourcePlayer.Seat,
                card.Id,
                card.CardSubType,
                targetSeats
            );
            context.LogCollector.Collect(logEvent);
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
            context.LogSink,
            context.GetPlayerChoice,
            context.IntermediateResults,
            context.EventBus,
            context.LogCollector
        );

        // Push the specific resolver onto the stack
        context.Stack.Push(specificResolver, newContext);

        return ResolutionResult.SuccessResult;
    }
}

/// <summary>
/// Resolver that handles the result of a Slash response window.
/// Decides whether to trigger damage based on the response result.
/// </summary>
public sealed class SlashResponseHandlerResolver : IResolver
{
    private readonly DamageDescriptor _pendingDamage;

    /// <summary>
    /// Initializes a new instance of the <see cref="SlashResponseHandlerResolver"/> class.
    /// </summary>
    /// <param name="pendingDamage">The damage descriptor to apply if no response was made.</param>
    public SlashResponseHandlerResolver(DamageDescriptor pendingDamage)
    {
        _pendingDamage = pendingDamage ?? throw new ArgumentNullException(nameof(pendingDamage));
    }

    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        // Read response window result from IntermediateResults dictionary
        var intermediateResults = context.IntermediateResults;
        if (intermediateResults is null || !intermediateResults.TryGetValue("LastResponseResult", out var resultObj))
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.slash.noResponseResult");
        }

        if (resultObj is not ResponseWindowResult responseResult)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.slash.invalidResponseResult");
        }

        // Decide whether to trigger damage based on response result
        if (responseResult.State == ResponseWindowState.NoResponse)
        {
            // No response - trigger damage
            var damageContext = new ResolutionContext(
                context.Game,
                context.SourcePlayer,
                context.Action,
                context.Choice,
                context.Stack,
                context.CardMoveService,
                context.RuleService,
                PendingDamage: _pendingDamage,
                LogSink: context.LogSink,
                context.GetPlayerChoice,
                context.IntermediateResults,
                context.EventBus,
                context.LogCollector
            );

            context.Stack.Push(new DamageResolver(), damageContext);
        }
        else if (responseResult.State == ResponseWindowState.ResponseSuccess)
        {
            // Response successful - slash dodged, no damage
            // Just return success
        }

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

        // Check if GetPlayerChoice is provided (required for response window)
        if (context.GetPlayerChoice is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.slash.getPlayerChoiceRequired");
        }

        // Create damage descriptor (will be used if no response is made)
        var damage = new DamageDescriptor(
            SourceSeat: sourcePlayer.Seat,
            TargetSeat: target.Seat,
            Amount: 1,  // Basic Slash deals 1 damage
            Type: DamageType.Normal,
            Reason: "Slash"
        );

        // Initialize IntermediateResults dictionary if not present
        // This dictionary will be shared across all resolvers in this resolution chain
        var intermediateResults = context.IntermediateResults;
        if (intermediateResults is null)
        {
            intermediateResults = new Dictionary<string, object>();
        }

        // Create new context with IntermediateResults for response window
        var responseContext = new ResolutionContext(
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
            intermediateResults,
            context.EventBus,
            context.LogCollector
        );

        // Create handler resolver context
        var handlerContext = new ResolutionContext(
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
            intermediateResults,
            context.EventBus,
            context.LogCollector
        );

        // Push SlashResponseHandlerResolver onto stack first (will execute after response window due to LIFO)
        context.Stack.Push(new SlashResponseHandlerResolver(damage), handlerContext);

        // Create response window for Jink
        var responseWindow = responseContext.CreateJinkResponseWindow(
            targetPlayer: target,
            sourceEvent: new { Type = "Slash", SourceSeat = sourcePlayer.Seat, TargetSeat = target.Seat },
            getPlayerChoice: context.GetPlayerChoice);

        // Push response window onto stack last (will execute first due to LIFO)
        context.Stack.Push(responseWindow, responseContext);

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

        // Publish DamageCreatedEvent before applying damage
        if (context.EventBus is not null)
        {
            var damageCreatedEvent = new DamageCreatedEvent(game, damage);
            context.EventBus.Publish(damageCreatedEvent);
        }

        // Apply damage: reduce health (cannot go below 0)
        var previousHealth = target.CurrentHealth;
        target.CurrentHealth = Math.Max(0, target.CurrentHealth - damage.Amount);

        // Publish DamageAppliedEvent after applying damage
        if (context.EventBus is not null)
        {
            var damageAppliedEvent = new DamageAppliedEvent(
                game,
                damage,
                previousHealth,
                target.CurrentHealth);
            context.EventBus.Publish(damageAppliedEvent);
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

        // Check if dying process should be triggered
        if (target.CurrentHealth <= 0 && damage.TriggersDying)
        {
            // Initialize IntermediateResults if not present
            var intermediateResults = context.IntermediateResults;
            if (intermediateResults is null)
            {
                intermediateResults = new Dictionary<string, object>();
            }
            
            // Store dying player info for DyingResolver
            intermediateResults["DyingPlayerSeat"] = target.Seat;
            
            // Create new context with IntermediateResults
            var dyingContext = new ResolutionContext(
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
                intermediateResults,
                context.EventBus,
                context.LogCollector
            );
            
            // Push DyingResolver onto stack
            context.Stack.Push(new DyingResolver(), dyingContext);
        }
        else
        {
            // Update alive status if health reaches 0 or below (only if not triggering dying)
            if (target.CurrentHealth <= 0)
            {
                target.IsAlive = false;
            }
        }

        return ResolutionResult.SuccessResult;
    }
}

/// <summary>
/// Resolver for dying process.
/// Handles dying state check and creates rescue response window.
/// </summary>
public sealed class DyingResolver : IResolver
{
    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        // Extract dying player info from IntermediateResults
        var intermediateResults = context.IntermediateResults;
        if (intermediateResults is null || !intermediateResults.TryGetValue("DyingPlayerSeat", out var seatObj))
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.dying.noDyingPlayer");
        }
        
        if (seatObj is not int dyingPlayerSeat)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.dying.invalidDyingPlayerSeat");
        }
        
        var game = context.Game;
        var dyingPlayer = game.Players.FirstOrDefault(p => p.Seat == dyingPlayerSeat);
        if (dyingPlayer is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: "resolution.dying.playerNotFound",
                details: new { DyingPlayerSeat = dyingPlayerSeat });
        }
        
        // Validate dying state
        if (dyingPlayer.CurrentHealth > 0 || dyingPlayer.IsAlive == false)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.dying.playerNotDying",
                details: new { DyingPlayerSeat = dyingPlayerSeat, CurrentHealth = dyingPlayer.CurrentHealth });
        }
        
        // Publish DyingStartEvent
        if (context.EventBus is not null)
        {
            var dyingStartEvent = new DyingStartEvent(game, dyingPlayerSeat);
            context.EventBus.Publish(dyingStartEvent);
        }
        
        // Log dying start event
        if (context.LogSink is not null)
        {
            var logEntry = new LogEntry
            {
                EventType = "DyingStart",
                Level = "Info",
                Message = $"Player {dyingPlayerSeat} is dying",
                Data = new { DyingPlayerSeat = dyingPlayerSeat, CurrentHealth = dyingPlayer.CurrentHealth }
            };
            context.LogSink.Log(logEntry);
        }
        
        // Check if GetPlayerChoice is provided (required for response window)
        if (context.GetPlayerChoice is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.dying.getPlayerChoiceRequired");
        }
        
        // Create handler resolver context
        var handlerContext = new ResolutionContext(
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
            intermediateResults,
            context.EventBus,
            context.LogCollector
        );
        
        // Push DyingRescueHandlerResolver onto stack first (will execute after response window due to LIFO)
        context.Stack.Push(new DyingRescueHandlerResolver(dyingPlayerSeat), handlerContext);
        
        // Create response window for Peach rescue
        var responseWindow = handlerContext.CreatePeachResponseWindow(
            dyingPlayerSeat: dyingPlayerSeat,
            sourceEvent: new { Type = "Dying", DyingPlayerSeat = dyingPlayerSeat },
            getPlayerChoice: context.GetPlayerChoice);
        
        // Push response window onto stack last (will execute first due to LIFO)
        context.Stack.Push(responseWindow, handlerContext);
        
        return ResolutionResult.SuccessResult;
    }
}

/// <summary>
/// Resolver that handles the result of a dying rescue response window.
/// Decides whether to restore health or mark player as dead based on the response result.
/// </summary>
public sealed class DyingRescueHandlerResolver : IResolver
{
    private readonly int _dyingPlayerSeat;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="DyingRescueHandlerResolver"/> class.
    /// </summary>
    /// <param name="dyingPlayerSeat">The seat of the dying player.</param>
    public DyingRescueHandlerResolver(int dyingPlayerSeat)
    {
        _dyingPlayerSeat = dyingPlayerSeat;
    }
    
    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        // Read response window result from IntermediateResults
        var intermediateResults = context.IntermediateResults;
        if (intermediateResults is null || !intermediateResults.TryGetValue("LastResponseResult", out var resultObj))
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.dying.noResponseResult");
        }
        
        if (resultObj is not ResponseWindowResult responseResult)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.dying.invalidResponseResult");
        }
        
        var game = context.Game;
        var dyingPlayer = game.Players.FirstOrDefault(p => p.Seat == _dyingPlayerSeat);
        if (dyingPlayer is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: "resolution.dying.playerNotFound",
                details: new { DyingPlayerSeat = _dyingPlayerSeat });
        }
        
        // Handle rescue result
        if (responseResult.State == ResponseWindowState.ResponseSuccess)
        {
            // Rescue successful - restore health to at least 1
            var previousHealth = dyingPlayer.CurrentHealth;
            dyingPlayer.CurrentHealth = Math.Max(1, dyingPlayer.CurrentHealth + 1);
            
            // Log rescue success
            if (context.LogSink is not null)
            {
                var logEntry = new LogEntry
                {
                    EventType = "DyingRescueSuccess",
                    Level = "Info",
                    Message = $"Player {responseResult.Responder?.Seat} rescued player {_dyingPlayerSeat}",
                    Data = new
                    {
                        DyingPlayerSeat = _dyingPlayerSeat,
                        RescuerSeat = responseResult.Responder?.Seat,
                        PreviousHealth = previousHealth,
                        CurrentHealth = dyingPlayer.CurrentHealth
                    }
                };
                context.LogSink.Log(logEntry);
            }
            
            // Check if still dying (health <= 0) - trigger another dying process
            if (dyingPlayer.CurrentHealth <= 0)
            {
                // Push DyingResolver again for continued rescue
                var dyingContext = new ResolutionContext(
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
                    intermediateResults,
                    context.EventBus,
                    context.LogCollector
                );
                
                context.Stack.Push(new DyingResolver(), dyingContext);
            }
        }
        else if (responseResult.State == ResponseWindowState.NoResponse)
        {
            // No rescue - mark as dead
            dyingPlayer.IsAlive = false;
            
            // Extract killer seat from PendingDamage if available
            int? killerSeat = context.PendingDamage?.SourceSeat;
            
            // Publish PlayerDiedEvent
            if (context.EventBus is not null)
            {
                var playerDiedEvent = new PlayerDiedEvent(game, _dyingPlayerSeat, killerSeat);
                context.EventBus.Publish(playerDiedEvent);
            }
            
            // Log death event
            if (context.LogSink is not null)
            {
                var logEntry = new LogEntry
                {
                    EventType = "PlayerDied",
                    Level = "Info",
                    Message = $"Player {_dyingPlayerSeat} died",
                    Data = new
                    {
                        DyingPlayerSeat = _dyingPlayerSeat,
                        CurrentHealth = dyingPlayer.CurrentHealth
                    }
                };
                context.LogSink.Log(logEntry);
            }
        }
        
        return ResolutionResult.SuccessResult;
    }
}
