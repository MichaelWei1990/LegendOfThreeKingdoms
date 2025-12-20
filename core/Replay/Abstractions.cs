using System.Collections.Generic;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Configuration;
using LegendOfThreeKingdoms.Core.Logging;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;

namespace LegendOfThreeKingdoms.Core.Replay;

/// <summary>
/// Minimal data structure required to replay a game.
/// Contains only the seed, initial configuration, and sequence of player choices.
/// </summary>
public sealed record ReplayRecord
{
    /// <summary>
    /// Random seed used during game initialization.
    /// Same seed + same config + same choices = same game state.
    /// </summary>
    public required int? Seed { get; init; }

    /// <summary>
    /// Initial game configuration used to create the game.
    /// </summary>
    public required GameConfig InitialConfig { get; init; }

    /// <summary>
    /// Sequence of player choices made during the game, in chronological order.
    /// Each choice corresponds to a ChoiceRequest that was presented during gameplay.
    /// </summary>
    public required IReadOnlyList<ChoiceResult> ChoiceSequence { get; init; }
}

/// <summary>
/// Engine responsible for replaying games from ReplayRecord.
/// </summary>
public interface IReplayEngine
{
    /// <summary>
    /// Starts a replay session from the given ReplayRecord.
    /// Rebuilds the game state using the seed and config, then replays choices in sequence.
    /// </summary>
    /// <param name="record">The replay record containing seed, config, and choice sequence.</param>
    /// <param name="gameMode">The game mode instance to use for initialization.</param>
    /// <param name="logCollector">Optional log collector for recording events during replay.</param>
    /// <returns>The result of the replay operation, containing the final game state.</returns>
    ReplayResult StartReplay(
        ReplayRecord record,
        IGameMode gameMode,
        ILogCollector? logCollector = null);
}

/// <summary>
/// Result of a replay operation.
/// </summary>
public sealed class ReplayResult
{
    /// <summary>
    /// Whether the replay completed successfully.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// The final game state after replaying all choices.
    /// </summary>
    public Game? Game { get; init; }

    /// <summary>
    /// Optional error code if replay failed.
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Optional error message if replay failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Number of choices that were successfully replayed.
    /// </summary>
    public required int ChoicesReplayed { get; init; }

    /// <summary>
    /// Total number of choices in the replay record.
    /// </summary>
    public required int TotalChoices { get; init; }
}
