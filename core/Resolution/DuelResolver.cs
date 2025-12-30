using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Response;
using LegendOfThreeKingdoms.Core.Rules;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Resolver for Duel (决斗) immediate trick card.
/// Effect: Target and source alternate playing Slash cards. The first player unable to play a Slash takes 1 damage from the other.
/// </summary>
public sealed class DuelResolver : IResolver
{
    internal const string CurrentPlayerSeatKey = "DuelCurrentPlayerSeat";
    internal const string OtherPlayerSeatKey = "DuelOtherPlayerSeat";
    private const string SourcePlayerSeatKey = "DuelSourcePlayerSeat";
    private const string TargetPlayerSeatKey = "DuelTargetPlayerSeat";

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
                messageKey: "resolution.duel.noChoice");
        }

        // Extract target from choice
        var selectedTargetSeats = choice.SelectedTargetSeats;
        if (selectedTargetSeats is null || selectedTargetSeats.Count == 0)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: "resolution.duel.noTarget");
        }

        var targetSeat = selectedTargetSeats[0];
        var targetPlayer = game.Players.FirstOrDefault(p => p.Seat == targetSeat);

        if (targetPlayer is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: "resolution.duel.targetNotFound",
                details: new { TargetSeat = targetSeat });
        }

        if (!targetPlayer.IsAlive)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.TargetNotAlive,
                messageKey: "resolution.duel.targetNotAlive",
                details: new { TargetSeat = targetSeat });
        }

        if (targetPlayer.Seat == sourcePlayer.Seat)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: "resolution.duel.cannotDuelSelf",
                details: new { TargetSeat = targetSeat });
        }

        // Initialize IntermediateResults if not present
        var intermediateResults = context.IntermediateResults;
        if (intermediateResults is null)
        {
            intermediateResults = new Dictionary<string, object>();
        }

        // Check if we're continuing the duel (continuation)
        if (intermediateResults.TryGetValue(CurrentPlayerSeatKey, out var currentSeatObj) &&
            currentSeatObj is int currentSeat &&
            intermediateResults.TryGetValue(OtherPlayerSeatKey, out var otherSeatObj) &&
            otherSeatObj is int otherSeat)
        {
            // Continue processing next round
            return ProcessNextRound(context, currentSeat, otherSeat, intermediateResults);
        }

        // First time: initialize duel state
        // Target player goes first
        intermediateResults[CurrentPlayerSeatKey] = targetPlayer.Seat;
        intermediateResults[OtherPlayerSeatKey] = sourcePlayer.Seat;
        intermediateResults[SourcePlayerSeatKey] = sourcePlayer.Seat;
        intermediateResults[TargetPlayerSeatKey] = targetPlayer.Seat;
        
        // Store the Duel card
        var duelCard = context.ExtractCausingCard();
        if (duelCard is not null)
        {
            intermediateResults["DuelCard"] = duelCard;
        }

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

        // Push self back onto stack to start the duel
        context.Stack.Push(this, newContext);

        return ResolutionResult.SuccessResult;
    }

    /// <summary>
    /// Processes the next round of the duel.
    /// </summary>
    private ResolutionResult ProcessNextRound(
        ResolutionContext context,
        int currentPlayerSeat,
        int otherPlayerSeat,
        Dictionary<string, object> intermediateResults)
    {
        var game = context.Game;
        var currentPlayer = game.Players.FirstOrDefault(p => p.Seat == currentPlayerSeat);
        var otherPlayer = game.Players.FirstOrDefault(p => p.Seat == otherPlayerSeat);

        if (currentPlayer is null || !currentPlayer.IsAlive)
        {
            // Current player is dead, end duel (no damage)
            return ResolutionResult.SuccessResult;
        }

        if (otherPlayer is null || !otherPlayer.IsAlive)
        {
            // Other player is dead, end duel (no damage)
            return ResolutionResult.SuccessResult;
        }

        // Create response window for current player to play Slash
        var responseResultKey = $"DuelResponse_{currentPlayerSeat}_{intermediateResults.Count}";
        
        // Create handler resolver context (will check response result and continue or deal damage)
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
        context.Stack.Push(new DuelResponseHandlerResolver(currentPlayerSeat, otherPlayerSeat, responseResultKey), handlerContext);

        // Calculate required Slash count (check if opposing player has Wushuang or similar skills)
        int requiredCount = 1;
        Card? duelCard = null;
        if (intermediateResults.TryGetValue("DuelCard", out var cardObj) && cardObj is Card card)
        {
            duelCard = card;
        }
        if (context.SkillManager is not null)
        {
            requiredCount = ResponseRequirementCalculator.CalculateSlashRequirementForDuel(
                context.Game,
                currentPlayer,
                otherPlayer,
                duelCard,
                context.SkillManager);
        }

        // Create response window for Slash
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
        var responseWindow = new DuelResponseWindowResolver(
            responseContext,
            currentPlayer,
            responseResultKey,
            context.GetPlayerChoice,
            requiredCount);

        // Push response window last (will execute first due to LIFO)
        context.Stack.Push(responseWindow, responseContext);

        return ResolutionResult.SuccessResult;
    }
}

/// <summary>
/// Custom response window resolver for Duel that stores result with a unique key.
/// </summary>
internal sealed class DuelResponseWindowResolver : IResolver
{
    private readonly ResponseWindowContext _windowContext;
    private readonly Func<ChoiceRequest, ChoiceResult> _getPlayerChoice;
    private readonly string _resultKey;
    private readonly int _requiredCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="DuelResponseWindowResolver"/> class.
    /// </summary>
    public DuelResponseWindowResolver(
        ResolutionContext context,
        Player responder,
        string resultKey,
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice,
        int requiredCount = 1)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (responder is null) throw new ArgumentNullException(nameof(responder));
        if (string.IsNullOrWhiteSpace(resultKey)) throw new ArgumentException("Result key cannot be null or empty.", nameof(resultKey));
        if (getPlayerChoice is null) throw new ArgumentNullException(nameof(getPlayerChoice));

