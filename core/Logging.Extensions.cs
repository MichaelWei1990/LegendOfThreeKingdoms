using System;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Logging;

namespace LegendOfThreeKingdoms.Core.Logging;

/// <summary>
/// Extension methods for integrating logging with the event bus.
/// </summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Subscribes the event bus to automatically collect log events.
    /// All published IGameEvent instances will be mapped to ILogEvent and collected.
    /// </summary>
    /// <param name="eventBus">The event bus to subscribe to.</param>
    /// <param name="logCollector">The log collector to collect events into.</param>
    /// <returns>The event bus instance for method chaining.</returns>
    public static IEventBus SubscribeToLogCollector(this IEventBus eventBus, ILogCollector logCollector)
    {
        if (eventBus is null)
            throw new ArgumentNullException(nameof(eventBus));
        if (logCollector is null)
            throw new ArgumentNullException(nameof(logCollector));

        // Subscribe to each specific event type
        // Since IEventBus.Subscribe<T> requires a concrete type, we need to subscribe to each event type individually
        SubscribeToEventType<TurnStartEvent>(eventBus, logCollector);
        SubscribeToEventType<TurnEndEvent>(eventBus, logCollector);
        SubscribeToEventType<PhaseStartEvent>(eventBus, logCollector);
        SubscribeToEventType<PhaseEndEvent>(eventBus, logCollector);
        SubscribeToEventType<DamageAppliedEvent>(eventBus, logCollector);
        SubscribeToEventType<CardMovedEvent>(eventBus, logCollector);
        SubscribeToEventType<DyingStartEvent>(eventBus, logCollector);
        SubscribeToEventType<PlayerDiedEvent>(eventBus, logCollector);

        return eventBus;
    }

    /// <summary>
    /// Helper method to subscribe to a specific event type and collect it as a log event.
    /// </summary>
    private static void SubscribeToEventType<T>(IEventBus eventBus, ILogCollector logCollector) where T : IGameEvent
    {
        eventBus.Subscribe<T>(gameEvent =>
        {
            var sequenceNumber = logCollector.GetNextSequenceNumber();
            var logEvent = LogEventMapper.MapFromGameEvent(gameEvent, sequenceNumber);
            
            if (logEvent is not null)
            {
                logCollector.Collect(logEvent);
            }
        });
    }
}
