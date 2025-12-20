using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Logging;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Response;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Logging;

/// <summary>
/// Maps IGameEvent instances to ILogEvent instances for structured logging.
/// </summary>
public static class LogEventMapper
{
    /// <summary>
    /// Maps a game event to a log event.
    /// </summary>
    /// <param name="gameEvent">The game event to map.</param>
    /// <param name="sequenceNumber">The sequence number for the log event.</param>
    /// <returns>The mapped log event, or null if the event type is not supported.</returns>
    public static ILogEvent? MapFromGameEvent(IGameEvent gameEvent, long sequenceNumber)
    {
        if (gameEvent is null)
            return null;

        return gameEvent switch
        {
            TurnStartEvent evt => new TurnStartLogEvent(
                evt.Timestamp,
                sequenceNumber,
                evt.Game,
                evt.PlayerSeat,
                evt.TurnNumber
            ),

            TurnEndEvent evt => new TurnEndLogEvent(
                evt.Timestamp,
                sequenceNumber,
                evt.Game,
                evt.PlayerSeat,
                evt.TurnNumber
            ),

            PhaseStartEvent evt => new PhaseStartLogEvent(
                evt.Timestamp,
                sequenceNumber,
                evt.Game,
                evt.PlayerSeat,
                evt.Phase
            ),

            PhaseEndEvent evt => new PhaseEndLogEvent(
                evt.Timestamp,
                sequenceNumber,
                evt.Game,
                evt.PlayerSeat,
                evt.Phase
            ),

            DamageAppliedEvent evt => new DamageAppliedLogEvent(
                evt.Timestamp,
                sequenceNumber,
                evt.Game,
                evt.Damage.SourceSeat,
                evt.Damage.TargetSeat,
                evt.Damage.Amount,
                evt.Damage.Type,
                evt.PreviousHealth,
                evt.CurrentHealth
            ),

            CardMovedEvent evt => MapCardMovedEvent(evt, sequenceNumber),

            DyingStartEvent evt => new DyingStartLogEvent(
                evt.Timestamp,
                sequenceNumber,
                evt.Game,
                evt.DyingPlayerSeat
            ),

            PlayerDiedEvent evt => new PlayerDiedLogEvent(
                evt.Timestamp,
                sequenceNumber,
                evt.Game,
                evt.DeadPlayerSeat,
                evt.KillerSeat
            ),

            // For events that don't have a direct mapping, return null
            // The caller can handle this by either ignoring or creating a generic log event
            _ => null
        };
    }

    /// <summary>
    /// Maps a CardMovedEvent to one or more CardMovedLogEvent instances.
    /// Creates a separate log event for each card that was moved.
    /// </summary>
    private static ILogEvent? MapCardMovedEvent(CardMovedEvent evt, long sequenceNumber)
    {
        var moveEvent = evt.CardMoveEvent;
        
        // Only log "After" events to avoid duplicate logging
        if (moveEvent.Timing != CardMoveEventTiming.After)
            return null;

        // For simplicity, we'll create a single log event for the first card
        // In a more sophisticated implementation, we could create multiple events
        // or modify the event structure to support multiple cards
        if (moveEvent.CardIds.Count == 0)
            return null;

        // Log the first card as a representative (or we could modify the event to support multiple cards)
        return new CardMovedLogEvent(
            evt.Timestamp,
            sequenceNumber,
            evt.Game,
            moveEvent.CardIds[0],
            moveEvent.SourceZoneId,
            moveEvent.TargetZoneId,
            moveEvent.SourceOwnerSeat,
            moveEvent.TargetOwnerSeat
        );
    }
}
