using System;
using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Identity;

/// <summary>
/// Event listener that checks win conditions after player death events.
/// </summary>
public sealed class WinConditionChecker
{
    private readonly IWinConditionService _winConditionService;
    private readonly IEventBus _eventBus;

    /// <summary>
    /// Creates a new WinConditionChecker.
    /// </summary>
    /// <param name="winConditionService">The win condition service to use.</param>
    /// <param name="eventBus">The event bus to subscribe to and publish events.</param>
    public WinConditionChecker(IWinConditionService winConditionService, IEventBus eventBus)
    {
        _winConditionService = winConditionService ?? throw new ArgumentNullException(nameof(winConditionService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

        // Subscribe to PlayerDiedEvent
        _eventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
    }

    /// <summary>
    /// Handles PlayerDiedEvent by checking win conditions.
    /// </summary>
    private void OnPlayerDied(PlayerDiedEvent evt)
    {
        if (evt is null) return;

        var game = evt.Game;

        // Skip if game is already finished
        if (game.IsFinished)
        {
            return;
        }

        // Check win conditions
        var result = _winConditionService.CheckWinConditions(game);

        if (result.IsGameOver && result.WinType.HasValue && result.WinningPlayers is not null)
        {
            // Mark game as finished
            game.IsFinished = true;

            // Set winner description
            var winnerSeats = result.WinningPlayers.Select(p => p.Seat).ToList();
            var winTypeName = result.WinType.Value switch
            {
                WinType.LordAndLoyalists => "Lord and Loyalists",
                WinType.Rebels => "Rebels",
                WinType.Renegade => "Renegade",
                _ => "Unknown"
            };
            game.WinnerDescription = $"{winTypeName} won: {string.Join(", ", winnerSeats)}. {result.EndReason}";

            // Publish GameEndedEvent
            var gameEndedEvent = new GameEndedEvent(
                game,
                result.WinType.Value,
                winnerSeats,
                result.EndReason ?? "Win condition met");
            _eventBus.Publish(gameEndedEvent);
        }
    }
}

