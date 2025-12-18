using System;

namespace LegendOfThreeKingdoms.Core.Abstractions;

/// <summary>
/// Abstraction over structured logging for the core engine.
/// Implementations decide where the log is written (console, file, remote, etc.).
/// </summary>
public interface ILogSink
{
    /// <summary>
    /// Emits a structured log entry.
    /// </summary>
    void Log(LogEntry entry);
}

/// <summary>
/// Minimal structured log entry used by the core.
/// </summary>
public sealed class LogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public string Level { get; init; } = "Info";

    public string EventType { get; init; } = string.Empty;

    public string? Message { get; init; }

    /// <summary>
    /// Optional structured data payload associated with the event.
    /// </summary>
    public object? Data { get; init; }
}
