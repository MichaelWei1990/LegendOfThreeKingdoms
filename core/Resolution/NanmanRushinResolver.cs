using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Response;
using LegendOfThreeKingdoms.Core.Rules;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Resolver for Nanman Rushin (南蛮入侵) immediate trick card.
/// Effect: All alive players except the user must play a Slash, or take 1 damage.
/// Targets are processed in turn order starting from the user's next player.
/// </summary>
public sealed class NanmanRushinResolver : IResolver
{
    private const string TargetsKey = "NanmanRushinTargets";
    private const string CurrentTargetIndexKey = "NanmanRushinCurrentTargetIndex";

    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var game = context.Game;
        var sourcePlayer = context.SourcePlayer;

        // Initialize IntermediateResults if not present
        var intermediateResults = context.IntermediateResults;
        if (intermediateResults is null)
        {
            intermediateResults = new Dictionary<string, object>();
        }

        // Check if we're processing targets (continuation)
        if (intermediateResults.TryGetValue(CurrentTargetIndexKey, out var indexObj) &&
            indexObj is int currentIndex &&
            intermediateResults.TryGetValue(TargetsKey, out var targetsObj) &&
            targetsObj is IReadOnlyList<Player> targets)
        {
            // Continue processing next target
            return ProcessNextTarget(context, targets, currentIndex, intermediateResults);
        }

        // First time: initialize target list
        var allTargets = GetTargetsInTurnOrder(game, sourcePlayer);
        
        if (allTargets.Count == 0)
        {
            // No targets, nothing to do
            if (context.LogSink is not null)
            {
                var logEntry = new LogEntry
                {
                    EventType = "NanmanRushinEffect",
                    Level = "Info",
                    Message = "Nanman Rushin: No targets available",
                    Data = new { SourcePlayerSeat = sourcePlayer.Seat }
                };
                context.LogSink.Log(logEntry);
            }
            return ResolutionResult.SuccessResult;
        }

        // Store targets and start processing from index 0
        intermediateResults[TargetsKey] = allTargets;
        intermediateResults[CurrentTargetIndexKey] = 0;

        // Create new context with IntermediateResults
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
            intermediateResults,
            context.EventBus,
            context.LogCollector,
            context.SkillManager,
            context.EquipmentSkillRegistry,
            context.JudgementService
        );

        // Push self back onto stack to process first target
        context.Stack.Push(this, newContext);

        return ResolutionResult.SuccessResult;
    }

    /// <summary>
    /// Processes the next target in the list.
    /// </summary>
    private ResolutionResult ProcessNextTarget(
        ResolutionContext context,
        IReadOnlyList<Player> targets,
        int currentIndex,
        Dictionary<string, object> intermediateResults)
    {
        if (currentIndex >= targets.Count)
        {
            // All targets processed
            return ResolutionResult.SuccessResult;
        }

        var target = targets[currentIndex];
        var sourcePlayer = context.SourcePlayer;

        // Skip if target is not alive (may have died during processing)
        if (!target.IsAlive)
        {
            // Move to next target
            intermediateResults[CurrentTargetIndexKey] = currentIndex + 1;
            var nextContext = new ResolutionContext(
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
                context.LogCollector,
                context.SkillManager,
                context.EquipmentSkillRegistry,
                context.JudgementService
            );
            context.Stack.Push(this, nextContext);
            return ResolutionResult.SuccessResult;
        }

        // Create damage descriptor for this target
        var damage = new DamageDescriptor(
            SourceSeat: sourcePlayer.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "NanmanRushin"
        );

        // Create handler resolver context (will check response result and apply damage if needed)
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
            context.LogCollector,
            context.SkillManager,
            context.EquipmentSkillRegistry,
            context.JudgementService
        );

        // Push handler resolver first (will execute after response window due to LIFO)
        // The handler will also push this resolver back to process next target
        context.Stack.Push(new NanmanRushinTargetHandlerResolver(damage, targets, currentIndex), handlerContext);

        // Create response window for Slash
        // Use a unique key for this target's response result
        var responseResultKey = $"NanmanRushinResponse_{target.Seat}";
        
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
            context.LogCollector,
            context.SkillManager,
            context.EquipmentSkillRegistry,
            context.JudgementService
        );

        // Create a custom response window resolver that stores result with unique key
        var responseWindow = new NanmanRushinResponseWindowResolver(
            responseContext,
            target,
            sourcePlayer,
            responseResultKey,
            context.GetPlayerChoice);

        // Push response window last (will execute first due to LIFO)
        context.Stack.Push(responseWindow, responseContext);
        
        // Store the response result key for the handler to use
        intermediateResults[$"NanmanRushinResponseKey_{currentIndex}"] = responseResultKey;

        return ResolutionResult.SuccessResult;
    }

    /// <summary>
    /// Gets all alive players except the source, in turn order starting from source's next player.
    /// </summary>
    private static IReadOnlyList<Player> GetTargetsInTurnOrder(Game game, Player sourcePlayer)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        if (sourcePlayer is null) throw new ArgumentNullException(nameof(sourcePlayer));

        var players = game.Players;
        var total = players.Count;

        if (total == 0)
        {
            return Array.Empty<Player>();
        }

        // Find the index of the source player
        var sourceIndex = -1;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].Seat == sourcePlayer.Seat)
            {
                sourceIndex = i;
                break;
            }
        }

        if (sourceIndex < 0)
        {
            return Array.Empty<Player>();
        }

        // Collect alive players in turn order starting from source's next player
        var result = new List<Player>();
        for (int i = 1; i < total; i++) // Start from 1 (next player)
        {
            var index = (sourceIndex + i) % total;
            var player = players[index];
            if (player.IsAlive)
            {
                result.Add(player);
            }
        }

        return result;
    }
}

