using System;
using System.Collections.Generic;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Response;

/// <summary>
/// Core interface for response windows in the game.
/// Response windows allow players to respond to events (e.g., playing Jink against Slash).
/// </summary>
public interface IResponseWindow
{
    /// <summary>
    /// Executes the response window, polling players in order to see if they want to respond.
    /// </summary>
    /// <param name="context">The response window context containing game state and dependencies.</param>
    /// <param name="getPlayerChoice">Function to get player choice for a given choice request. 
    /// This is provided by the upper engine layer and may block until the player makes a choice.</param>
    /// <returns>The result of the response window execution.</returns>
    ResponseWindowResult Execute(
        ResponseWindowContext context,
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice);
}

/// <summary>
/// Context object passed to response windows during execution.
/// Contains all necessary dependencies and state for response window processing.
/// </summary>
public sealed record ResponseWindowContext(
    Game Game,
    ResponseType ResponseType,
    IReadOnlyList<Player> ResponderOrder,
    object? SourceEvent,
    IRuleService RuleService,
    IResponseRuleService ResponseRuleService,
    IChoiceRequestFactory ChoiceFactory,
    ICardMoveService CardMoveService,
    ILogSink? LogSink = null
);

/// <summary>
/// Describes a response opportunity for a specific player.
/// Contains information about what cards the player can use to respond.
/// </summary>
public sealed record ResponseOpportunity(
    Player Responder,
    ResponseType ResponseType,
    IReadOnlyList<Card> LegalCards,
    ChoiceRequest ChoiceRequest
);

/// <summary>
/// Result of a response window execution.
/// </summary>
public sealed record ResponseWindowResult(
    ResponseWindowState State,
    Player? Responder = null,
    Card? ResponseCard = null,
    ChoiceResult? Choice = null
);

/// <summary>
/// State of a response window after execution.
/// </summary>
public enum ResponseWindowState
{
    /// <summary>
    /// No player responded to the window.
    /// </summary>
    NoResponse = 0,

    /// <summary>
    /// A player successfully responded.
    /// </summary>
    ResponseSuccess,

    /// <summary>
    /// Response failed (reserved for future use).
    /// </summary>
    ResponseFailed
}
