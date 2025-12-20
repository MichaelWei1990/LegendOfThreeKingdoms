using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Logging;

namespace LegendOfThreeKingdoms.Core.Logging;

/// <summary>
/// Basic implementation of the log collector.
/// Stores events in memory and maintains sequence numbers.
/// </summary>
public sealed class BasicLogCollector : ILogCollector
{
    private readonly List<ILogEvent> _events = new();
    private long _sequenceCounter = 0;
    private readonly object _lock = new();

    /// <inheritdoc />
    public void Collect(ILogEvent logEvent)
    {
        if (logEvent is null)
            throw new ArgumentNullException(nameof(logEvent));

        lock (_lock)
        {
            _events.Add(logEvent);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ILogEvent> GetEvents()
    {
        lock (_lock)
        {
            return _events.ToList().AsReadOnly();
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_lock)
        {
            _events.Clear();
            _sequenceCounter = 0;
        }
    }

    /// <inheritdoc />
    public long GetNextSequenceNumber()
    {
        lock (_lock)
        {
            return ++_sequenceCounter;
        }
    }
}
