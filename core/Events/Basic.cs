using System;
using System.Collections.Generic;
using LegendOfThreeKingdoms.Core.Events;

namespace LegendOfThreeKingdoms.Core.Events;

/// <summary>
/// Basic implementation of the event bus.
/// Provides synchronous, single-threaded event distribution.
/// </summary>
public sealed class BasicEventBus : IEventBus
{
    private readonly Dictionary<Type, List<Delegate>> _subscribers = new();

    /// <inheritdoc />
    public void Publish<T>(T gameEvent) where T : IGameEvent
    {
        if (gameEvent is null)
            throw new ArgumentNullException(nameof(gameEvent));

        var eventType = typeof(T);
        if (!_subscribers.TryGetValue(eventType, out var handlers) || handlers.Count == 0)
        {
            // No subscribers for this event type, nothing to do
            return;
        }

        // Invoke all handlers synchronously
        // If a handler throws an exception, we continue with other handlers
        // (exception handling strategy: log but don't interrupt)
        foreach (var handler in handlers)
        {
            try
            {
                if (handler is Action<T> typedHandler)
                {
                    typedHandler(gameEvent);
                }
            }
            catch
            {
                // In a production system, we might want to log exceptions here
                // For now, we silently continue to avoid breaking other subscribers
            }
        }
    }

    /// <inheritdoc />
    public void Subscribe<T>(Action<T> handler) where T : IGameEvent
    {
        if (handler is null)
            throw new ArgumentNullException(nameof(handler));

        var eventType = typeof(T);
        if (!_subscribers.TryGetValue(eventType, out var handlers))
        {
            handlers = new List<Delegate>();
            _subscribers[eventType] = handlers;
        }

        handlers.Add(handler);
    }

    /// <inheritdoc />
    public void Unsubscribe<T>(Action<T> handler) where T : IGameEvent
    {
        if (handler is null)
            throw new ArgumentNullException(nameof(handler));

        var eventType = typeof(T);
        if (_subscribers.TryGetValue(eventType, out var handlers))
        {
            handlers.Remove(handler);
            
            // Clean up empty lists to avoid memory leaks
            if (handlers.Count == 0)
            {
                _subscribers.Remove(eventType);
            }
        }
    }
}
