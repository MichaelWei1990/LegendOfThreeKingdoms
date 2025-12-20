using System;
using System.Collections.Generic;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Logging;

/// <summary>
/// Base interface for all structured log events.
/// Log events are used for replay, debugging, and battle report generation.
/// </summary>
public interface ILogEvent
{
    /// <summary>
    /// Timestamp when the event occurred (UTC).
    /// </summary>
    DateTime Timestamp { get; }

    /// <summary>
    /// Sequence number of the event, used to guarantee ordering.
    /// </summary>
    long SequenceNumber { get; }

    /// <summary>
    /// Event type identifier (e.g., "TurnStart", "DamageApplied", "CardUsed").
    /// </summary>
    string EventType { get; }

    /// <summary>
    /// The game instance this event belongs to.
    /// </summary>
    Game Game { get; }

    /// <summary>
    /// Optional structured data payload associated with the event.
    /// </summary>
    object? Data { get; }
}

/// <summary>
/// Base record type for log events.
/// Implements ILogEvent interface.
/// </summary>
public abstract record LogEvent(
    DateTime Timestamp,
    long SequenceNumber,
    string EventType,
    Game Game,
    object? Data = null
) : ILogEvent;

/// <summary>
/// Interface for collecting structured log events.
/// </summary>
public interface ILogCollector
{
    /// <summary>
    /// Collects a log event.
    /// </summary>
    /// <param name="logEvent">The log event to collect.</param>
    void Collect(ILogEvent logEvent);

    /// <summary>
    /// Gets all collected events in order.
    /// </summary>
    /// <returns>A read-only list of collected log events.</returns>
    IReadOnlyList<ILogEvent> GetEvents();

    /// <summary>
    /// Clears all collected events.
    /// </summary>
    void Clear();

    /// <summary>
    /// Gets the next sequence number for a new event.
    /// </summary>
    /// <returns>The next sequence number.</returns>
    long GetNextSequenceNumber();
}
