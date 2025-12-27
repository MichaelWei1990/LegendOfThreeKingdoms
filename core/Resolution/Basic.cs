using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Logging;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Response;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Zones;
using static LegendOfThreeKingdoms.Core.Resolution.CardMoveStrategyExecutor;

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
    private readonly CardConversionStrategyExecutor _conversionExecutor;
    private readonly CardMoveStrategyExecutor _moveExecutor;
    private readonly CardConversionCleanupService _cleanupService;

    /// <summary>
    /// Creates a new instance with default strategies.
    /// </summary>
    public UseCardResolver()
        : this(new CardConversionStrategyExecutor(), new CardMoveStrategyExecutor(), new CardConversionCleanupService())
    {
    }

    /// <summary>
    /// Creates a new instance with custom strategies.
    /// </summary>
    public UseCardResolver(
        CardConversionStrategyExecutor conversionExecutor,
        CardMoveStrategyExecutor moveExecutor,
        CardConversionCleanupService cleanupService)
    {
        _conversionExecutor = conversionExecutor ?? throw new ArgumentNullException(nameof(conversionExecutor));
        _moveExecutor = moveExecutor ?? throw new ArgumentNullException(nameof(moveExecutor));
        _cleanupService = cleanupService ?? throw new ArgumentNullException(nameof(cleanupService));
    }

    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        // Step 1: Validate input and extract selected cards
        var validationResult = ValidateAndExtractInput(context, out var selectedCards);
        if (validationResult is not null)
            return validationResult;

        // Step 2: Perform card conversion
        var conversionResult = PerformCardConversion(context, selectedCards);
        var actualCard = conversionResult.ActualCard;
        var intermediateResults = conversionResult.UpdatedIntermediateResults ?? context.IntermediateResults;

        // Step 3: Validate action using rule service
        var ruleValidationResult = ValidateAction(context, actualCard);
        if (ruleValidationResult is not null)
            return ruleValidationResult;

        // Step 4: Create specific resolver based on card type
        var specificResolver = CreateSpecificResolver(actualCard);

        // Step 5: Move cards if needed (before resolution)
        var moveResult = MoveCardsIfNeeded(context, conversionResult, actualCard, selectedCards);
        if (!moveResult.Success)
            return moveResult;

        // Step 6: Handle case where no specific resolver exists
        if (specificResolver is null)
            return ResolutionResult.SuccessResult;

        // Step 7: Log card usage and publish events
        LogAndPublishCardUsage(context, actualCard);

        // Step 8: Update Action.CardCandidates for card conversion
        var updatedAction = UpdateActionCandidates(context.Action, conversionResult, actualCard);

        // Step 9: Push resolvers to stack
        PushResolversToStack(context, specificResolver, updatedAction, intermediateResults, conversionResult, actualCard);

        return ResolutionResult.SuccessResult;
    }

    /// <summary>
    /// Validates input context and extracts selected cards from choice.
    /// </summary>
    private static ResolutionResult? ValidateAndExtractInput(
        ResolutionContext context,
        out List<Card> selectedCards)
    {
        selectedCards = new List<Card>();

        var action = context.Action;
        var choice = context.Choice;
        var sourcePlayer = context.SourcePlayer;

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

        var selectedCardIds = choice.SelectedCardIds;
        if (selectedCardIds is null || selectedCardIds.Count == 0)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.CardNotFound,
                messageKey: "resolution.usecard.noCardSelected");
        }

        // Extract selected cards from hand (for multi-card conversion skills like Serpent Spear)
        var handCards = sourcePlayer.HandZone.Cards?.ToList() ?? new List<Card>();
        selectedCards = selectedCardIds
            .Select(id => handCards.FirstOrDefault(c => c.Id == id))
            .Where(c => c is not null)
            .Cast<Card>()
            .ToList();

        if (selectedCards.Count != selectedCardIds.Count)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.CardNotFound,
                messageKey: "resolution.usecard.cardNotFound",
                details: new { SelectedCardIds = selectedCardIds });
        }

        return null; // Success
    }

    /// <summary>
    /// Performs card conversion using the conversion strategy executor.
    /// </summary>
    private CardConversionResult PerformCardConversion(
        ResolutionContext context,
        List<Card> selectedCards)
    {
        return _conversionExecutor.Execute(context, selectedCards, context.Action!);
    }

    /// <summary>
    /// Validates the action using the rule service.
    /// </summary>
    private static ResolutionResult? ValidateAction(ResolutionContext context, Card actualCard)
    {
        var game = context.Game;
        var sourcePlayer = context.SourcePlayer;
        var action = context.Action!;

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

        return null; // Success
    }

    /// <summary>
    /// Creates a specific resolver based on the card type.
    /// </summary>
    private static IResolver? CreateSpecificResolver(Card actualCard)
    {
        return actualCard.CardType switch
        {
            CardType.Equip => new EquipResolver(),
            CardType.Trick => actualCard.CardSubType switch
            {
                CardSubType.ImmediateTrick => new ImmediateTrickResolver(),
                CardSubType.DelayedTrick => new DelayedTrickResolver(),
                CardSubType.WuzhongShengyou => new ImmediateTrickResolver(),
                CardSubType.TaoyuanJieyi => new ImmediateTrickResolver(),
                CardSubType.ShunshouQianyang => new ImmediateTrickResolver(),
                CardSubType.GuoheChaiqiao => new ImmediateTrickResolver(),
                CardSubType.WanjianQifa => new ImmediateTrickResolver(),
                CardSubType.NanmanRushin => new ImmediateTrickResolver(),
                CardSubType.Duel => new ImmediateTrickResolver(),
                CardSubType.Lebusishu => new DelayedTrickResolver(),
                CardSubType.Shandian => new DelayedTrickResolver(),
                _ => null
            },
            _ => actualCard.CardSubType switch
            {
                CardSubType.Slash => new SlashResolver(),
                // Other card types can be added here in the future
                _ => null
            }
        };
    }

    /// <summary>
    /// Moves cards if needed based on the move strategy.
    /// </summary>
    private ResolutionResult MoveCardsIfNeeded(
        ResolutionContext context,
        CardConversionResult conversionResult,
        Card actualCard,
        List<Card> selectedCards)
    {
        var game = context.Game;
        var sourcePlayer = context.SourcePlayer;

        // Determine if this is a delayed trick
        var isDelayedTrick = IsDelayedTrick(actualCard);

        // Use strategy pattern to determine card movement
        var moveResult = _moveExecutor.Execute(conversionResult, actualCard, isDelayedTrick, selectedCards);

        // Move cards before resolution if needed
        if (moveResult.ShouldMove && moveResult.MoveBeforeResolution && moveResult.CardsToMove is not null)
        {
            try
            {
                // Note: We move the original card(s), not the virtual card
                // as DiscardFromHand uses object reference comparison
                context.CardMoveService.DiscardFromHand(game, sourcePlayer, moveResult.CardsToMove.ToList());
            }
            catch (Exception ex)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "resolution.usecard.cardMoveFailed",
                    details: new { Exception = ex.Message });
            }
        }

        return ResolutionResult.SuccessResult;
    }

    /// <summary>
    /// Determines if a card is a delayed trick.
    /// </summary>
    private static bool IsDelayedTrick(Card card)
    {
        return card.CardType == CardType.Trick &&
               (card.CardSubType == CardSubType.DelayedTrick ||
                card.CardSubType == CardSubType.Lebusishu ||
                card.CardSubType == CardSubType.Shandian);
    }

    /// <summary>
    /// Logs card usage and publishes card used events.
    /// </summary>
    private static void LogAndPublishCardUsage(ResolutionContext context, Card actualCard)
    {
        var game = context.Game;
        var sourcePlayer = context.SourcePlayer;
        var choice = context.Choice!;

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
                actualCard.Id,
                actualCard.CardSubType,
                targetSeats
            );
            context.LogCollector.Collect(logEvent);
        }

        // Publish CardUsedEvent for skills that need to track card usage (e.g., Keji)
        if (context.EventBus is not null)
        {
            var cardUsedEvent = new CardUsedEvent(
                game,
                sourcePlayer.Seat,
                actualCard.Id,
                actualCard.CardSubType
            );
            context.EventBus.Publish(cardUsedEvent);
        }
    }

    /// <summary>
    /// Updates Action.CardCandidates for card conversion scenarios.
    /// </summary>
    private static ActionDescriptor? UpdateActionCandidates(
        ActionDescriptor? action,
        CardConversionResult conversionResult,
        Card actualCard)
    {
        if (!conversionResult.IsConversion || action?.CardCandidates is null)
            return action;

        var updatedCandidates = action.CardCandidates.ToList();

        // Update single-card conversion
        if (conversionResult.OriginalCard is not null)
        {
            for (int i = 0; i < updatedCandidates.Count; i++)
            {
                if (updatedCandidates[i].Id == conversionResult.OriginalCard.Id)
                {
                    updatedCandidates[i] = actualCard;
                    break;
                }
            }
        }

        // Update multi-card conversion (replace all original cards with virtual card)
        if (conversionResult.OriginalCards is not null)
        {
            var originalCardIds = conversionResult.OriginalCards.Select(c => c.Id).ToHashSet();
            for (int i = 0; i < updatedCandidates.Count; i++)
            {
                if (originalCardIds.Contains(updatedCandidates[i].Id))
                {
                    updatedCandidates[i] = actualCard;
                }
            }
        }

        return action with { CardCandidates = updatedCandidates };
    }

    /// <summary>
    /// Pushes resolvers to the resolution stack.
    /// </summary>
    private void PushResolversToStack(
        ResolutionContext context,
        IResolver specificResolver,
        ActionDescriptor? updatedAction,
        Dictionary<string, object>? intermediateResults,
        CardConversionResult conversionResult,
        Card actualCard)
    {
        var game = context.Game;
        var sourcePlayer = context.SourcePlayer;
        var choice = context.Choice!;

        // Create new context for the specific resolver
        var newContext = CreateResolverContext(
            context,
            updatedAction,
            choice,
            intermediateResults);

        // Push the specific resolver onto the stack
        context.Stack.Push(specificResolver, newContext);

        // Push cleanup resolver if needed (for equipment cards or other special cases)
        var cleanupResolver = _cleanupService.CreateCleanupResolver(conversionResult);
        if (cleanupResolver is not null && _cleanupService.NeedsCleanup(conversionResult, actualCard.CardType))
        {
            var cleanupContext = CreateCleanupContext(context, intermediateResults);
            context.Stack.Push(cleanupResolver, cleanupContext);
        }
    }

    /// <summary>
    /// Creates a resolution context for the specific resolver.
    /// </summary>
    private static ResolutionContext CreateResolverContext(
        ResolutionContext originalContext,
        ActionDescriptor? action,
        ChoiceResult choice,
        Dictionary<string, object>? intermediateResults)
    {
        return new ResolutionContext(
            originalContext.Game,
            originalContext.SourcePlayer,
            action,
            choice,
            originalContext.Stack,
            originalContext.CardMoveService,
            originalContext.RuleService,
            originalContext.PendingDamage,
            originalContext.LogSink,
            originalContext.GetPlayerChoice,
            intermediateResults,
            originalContext.EventBus,
            originalContext.LogCollector,
            originalContext.SkillManager,
            originalContext.EquipmentSkillRegistry,
            originalContext.JudgementService
        );
    }

    /// <summary>
    /// Creates a resolution context for the cleanup resolver.
    /// </summary>
    private static ResolutionContext CreateCleanupContext(
        ResolutionContext originalContext,
        Dictionary<string, object>? intermediateResults)
    {
        return new ResolutionContext(
            originalContext.Game,
            originalContext.SourcePlayer,
            null,
            null,
            originalContext.Stack,
            originalContext.CardMoveService,
            originalContext.RuleService,
            null,
            originalContext.LogSink,
            originalContext.GetPlayerChoice,
            intermediateResults,
            originalContext.EventBus,
            originalContext.LogCollector,
            originalContext.SkillManager,
            originalContext.EquipmentSkillRegistry,
            originalContext.JudgementService
        );
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

        // Publish BeforeDamageEvent to allow skills to prevent or modify damage
        bool isPrevented = false;
        if (context.EventBus is not null)
        {
            var beforeDamageEvent = new BeforeDamageEvent(game, damage);
            context.EventBus.Publish(beforeDamageEvent);
            isPrevented = beforeDamageEvent.IsPrevented;
        }

        // Publish DamageCreatedEvent before applying damage
        if (context.EventBus is not null)
        {
            var damageCreatedEvent = new DamageCreatedEvent(game, damage);
            context.EventBus.Publish(damageCreatedEvent);
        }

        // Apply damage: reduce health (cannot go below 0)
        // If damage was prevented, amount becomes 0
        var previousHealth = target.CurrentHealth;
        var actualDamageAmount = isPrevented ? 0 : damage.Amount;
        target.CurrentHealth = Math.Max(0, target.CurrentHealth - actualDamageAmount);

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

        // Publish DamageResolvedEvent after damage is fully resolved
        // This event is used by skills that need to react to completed damage (e.g., Jianxiong)
        if (context.EventBus is not null)
        {
            var damageResolvedEvent = new DamageResolvedEvent(
                game,
                damage,
                previousHealth,
                target.CurrentHealth);
            context.EventBus.Publish(damageResolvedEvent);
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
            
            // If target is still alive after damage (no dying triggered), publish AfterDamageEvent
            if (target.IsAlive && context.EventBus is not null)
            {
                var afterDamageEvent = new AfterDamageEvent(
                    game,
                    damage,
                    previousHealth,
                    target.CurrentHealth);
                context.EventBus.Publish(afterDamageEvent);
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
            else
            {
                // Player was saved and is no longer dying - publish AfterDamageEvent
                if (dyingPlayer.IsAlive && context.PendingDamage is not null && context.EventBus is not null)
                {
                    var afterDamageEvent = new AfterDamageEvent(
                        game,
                        context.PendingDamage,
                        previousHealth,
                        dyingPlayer.CurrentHealth);
                    context.EventBus.Publish(afterDamageEvent);
                }
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

/// <summary>
/// Resolver for cleaning up after card conversion skills.
/// Moves the original card to discard pile after the converted card is resolved.
/// </summary>
internal sealed class CardConversionCleanupResolver : IResolver
{
    private readonly Card _originalCard;

    /// <summary>
    /// Creates a new CardConversionCleanupResolver.
    /// </summary>
    /// <param name="originalCard">The original card that was converted.</param>
    public CardConversionCleanupResolver(Card originalCard)
    {
        _originalCard = originalCard ?? throw new ArgumentNullException(nameof(originalCard));
    }

    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var game = context.Game;
        var sourcePlayer = context.SourcePlayer;

        // Find the original card in the source player's hand
        var cardInHand = sourcePlayer.HandZone.Cards?.FirstOrDefault(c => c.Id == _originalCard.Id);
        if (cardInHand is null)
        {
            // Card might have already been moved (shouldn't happen, but handle gracefully)
            return ResolutionResult.SuccessResult;
        }

        try
        {
            // Move the original card to discard pile
            var cardsToMove = new[] { cardInHand };
            context.CardMoveService.DiscardFromHand(game, sourcePlayer, cardsToMove);

            // Log the cleanup if log sink is available
            if (context.LogSink is not null)
            {
                var logEntry = new LogEntry
                {
                    EventType = "CardConversionCleanup",
                    Level = "Info",
                    Message = $"Player {sourcePlayer.Seat} discarded original card {_originalCard.Id} after card conversion",
                    Data = new
                    {
                        SourcePlayerSeat = sourcePlayer.Seat,
                        CardId = _originalCard.Id,
                        CardSubType = _originalCard.CardSubType
                    }
                };
                context.LogSink.Log(logEntry);
            }

            return ResolutionResult.SuccessResult;
        }
        catch (Exception ex)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.cardconversioncleanup.cardMoveFailed",
                details: new { Exception = ex.Message });
        }
    }
}
