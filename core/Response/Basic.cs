using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Response;

/// <summary>
/// Basic implementation of a response window.
/// Polls players in order to see if they want to respond to an event.
/// </summary>
public sealed class BasicResponseWindow : IResponseWindow
{
    /// <inheritdoc />
    public ResponseWindowResult Execute(
        ResponseWindowContext context,
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (getPlayerChoice is null) throw new ArgumentNullException(nameof(getPlayerChoice));

        var game = context.Game;
        var responseType = context.ResponseType;
        var responderOrder = context.ResponderOrder;
        var responseRuleService = context.ResponseRuleService;
        var choiceFactory = context.ChoiceFactory;
        var cardMoveService = context.CardMoveService;

        // Log response window opening
        if (context.LogSink is not null)
        {
            var logEntry = new LogEntry
            {
                EventType = "ResponseWindowOpened",
                Level = "Info",
                Message = $"Response window opened for {responseType}",
                Data = new
                {
                    ResponseType = responseType.ToString(),
                    ResponderCount = responderOrder.Count,
                    ResponderSeats = responderOrder.Select(p => p.Seat).ToArray()
                }
            };
            context.LogSink.Log(logEntry);
        }

        // Poll each player in order
        foreach (var responder in responderOrder)
        {
            var result = TryPollResponder(
                context,
                responder,
                game,
                responseType,
                responseRuleService,
                choiceFactory,
                cardMoveService,
                getPlayerChoice);

            if (result is not null)
            {
                return result;
            }
        }

        // No player responded - log and return
        if (context.LogSink is not null)
        {
            var logEntry = new LogEntry
            {
                EventType = "ResponseWindowClosed",
                Level = "Info",
                Message = $"Response window closed with no response for {responseType}",
                Data = new
                {
                    ResponseType = responseType.ToString()
                }
            };
            context.LogSink.Log(logEntry);
        }

        return new ResponseWindowResult(State: ResponseWindowState.NoResponse);
    }

    /// <summary>
    /// Attempts to poll a single responder for a response.
    /// </summary>
    /// <returns>ResponseWindowResult if the player successfully responded, null otherwise.</returns>
    private static ResponseWindowResult? TryPollResponder(
        ResponseWindowContext context,
        Player responder,
        Game game,
        ResponseType responseType,
        IResponseRuleService responseRuleService,
        IChoiceRequestFactory choiceFactory,
        ICardMoveService cardMoveService,
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice)
    {
        // Check if player is still alive (may have changed during the window)
        if (!responder.IsAlive)
        {
            return null;
        }

        // Check if player can respond
        var legalCards = GetLegalResponseCards(
            game,
            responder,
            responseType,
            context.SourceEvent,
            responseRuleService);

        if (legalCards is null)
        {
            return null;
        }

        // Get player choice
        var choice = GetPlayerChoiceForResponse(
            game,
            responder,
            responseType,
            context.SourceEvent,
            choiceFactory,
            getPlayerChoice);

        if (choice is null || choice.SelectedCardIds is null || choice.SelectedCardIds.Count == 0)
        {
            // Player chose not to respond (passed)
            LogResponsePassed(context.LogSink, responder, responseType);
            return null;
        }

        // Process player response
        return ProcessPlayerResponse(
            context,
            responder,
            game,
            responseType,
            legalCards,
            choice,
            cardMoveService);
    }

    /// <summary>
    /// Gets the legal response cards for a player.
    /// </summary>
    /// <returns>List of legal cards if player can respond, null otherwise.</returns>
    private static IReadOnlyList<Card>? GetLegalResponseCards(
        Game game,
        Player responder,
        ResponseType responseType,
        object? sourceEvent,
        IResponseRuleService responseRuleService)
    {
        var responseContext = new ResponseContext(
            game,
            responder,
            responseType,
            sourceEvent);

        // Check if player can respond
        var canRespondResult = responseRuleService.CanRespondWithCard(responseContext);
        if (!canRespondResult.IsAllowed)
        {
            return null;
        }

        // Get legal response cards
        var legalCardsResult = responseRuleService.GetLegalResponseCards(responseContext);
        if (!legalCardsResult.HasAny)
        {
            return null;
        }

        return legalCardsResult.Items;
    }

    /// <summary>
    /// Gets the player's choice for a response opportunity.
    /// </summary>
    /// <returns>The player's choice, or null if they passed.</returns>
    private static ChoiceResult? GetPlayerChoiceForResponse(
        Game game,
        Player responder,
        ResponseType responseType,
        object? sourceEvent,
        IChoiceRequestFactory choiceFactory,
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice)
    {
        var responseContext = new ResponseContext(
            game,
            responder,
            responseType,
            sourceEvent);

        var choiceRequest = choiceFactory.CreateForResponse(responseContext);
        return getPlayerChoice(choiceRequest);
    }

