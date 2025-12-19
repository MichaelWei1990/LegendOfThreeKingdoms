using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using LegendOfThreeKingdoms.Core.Logging;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Logging;

/// <summary>
/// Provides serialization support for log events.
/// Supports JSON serialization for persistence and transmission.
/// </summary>
public static class LogEventSerialization
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Serializes a log event to JSON string.
    /// </summary>
    /// <param name="logEvent">The log event to serialize.</param>
    /// <param name="options">Optional JSON serializer options. If null, default options are used.</param>
    /// <returns>JSON string representation of the log event.</returns>
    public static string SerializeToJson(ILogEvent logEvent, JsonSerializerOptions? options = null)
    {
        if (logEvent is null)
            throw new ArgumentNullException(nameof(logEvent));

        var optionsToUse = options ?? DefaultOptions;
        return JsonSerializer.Serialize(logEvent, optionsToUse);
    }

    /// <summary>
    /// Serializes a collection of log events to JSON string.
    /// </summary>
    /// <param name="logEvents">The log events to serialize.</param>
    /// <param name="options">Optional JSON serializer options. If null, default options are used.</param>
    /// <returns>JSON string representation of the log events array.</returns>
    public static string SerializeToJson(IEnumerable<ILogEvent> logEvents, JsonSerializerOptions? options = null)
    {
        if (logEvents is null)
            throw new ArgumentNullException(nameof(logEvents));

        var optionsToUse = options ?? DefaultOptions;
        return JsonSerializer.Serialize(logEvents.ToArray(), optionsToUse);
    }

    /// <summary>
    /// Deserializes a JSON string to a log event.
    /// Note: This is a basic implementation. For full deserialization support,
    /// you may need to implement custom converters for specific event types.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="options">Optional JSON serializer options. If null, default options are used.</param>
    /// <returns>A deserialized log event, or null if deserialization fails.</returns>
    public static ILogEvent? DeserializeFromJson(string json, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var optionsToUse = options ?? DefaultOptions;
            // For now, we deserialize as a generic LogEvent
            // In a more sophisticated implementation, we could use polymorphic deserialization
            return JsonSerializer.Deserialize<LogEvent>(json, optionsToUse);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Deserializes a JSON string to a collection of log events.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="options">Optional JSON serializer options. If null, default options are used.</param>
    /// <returns>A collection of deserialized log events, or empty collection if deserialization fails.</returns>
    public static IReadOnlyList<ILogEvent> DeserializeArrayFromJson(string json, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<ILogEvent>();

        try
        {
            var optionsToUse = options ?? DefaultOptions;
            var events = JsonSerializer.Deserialize<LogEvent[]>(json, optionsToUse);
            return events ?? Array.Empty<ILogEvent>();
        }
        catch
        {
            return Array.Empty<ILogEvent>();
        }
    }
}
