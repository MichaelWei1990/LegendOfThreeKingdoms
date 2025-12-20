using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.GameSetup;
using LegendOfThreeKingdoms.Core.Logging;

namespace LegendOfThreeKingdoms.Core.Replay;

/// <summary>
/// Extension methods for replay functionality.
/// </summary>
public static class ReplayExtensions
{
    /// <summary>
    /// Creates a replay engine instance using the default game initializer.
    /// </summary>
    public static IReplayEngine CreateReplayEngine()
    {
        return new BasicReplayEngine(new BasicGameInitializer());
    }

    /// <summary>
    /// Creates a replay engine instance using the provided game initializer.
    /// </summary>
    public static IReplayEngine CreateReplayEngine(IGameInitializer gameInitializer)
    {
        if (gameInitializer is null) throw new ArgumentNullException(nameof(gameInitializer));
        return new BasicReplayEngine(gameInitializer);
    }

    /// <summary>
    /// Starts a replay from the given replay record.
    /// </summary>
    /// <param name="record">The replay record containing seed, config, and choice sequence.</param>
    /// <param name="gameMode">The game mode instance to use for initialization.</param>
    /// <param name="logCollector">Optional log collector for recording events during replay.</param>
    /// <returns>The result of the replay operation, containing the final game state.</returns>
    public static ReplayResult StartReplay(
        ReplayRecord record,
        IGameMode gameMode,
        ILogCollector? logCollector = null)
    {
        var engine = CreateReplayEngine();
        return engine.StartReplay(record, gameMode, logCollector);
    }

    /// <summary>
    /// Starts a replay from the given replay record using a custom game initializer.
    /// </summary>
    /// <param name="record">The replay record containing seed, config, and choice sequence.</param>
    /// <param name="gameMode">The game mode instance to use for initialization.</param>
    /// <param name="gameInitializer">The game initializer to use for rebuilding the game.</param>
    /// <param name="logCollector">Optional log collector for recording events during replay.</param>
    /// <returns>The result of the replay operation, containing the final game state.</returns>
    public static ReplayResult StartReplay(
        ReplayRecord record,
        IGameMode gameMode,
        IGameInitializer gameInitializer,
        ILogCollector? logCollector = null)
    {
        var engine = CreateReplayEngine(gameInitializer);
        return engine.StartReplay(record, gameMode, logCollector);
    }
}
