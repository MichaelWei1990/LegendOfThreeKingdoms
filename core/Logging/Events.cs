using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Response;
using LegendOfThreeKingdoms.Core.Rules;

namespace LegendOfThreeKingdoms.Core.Logging;

/// <summary>
/// Log event for when a player's turn starts.
/// </summary>
public sealed record TurnStartLogEvent(
    DateTime Timestamp,
    long SequenceNumber,
    Game Game,
    int PlayerSeat,
    int TurnNumber
) : LogEvent(
    Timestamp,
    SequenceNumber,
    "TurnStart",
    Game,
    new { PlayerSeat, TurnNumber }
);

/// <summary>
/// Log event for when a player's turn ends.
/// </summary>
public sealed record TurnEndLogEvent(
    DateTime Timestamp,
    long SequenceNumber,
    Game Game,
    int PlayerSeat,
    int TurnNumber
) : LogEvent(
    Timestamp,
    SequenceNumber,
    "TurnEnd",
    Game,
    new { PlayerSeat, TurnNumber }
);

/// <summary>
/// Log event for when a phase starts.
/// </summary>
public sealed record PhaseStartLogEvent(
    DateTime Timestamp,
    long SequenceNumber,
    Game Game,
    int PlayerSeat,
    Phase Phase
) : LogEvent(
    Timestamp,
    SequenceNumber,
    "PhaseStart",
    Game,
    new { PlayerSeat, Phase = Phase.ToString() }
);

/// <summary>
/// Log event for when a phase ends.
/// </summary>
public sealed record PhaseEndLogEvent(
    DateTime Timestamp,
    long SequenceNumber,
    Game Game,
    int PlayerSeat,
    Phase Phase
) : LogEvent(
    Timestamp,
    SequenceNumber,
    "PhaseEnd",
    Game,
    new { PlayerSeat, Phase = Phase.ToString() }
);

/// <summary>
/// Log event for when a card is used.
/// </summary>
public sealed record CardUsedLogEvent(
    DateTime Timestamp,
    long SequenceNumber,
    Game Game,
    int SourcePlayerSeat,
    int CardId,
    CardSubType CardSubType,
    IReadOnlyList<int>? TargetSeats = null
) : LogEvent(
    Timestamp,
    SequenceNumber,
    "CardUsed",
    Game,
    new
    {
        SourcePlayerSeat,
        CardId,
        CardSubType = CardSubType.ToString(),
        TargetSeats = TargetSeats?.ToArray()
    }
);

/// <summary>
/// Log event for when damage is applied to a player.
/// </summary>
public sealed record DamageAppliedLogEvent(
    DateTime Timestamp,
    long SequenceNumber,
    Game Game,
    int SourceSeat,
    int TargetSeat,
    int Amount,
    DamageType Type,
    int PreviousHealth,
    int CurrentHealth
) : LogEvent(
    Timestamp,
    SequenceNumber,
    "DamageApplied",
    Game,
    new
    {
        SourceSeat,
        TargetSeat,
        Amount,
        Type = Type.ToString(),
        PreviousHealth,
        CurrentHealth
    }
);

/// <summary>
/// Log event for when cards are moved between zones.
/// </summary>
public sealed record CardMovedLogEvent(
    DateTime Timestamp,
    long SequenceNumber,
    Game Game,
    int CardId,
    string FromZone,
    string ToZone,
    int? FromOwnerSeat = null,
    int? ToOwnerSeat = null
) : LogEvent(
    Timestamp,
    SequenceNumber,
    "CardMoved",
    Game,
    new
    {
        CardId,
        FromZone,
        ToZone,
        FromOwnerSeat,
        ToOwnerSeat
    }
);

/// <summary>
/// Log event for when a player enters the dying state.
/// </summary>
public sealed record DyingStartLogEvent(
    DateTime Timestamp,
    long SequenceNumber,
    Game Game,
    int DyingPlayerSeat
) : LogEvent(
    Timestamp,
    SequenceNumber,
    "DyingStart",
    Game,
    new { DyingPlayerSeat }
);

/// <summary>
/// Log event for when a player dies.
/// </summary>
public sealed record PlayerDiedLogEvent(
    DateTime Timestamp,
    long SequenceNumber,
    Game Game,
    int DeadPlayerSeat,
    int? KillerSeat = null
) : LogEvent(
    Timestamp,
    SequenceNumber,
    "PlayerDied",
    Game,
    new { DeadPlayerSeat, KillerSeat }
);

/// <summary>
/// Log event for when a response window is opened.
/// </summary>
public sealed record ResponseWindowOpenedLogEvent(
    DateTime Timestamp,
    long SequenceNumber,
    Game Game,
    ResponseType ResponseType,
    IReadOnlyList<int> ResponderSeats
) : LogEvent(
    Timestamp,
    SequenceNumber,
    "ResponseWindowOpened",
    Game,
    new
    {
        ResponseType = ResponseType.ToString(),
        ResponderSeats = ResponderSeats.ToArray()
    }
);

/// <summary>
/// Log event for when a response card is played.
/// </summary>
public sealed record ResponseCardPlayedLogEvent(
    DateTime Timestamp,
    long SequenceNumber,
    Game Game,
    int ResponderSeat,
    int CardId,
    ResponseType ResponseType
) : LogEvent(
    Timestamp,
    SequenceNumber,
    "ResponseCardPlayed",
    Game,
    new
    {
        ResponderSeat,
        CardId,
        ResponseType = ResponseType.ToString()
    }
);