        _resultKey = resultKey;
        _getPlayerChoice = getPlayerChoice;
        _requiredCount = requiredCount;

        // Create responder order (only the current player)
        var responderOrder = new[] { responder };

        // Create response window context
        var responseRuleService = new Rules.ResponseRuleService(context.SkillManager);
        _windowContext = new ResponseWindowContext(
            Game: context.Game,
            ResponseType: Rules.ResponseType.SlashAgainstDuel,
            ResponderOrder: responderOrder,
            SourceEvent: new { Type = "Duel", ResponderSeat = responder.Seat },
            RuleService: context.RuleService,
            ResponseRuleService: responseRuleService,
            ChoiceFactory: new Rules.ChoiceRequestFactory(),
            CardMoveService: context.CardMoveService,
            LogSink: context.LogSink,
            SkillManager: context.SkillManager,
            JudgementService: context.JudgementService,
            EventBus: context.EventBus,
            IntermediateResults: context.IntermediateResults,
            RequiredResponseCount: requiredCount
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
                "IntermediateResults dictionary is required for DuelResponseWindowResolver.");
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
/// Handler resolver for a single Duel round.
/// Checks response result and either continues the duel or deals damage.
/// </summary>
internal sealed class DuelResponseHandlerResolver : IResolver
{
    private readonly int _currentPlayerSeat;
    private readonly int _otherPlayerSeat;
    private readonly string _responseResultKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="DuelResponseHandlerResolver"/> class.
    /// </summary>
    public DuelResponseHandlerResolver(int currentPlayerSeat, int otherPlayerSeat, string responseResultKey)
    {
        _currentPlayerSeat = currentPlayerSeat;
        _otherPlayerSeat = otherPlayerSeat;
        _responseResultKey = responseResultKey ?? throw new ArgumentNullException(nameof(responseResultKey));
    }

    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var game = context.Game;
        var intermediateResults = context.IntermediateResults;
        if (intermediateResults is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.duel.noIntermediateResults");
        }

        // Get the response result
        if (!intermediateResults.TryGetValue(_responseResultKey, out var resultObj))
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.duel.noResponseResult");
        }

        if (resultObj is not ResponseWindowResult responseResult)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.duel.invalidResponseResult");
        }

        var currentPlayer = game.Players.FirstOrDefault(p => p.Seat == _currentPlayerSeat);
        var otherPlayer = game.Players.FirstOrDefault(p => p.Seat == _otherPlayerSeat);

        if (currentPlayer is null || otherPlayer is null)
        {
            // One of the players is dead, end duel
            return ResolutionResult.SuccessResult;
        }

        // Calculate required count to check if response was sufficient
        int requiredCount = 1;
        Card? duelCardForRequirement = null;
        if (intermediateResults.TryGetValue("DuelCard", out var cardObjForRequirement) && cardObjForRequirement is Card cardForRequirement)
        {
            duelCardForRequirement = cardForRequirement;
        }
        if (context.SkillManager is not null && otherPlayer is not null)
        {
            requiredCount = ResponseRequirementCalculator.CalculateSlashRequirementForDuel(
                game,
                currentPlayer,
                otherPlayer,
                duelCardForRequirement,
                context.SkillManager);
        }

        // Decide whether to continue or deal damage
        // Response is insufficient if state is NoResponse OR if provided units < required count
        bool responseInsufficient = responseResult.State == ResponseWindowState.NoResponse ||
            responseResult.ResponseUnitsProvided < requiredCount;

        if (responseInsufficient)
        {
            // Get the Duel card from intermediate results
            Card? duelCard = null;
            if (intermediateResults.TryGetValue("DuelCard", out var cardObj) && cardObj is Card card)
            {
                duelCard = card;
            }
            
            // Current player cannot play Slash - deal damage to current player from other player
            var damage = new DamageDescriptor(
                SourceSeat: _otherPlayerSeat,
                TargetSeat: _currentPlayerSeat,
                Amount: 1,
                Type: DamageType.Normal,
                Reason: "Duel",
                CausingCard: duelCard  // The Duel card that causes the damage
            );

            var damageContext = new ResolutionContext(
                game,
                context.SourcePlayer,
                context.Action,
                context.Choice,
                context.Stack,
                context.CardMoveService,
                context.RuleService,
                PendingDamage: damage,
                LogSink: context.LogSink,
                context.GetPlayerChoice,
                intermediateResults,
                context.EventBus,
                context.LogCollector,
                context.SkillManager,
                context.EquipmentSkillRegistry,
                context.JudgementService
            );

            context.Stack.Push(new DamageResolver(), damageContext);
        }
        else if (responseResult.State == ResponseWindowState.ResponseSuccess)
        {
            // Current player played Slash - swap players and continue
            intermediateResults[DuelResolver.CurrentPlayerSeatKey] = _otherPlayerSeat;
            intermediateResults[DuelResolver.OtherPlayerSeatKey] = _currentPlayerSeat;

            // Push DuelResolver back to process next round
            var nextContext = new ResolutionContext(
                game,
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

            context.Stack.Push(new DuelResolver(), nextContext);
        }

        return ResolutionResult.SuccessResult;
    }
}