/// <summary>
/// Custom response window resolver for Nanman Rushin that stores result with a unique key.
/// </summary>
internal sealed class NanmanRushinResponseWindowResolver : IResolver
{
    private readonly ResponseWindowContext _windowContext;
    private readonly Func<ChoiceRequest, ChoiceResult> _getPlayerChoice;
    private readonly string _resultKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="NanmanRushinResponseWindowResolver"/> class.
    /// </summary>
    public NanmanRushinResponseWindowResolver(
        ResolutionContext context,
        Player targetPlayer,
        Player sourcePlayer,
        string resultKey,
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (targetPlayer is null) throw new ArgumentNullException(nameof(targetPlayer));
        if (sourcePlayer is null) throw new ArgumentNullException(nameof(sourcePlayer));
        if (string.IsNullOrWhiteSpace(resultKey)) throw new ArgumentException("Result key cannot be null or empty.", nameof(resultKey));
        if (getPlayerChoice is null) throw new ArgumentNullException(nameof(getPlayerChoice));

        _resultKey = resultKey;
        _getPlayerChoice = getPlayerChoice;

        // Create responder order (only the target player for Slash response)
        var responderOrder = new[] { targetPlayer };

        // Create response window context
        var responseRuleService = new Rules.ResponseRuleService();
        _windowContext = new ResponseWindowContext(
            Game: context.Game,
            ResponseType: Rules.ResponseType.SlashAgainstNanmanRushin,
            ResponderOrder: responderOrder,
            SourceEvent: new { Type = "NanmanRushin", SourceSeat = sourcePlayer.Seat, TargetSeat = targetPlayer.Seat },
            RuleService: context.RuleService,
            ResponseRuleService: responseRuleService,
            ChoiceFactory: new Rules.ChoiceRequestFactory(),
            CardMoveService: context.CardMoveService,
            LogSink: context.LogSink,
            SkillManager: context.SkillManager,
            JudgementService: context.JudgementService,
            EventBus: context.EventBus,
            IntermediateResults: context.IntermediateResults
        );
    }

    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        // Create response window and execute
        var responseWindow = new Response.BasicResponseWindow();
        var result = responseWindow.Execute(_windowContext, _getPlayerChoice);

        // Store response result in IntermediateResults dictionary with unique key
        if (context.IntermediateResults is null)
        {
            throw new InvalidOperationException(
                "IntermediateResults dictionary is required for NanmanRushinResponseWindowResolver.");
        }

        context.IntermediateResults[_resultKey] = result;

        // Convert response window result to resolution result
        return result.State switch
        {
            ResponseWindowState.NoResponse => ResolutionResult.SuccessResult,
            ResponseWindowState.ResponseSuccess => ResolutionResult.SuccessResult,
            ResponseWindowState.ResponseFailed => ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "response.window.failed"),
            _ => ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "response.window.unknownState")
        };
    }
}

/// <summary>
/// Handler resolver for a single Nanman Rushin target.
/// Checks response result and applies damage if no response was made.
/// Then continues processing next target.
/// </summary>
internal sealed class NanmanRushinTargetHandlerResolver : IResolver
{
    private readonly DamageDescriptor _pendingDamage;
    private readonly IReadOnlyList<Player> _allTargets;
    private readonly int _currentTargetIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="NanmanRushinTargetHandlerResolver"/> class.
    /// </summary>
    public NanmanRushinTargetHandlerResolver(
        DamageDescriptor pendingDamage,
        IReadOnlyList<Player> allTargets,
        int currentTargetIndex)
    {
        _pendingDamage = pendingDamage ?? throw new ArgumentNullException(nameof(pendingDamage));
        _allTargets = allTargets ?? throw new ArgumentNullException(nameof(allTargets));
        _currentTargetIndex = currentTargetIndex;
    }

    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        // Read response window result from IntermediateResults dictionary
        var intermediateResults = context.IntermediateResults;
        if (intermediateResults is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.nanmanrushin.noIntermediateResults");
        }

        // Get the response result key for this target
        var responseResultKey = $"NanmanRushinResponseKey_{_currentTargetIndex}";
        if (!intermediateResults.TryGetValue(responseResultKey, out var keyObj) || keyObj is not string resultKey)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.nanmanrushin.noResponseResultKey");
        }

        // Get the actual response result using the stored key
        if (!intermediateResults.TryGetValue(resultKey, out var resultObj))
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.nanmanrushin.noResponseResult");
        }

        if (resultObj is not ResponseWindowResult responseResult)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.nanmanrushin.invalidResponseResult");
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
                context.LogCollector,
                context.SkillManager,
                context.EquipmentSkillRegistry,
                context.JudgementService
            );

            context.Stack.Push(new DamageResolver(), damageContext);
        }
        // If response was successful, no damage is dealt

        // Continue processing next target
        var nextIndex = _currentTargetIndex + 1;
        if (nextIndex < _allTargets.Count)
        {
            intermediateResults["NanmanRushinCurrentTargetIndex"] = nextIndex;
            
            var nextContext = new ResolutionContext(
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
                context.LogCollector,
                context.SkillManager,
                context.EquipmentSkillRegistry,
                context.JudgementService
            );

            // Push NanmanRushinResolver back to process next target
            context.Stack.Push(new NanmanRushinResolver(), nextContext);
        }

        return ResolutionResult.SuccessResult;
    }
}
