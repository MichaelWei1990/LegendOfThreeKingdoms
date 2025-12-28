using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Resolver for losing HP (not from damage).
/// This is distinct from damage and should not trigger damage-related skills.
/// Used by skills like Kurou (苦肉) that lose HP as a cost.
/// </summary>
public sealed class LoseHpResolver : IResolver
{
    private readonly int _targetSeat;
    private readonly int _amount;
    private readonly object? _source;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoseHpResolver"/> class.
    /// </summary>
    /// <param name="targetSeat">The seat of the player losing HP.</param>
    /// <param name="amount">The amount of HP to lose.</param>
    /// <param name="source">The source of the HP loss (e.g., skill name).</param>
    public LoseHpResolver(int targetSeat, int amount, object? source = null)
    {
        if (amount <= 0)
            throw new ArgumentException("HP loss amount must be positive.", nameof(amount));
        
        _targetSeat = targetSeat;
        _amount = amount;
        _source = source;
    }

    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var game = context.Game;
        var target = game.Players.FirstOrDefault(p => p.Seat == _targetSeat);
        if (target is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: "resolution.loseHp.playerNotFound",
                details: new { TargetSeat = _targetSeat });
        }

        if (!target.IsAlive)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: "resolution.loseHp.playerNotAlive",
                details: new { TargetSeat = _targetSeat });
        }

        // Record previous health
        var previousHealth = target.CurrentHealth;

        // Apply HP loss
        target.CurrentHealth = Math.Max(0, target.CurrentHealth - _amount);

        // Publish HpLostEvent
        if (context.EventBus is not null)
        {
            var hpLostEvent = new HpLostEvent(
                game,
                _targetSeat,
                _amount,
                previousHealth,
                target.CurrentHealth,
                _source);
            context.EventBus.Publish(hpLostEvent);
        }

        // Log HP loss
        if (context.LogSink is not null)
        {
            var logEntry = new LogEntry
            {
                EventType = "HpLost",
                Level = "Info",
                Message = $"Player {_targetSeat} lost {_amount} HP (from {previousHealth} to {target.CurrentHealth})",
                Data = new
                {
                    TargetSeat = _targetSeat,
                    Amount = _amount,
                    PreviousHealth = previousHealth,
                    CurrentHealth = target.CurrentHealth,
                    Source = _source
                }
            };
            context.LogSink.Log(logEntry);
        }

        // Check if player is dying (health <= 0)
        if (target.CurrentHealth <= 0)
        {
            // Store dying player info in IntermediateResults
            var intermediateResults = context.IntermediateResults ?? new Dictionary<string, object>();
            intermediateResults["DyingPlayerSeat"] = _targetSeat;

            // Create handler resolver context
            var handlerContext = new ResolutionContext(
                context.Game,
                context.SourcePlayer,
                context.Action,
                context.Choice,
                context.Stack,
                context.CardMoveService,
                context.RuleService,
                PendingDamage: null, // No damage, this is HP loss
                context.LogSink,
                context.GetPlayerChoice,
                intermediateResults,
                context.EventBus,
                context.LogCollector,
                context.SkillManager,
                context.EquipmentSkillRegistry,
                context.JudgementService);

            // Push AfterHpLostHandlerResolver onto stack first (will execute after dying process due to LIFO)
            context.Stack.Push(new AfterHpLostHandlerResolver(_targetSeat, _amount, previousHealth, _source), handlerContext);

            // Push DyingResolver to handle dying process
            context.Stack.Push(new DyingResolver(), handlerContext);
        }
        else
        {
            // Player is still alive - publish AfterHpLostEvent immediately
            if (context.EventBus is not null)
            {
                var afterHpLostEvent = new AfterHpLostEvent(
                    game,
                    _targetSeat,
                    _amount,
                    previousHealth,
                    target.CurrentHealth,
                    _source);
                context.EventBus.Publish(afterHpLostEvent);
            }
        }

        return ResolutionResult.SuccessResult;
    }
}

/// <summary>
/// Resolver that handles the result of a dying rescue after HP loss.
/// Publishes AfterHpLostEvent if the player was saved.
/// </summary>
internal sealed class AfterHpLostHandlerResolver : IResolver
{
    private readonly int _targetSeat;
    private readonly int _amount;
    private readonly int _previousHealth;
    private readonly object? _source;

    public AfterHpLostHandlerResolver(int targetSeat, int amount, int previousHealth, object? source)
    {
        _targetSeat = targetSeat;
        _amount = amount;
        _previousHealth = previousHealth;
        _source = source;
    }

    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var game = context.Game;
        var target = game.Players.FirstOrDefault(p => p.Seat == _targetSeat);
        if (target is null)
        {
            return ResolutionResult.SuccessResult; // Player not found, skip
        }

        // Check if player is still alive (was rescued)
        if (target.IsAlive && target.CurrentHealth > 0)
        {
            // Player was saved - publish AfterHpLostEvent
            if (context.EventBus is not null)
            {
                var afterHpLostEvent = new AfterHpLostEvent(
                    game,
                    _targetSeat,
                    _amount,
                    _previousHealth,
                    target.CurrentHealth,
                    _source);
                context.EventBus.Publish(afterHpLostEvent);
            }
        }
        // If player is dead, AfterHpLostEvent is not published (as per requirements)

        return ResolutionResult.SuccessResult;
    }
}

