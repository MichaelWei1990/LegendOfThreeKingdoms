using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Response;

/// <summary>
/// Extension methods for creating and managing response windows in the resolution pipeline.
/// </summary>
public static class ResponseExtensions
{
    /// <summary>
    /// Creates a response window for a Slash event, where the target player can respond with Jink.
    /// </summary>
    /// <param name="context">The resolution context.</param>
    /// <param name="targetPlayer">The target player who can respond.</param>
    /// <param name="sourceEvent">The source event that triggered the response window (e.g., Slash event).</param>
    /// <param name="getPlayerChoice">Function to get player choice for a given choice request.</param>
    /// <param name="requiredCount">The required number of response units (default: 1).</param>
    /// <returns>The response window resolver that can be pushed onto the resolution stack.</returns>
    public static ResponseWindowResolver CreateJinkResponseWindow(
        this ResolutionContext context,
        Player targetPlayer,
        object? sourceEvent,
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice,
        int requiredCount = 1)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (targetPlayer is null) throw new ArgumentNullException(nameof(targetPlayer));
        if (getPlayerChoice is null) throw new ArgumentNullException(nameof(getPlayerChoice));

        // Create responder order (only the target player for Jink response)
        var responderOrder = new[] { targetPlayer };

        // Create response window context
        // Note: We need both IRuleService and IResponseRuleService
        // For now, we create a ResponseRuleService instance, but this should be injected in the future
        var responseRuleService = new ResponseRuleService(context.SkillManager);
        var windowContext = new ResponseWindowContext(
            Game: context.Game,
            ResponseType: ResponseType.JinkAgainstSlash,
            ResponderOrder: responderOrder,
            SourceEvent: sourceEvent,
            RuleService: context.RuleService,
            ResponseRuleService: responseRuleService,
            ChoiceFactory: new ChoiceRequestFactory(), // TODO: Should be injected
            CardMoveService: context.CardMoveService,
            LogSink: context.LogSink,
            SkillManager: context.SkillManager,
            JudgementService: context.JudgementService,
            EventBus: context.EventBus,
            IntermediateResults: context.IntermediateResults,
            RequiredResponseCount: requiredCount
        );

        return new ResponseWindowResolver(windowContext, getPlayerChoice);
    }

    /// <summary>
    /// Creates a response window with a custom responder order.
    /// </summary>
    /// <param name="context">The resolution context.</param>
    /// <param name="responseType">The type of response.</param>
    /// <param name="responderOrder">The order in which players should be polled.</param>
    /// <param name="sourceEvent">The source event that triggered the response window.</param>
    /// <param name="getPlayerChoice">Function to get player choice for a given choice request.</param>
    /// <returns>The response window resolver that can be pushed onto the resolution stack.</returns>
    public static ResponseWindowResolver CreateResponseWindow(
        this ResolutionContext context,
        ResponseType responseType,
        IReadOnlyList<Player> responderOrder,
        object? sourceEvent,
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (responderOrder is null) throw new ArgumentNullException(nameof(responderOrder));
        if (getPlayerChoice is null) throw new ArgumentNullException(nameof(getPlayerChoice));

        // Create response window context
        // Note: We need both IRuleService and IResponseRuleService
        // For now, we create a ResponseRuleService instance, but this should be injected in the future
        var responseRuleService = new ResponseRuleService(context.SkillManager);
        var windowContext = new ResponseWindowContext(
            Game: context.Game,
            ResponseType: responseType,
            ResponderOrder: responderOrder,
            SourceEvent: sourceEvent,
            RuleService: context.RuleService,
            ResponseRuleService: responseRuleService,
            ChoiceFactory: new ChoiceRequestFactory(), // TODO: Should be injected
            CardMoveService: context.CardMoveService,
            LogSink: context.LogSink,
            SkillManager: context.SkillManager,
            JudgementService: context.JudgementService,
            EventBus: context.EventBus
        );

        return new ResponseWindowResolver(windowContext, getPlayerChoice);
    }

    /// <summary>
    /// Calculates the responder order for a Jink response window.
    /// For Jink against Slash, only the target player can respond.
    /// </summary>
    /// <param name="game">The game state.</param>
    /// <param name="targetSeat">The seat of the target player.</param>
    /// <returns>The list of players in responder order.</returns>
    public static IReadOnlyList<Player> CalculateJinkResponderOrder(Game game, int targetSeat)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));

        var targetPlayer = game.Players.FirstOrDefault(p => p.Seat == targetSeat && p.IsAlive);
        if (targetPlayer is null)
        {
            return Array.Empty<Player>();
        }

        return new[] { targetPlayer };
    }

    /// <summary>
    /// Calculates the responder order for a Peach response window (for dying rescue).
    /// All alive players can respond, starting from the dying player, then in seat order.
    /// </summary>
    /// <param name="game">The game state.</param>
    /// <param name="dyingPlayerSeat">The seat of the dying player.</param>
    /// <returns>The list of players in responder order.</returns>
    public static IReadOnlyList<Player> CalculatePeachResponderOrder(Game game, int dyingPlayerSeat)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));

        // Start with the dying player, then all other alive players in seat order
        var allPlayers = game.Players.Where(p => p.IsAlive).OrderBy(p => p.Seat).ToList();
        var dyingPlayer = allPlayers.FirstOrDefault(p => p.Seat == dyingPlayerSeat);

        if (dyingPlayer is null)
        {
            // Dying player not found or not alive, return all alive players
            return allPlayers;
        }

        // Reorder: dying player first, then others
        var result = new List<Player> { dyingPlayer };
        result.AddRange(allPlayers.Where(p => p.Seat != dyingPlayerSeat));

        return result;
    }

    /// <summary>
    /// Creates a response window for a dying rescue event, where players can respond with Peach.
    /// </summary>
    /// <param name="context">The resolution context.</param>
    /// <param name="dyingPlayerSeat">The seat of the dying player.</param>
    /// <param name="sourceEvent">The source event that triggered the response window (e.g., Dying event).</param>
    /// <param name="getPlayerChoice">Function to get player choice for a given choice request.</param>
    /// <returns>The response window resolver that can be pushed onto the resolution stack.</returns>
    public static ResponseWindowResolver CreatePeachResponseWindow(
        this ResolutionContext context,
        int dyingPlayerSeat,
        object? sourceEvent,
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (getPlayerChoice is null) throw new ArgumentNullException(nameof(getPlayerChoice));

        var game = context.Game;
        var responderOrder = CalculatePeachResponderOrder(game, dyingPlayerSeat);
        
        // Create response window context
        // Note: We need both IRuleService and IResponseRuleService
        // For now, we create a ResponseRuleService instance, but this should be injected in the future
        var responseRuleService = new ResponseRuleService(context.SkillManager);
        var windowContext = new ResponseWindowContext(
            Game: game,
            ResponseType: ResponseType.PeachForDying,
            ResponderOrder: responderOrder,
            SourceEvent: sourceEvent,
            RuleService: context.RuleService,
            ResponseRuleService: responseRuleService,
            ChoiceFactory: new ChoiceRequestFactory(), // TODO: Should be injected
            CardMoveService: context.CardMoveService,
            LogSink: context.LogSink,
            SkillManager: context.SkillManager,
            JudgementService: context.JudgementService,
            EventBus: context.EventBus,
            IntermediateResults: context.IntermediateResults
        );
        
        return new ResponseWindowResolver(windowContext, getPlayerChoice);
    }
}