    /// <summary>
    /// Processes a player's response: validates the card, moves it, and logs the result.
    /// </summary>
    /// <returns>ResponseWindowResult if successful, null if validation or card move failed.</returns>
    private static ResponseWindowResult? ProcessPlayerResponse(
        ResponseWindowContext context,
        Player responder,
        Game game,
        ResponseType responseType,
        IReadOnlyList<Card> legalCards,
        ChoiceResult choice,
        ICardMoveService cardMoveService)
    {
        // Validate the selected card
        var selectedCardId = choice.SelectedCardIds![0];
        var selectedCard = legalCards.FirstOrDefault(c => c.Id == selectedCardId);

        if (selectedCard is null)
        {
            LogResponseInvalid(context.LogSink, responder, selectedCardId, responseType);
            return null;
        }

        // Move response card from hand to discard pile
        if (!TryMoveResponseCard(game, responder, selectedCard, cardMoveService, context.LogSink, selectedCardId))
        {
            return null;
        }

        // Log successful response
        LogResponseCardPlayed(context.LogSink, responder, selectedCard, responseType);

        // Response successful - return immediately (first response wins)
        return new ResponseWindowResult(
            State: ResponseWindowState.ResponseSuccess,
            Responder: responder,
            ResponseCard: selectedCard,
            Choice: choice);
    }

    /// <summary>
    /// Attempts to move the response card from hand to discard pile.
    /// </summary>
    /// <returns>True if successful, false otherwise.</returns>
    private static bool TryMoveResponseCard(
        Game game,
        Player responder,
        Card selectedCard,
        ICardMoveService cardMoveService,
        ILogSink? logSink,
        int selectedCardId)
    {
        try
        {
            var cardsToMove = new[] { selectedCard };
            cardMoveService.DiscardFromHand(game, responder, cardsToMove);
            return true;
        }
        catch (Exception ex)
        {
            // Card move failed - log and return false
            if (logSink is not null)
            {
                var logEntry = new LogEntry
                {
                    EventType = "ResponseCardMoveFailed",
                    Level = "Error",
                    Message = $"Failed to move response card: {ex.Message}",
                    Data = new
                    {
                        ResponderSeat = responder.Seat,
                        SelectedCardId = selectedCardId,
                        Exception = ex.Message
                    }
                };
                logSink.Log(logEntry);
            }
            return false;
        }
    }

    /// <summary>
    /// Logs that a player passed on a response opportunity.
    /// </summary>
    private static void LogResponsePassed(ILogSink? logSink, Player responder, ResponseType responseType)
    {
        if (logSink is null)
        {
            return;
        }

        var logEntry = new LogEntry
        {
            EventType = "ResponsePassed",
            Level = "Info",
            Message = $"Player {responder.Seat} passed on response opportunity",
            Data = new
            {
                ResponderSeat = responder.Seat,
                ResponseType = responseType.ToString()
            }
        };
        logSink.Log(logEntry);
    }

    /// <summary>
    /// Logs that a player selected an invalid response card.
    /// </summary>
    private static void LogResponseInvalid(ILogSink? logSink, Player responder, int selectedCardId, ResponseType responseType)
    {
        if (logSink is null)
        {
            return;
        }

        var logEntry = new LogEntry
        {
            EventType = "ResponseInvalid",
            Level = "Warning",
            Message = $"Player {responder.Seat} selected invalid card {selectedCardId}",
            Data = new
            {
                ResponderSeat = responder.Seat,
                SelectedCardId = selectedCardId,
                ResponseType = responseType.ToString()
            }
        };
        logSink.Log(logEntry);
    }

    /// <summary>
    /// Logs that a player successfully played a response card.
    /// </summary>
    private static void LogResponseCardPlayed(ILogSink? logSink, Player responder, Card selectedCard, ResponseType responseType)
    {
        if (logSink is null)
        {
            return;
        }

        var logEntry = new LogEntry
        {
            EventType = "ResponseCardPlayed",
            Level = "Info",
            Message = $"Player {responder.Seat} played {selectedCard.CardSubType} in response to {responseType}",
            Data = new
            {
                ResponderSeat = responder.Seat,
                ResponseType = responseType.ToString(),
                CardId = selectedCard.Id,
                CardSubType = selectedCard.CardSubType.ToString()
            }
        };
        logSink.Log(logEntry);
    }
}

/// <summary>
/// Resolver that executes a response window as part of the resolution pipeline.
/// This allows response windows to be pushed onto the resolution stack.
/// </summary>
public sealed class ResponseWindowResolver : IResolver
{
    private readonly ResponseWindowContext _windowContext;
    private readonly Func<ChoiceRequest, ChoiceResult> _getPlayerChoice;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResponseWindowResolver"/> class.
    /// </summary>
    /// <param name="windowContext">The response window context.</param>
    /// <param name="getPlayerChoice">Function to get player choice for a given choice request.</param>
    public ResponseWindowResolver(
        ResponseWindowContext windowContext,
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice)
    {
        _windowContext = windowContext ?? throw new ArgumentNullException(nameof(windowContext));
        _getPlayerChoice = getPlayerChoice ?? throw new ArgumentNullException(nameof(getPlayerChoice));
    }

    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        // Create response window and execute
        var responseWindow = new BasicResponseWindow();
        var result = responseWindow.Execute(_windowContext, _getPlayerChoice);

        // Store response result in IntermediateResults dictionary for subsequent resolvers
        // The dictionary should already exist (created by SlashResolver) and be shared across all resolvers
        if (context.IntermediateResults is null)
        {
            // This should not happen in normal flow, but we need to handle it
            // The problem is that we can't update the context (it's immutable), so subsequent resolvers won't see this
            // For now, we'll just log a warning or throw an error
            throw new InvalidOperationException(
                "IntermediateResults dictionary is required for ResponseWindowResolver. " +
                "It should be created by SlashResolver before pushing the response window.");
        }

        context.IntermediateResults["LastResponseResult"] = result;

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
