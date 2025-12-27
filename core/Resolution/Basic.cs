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

        // Get the actual card to use from IntermediateResults (set by CardConversionHelper before resolver is called)
        // If not found, fall back to legacy conversion logic for backward compatibility
        Card actualCard;
        Card? originalCard = null;
        bool isCardConversion = false;

        if (context.IntermediateResults is not null &&
            context.IntermediateResults.TryGetValue("ActualCard", out var actualCardObj) &&
            actualCardObj is Card resolvedCard)
        {
            // Conversion was already resolved by CardConversionHelper
            actualCard = resolvedCard;
            
            // Check if conversion occurred
            if (context.IntermediateResults.TryGetValue("ConversionOriginalCard", out var originalCardObj) &&
                originalCardObj is Card original)
            {
                originalCard = original;
                isCardConversion = true;
            }
        }
        else
        {
            // Legacy path: perform conversion here for backward compatibility
            // This allows existing code that creates ResolutionContext directly to still work
            actualCard = card;
            
            // Determine the expected card type from the action
            CardSubType? expectedCardSubType = action.ActionId switch
            {
                "UseGuoheChaiqiao" => CardSubType.GuoheChaiqiao,
                // Add more action-to-card mappings here as needed
                _ => null
            };

            // If action expects a specific card type and the selected card is not that type, try conversion
            if (expectedCardSubType.HasValue && 
                card.CardSubType != expectedCardSubType.Value &&
                context.SkillManager is not null)
            {
                var skills = context.SkillManager.GetActiveSkills(game, sourcePlayer)
                    .OfType<Skills.ICardConversionSkill>()
                    .ToList();
                
                foreach (var skill in skills)
                {
                    var virtualCard = skill.CreateVirtualCard(card, game, sourcePlayer);
                    if (virtualCard is not null && virtualCard.CardSubType == expectedCardSubType.Value)
                    {
                        originalCard = card;
                        actualCard = virtualCard;
                        isCardConversion = true;
                        
                        // Store original card and conversion skill in IntermediateResults for cleanup
                        if (context.IntermediateResults is null)
                        {
                            // Note: We can't modify the context as it's a record, so we'll handle cleanup differently
                            // The cleanup resolver will need to find the original card by ID
                        }
                        else
                        {
                            context.IntermediateResults["ConversionOriginalCard"] = originalCard;
                            context.IntermediateResults["ConversionSkill"] = skill;
                        }
                        break; // Use the first matching conversion skill
                    }
                }
            }
        }

        // Final validation using rule service (use actualCard for validation)
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

        // Push specific resolver based on card type (use actualCard)
        IResolver? specificResolver = actualCard.CardType switch
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

        // For card conversion, don't move the original card yet - CardConversionCleanupResolver will handle it
        // For equipment cards, don't move to discard pile yet - EquipResolver will handle it
        // For delayed tricks, don't move to discard pile yet - DelayedTrickResolver will move to judgement zone
        // For other cards, move to discard pile first
        var isDelayedTrick = actualCard.CardType == CardType.Trick &&
                            (actualCard.CardSubType == CardSubType.DelayedTrick ||
                             actualCard.CardSubType == CardSubType.Lebusishu ||
                             actualCard.CardSubType == CardSubType.Shandian);

        if (!isCardConversion && actualCard.CardType != CardType.Equip && !isDelayedTrick)
        {
            try
            {
                // Use the original card object (not actualCard) for discarding,
                // as DiscardFromHand uses object reference comparison
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
        }

        if (specificResolver is null)
        {
            // Card type not supported yet
            // For non-equipment cards, card was already moved to discard pile
            // This is acceptable for now as the card was successfully "used"
            return ResolutionResult.SuccessResult;
        }

        // Log card usage event if log collector is available (use actualCard)
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

        // Publish CardUsedEvent for skills that need to track card usage (e.g., Keji) (use actualCard)
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

        // For card conversion, update Action.CardCandidates to use the virtual card
        ActionDescriptor? updatedAction = action;
        if (isCardConversion && originalCard is not null && action.CardCandidates is not null)
        {
            var updatedCandidates = action.CardCandidates
                .Select(c => c.Id == originalCard.Id ? actualCard : c)
                .ToList();
            updatedAction = action with { CardCandidates = updatedCandidates };
        }

        // Create new context for the specific resolver (use updatedAction)
        var newContext = new ResolutionContext(
            game,
            sourcePlayer,
            updatedAction,
            choice,
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

        // For card conversion, push a resolver to move the original card to discard pile after resolution
        if (isCardConversion && originalCard is not null)
        {
            var conversionCleanupResolver = new CardConversionCleanupResolver(originalCard);
            var cleanupContext = new ResolutionContext(
                game,
                sourcePlayer,
                null,
                null,
                context.Stack,
                context.CardMoveService,
                context.RuleService,
                null,
                context.LogSink,
                context.GetPlayerChoice,
                context.IntermediateResults,
                context.EventBus,
                context.LogCollector,
                context.SkillManager,
                context.EquipmentSkillRegistry,
                context.JudgementService
            );
            context.Stack.Push(conversionCleanupResolver, cleanupContext);
        }

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
