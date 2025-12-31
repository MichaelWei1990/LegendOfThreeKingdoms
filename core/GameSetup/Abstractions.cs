using System.Collections.Generic;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Configuration;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Turns;

namespace LegendOfThreeKingdoms.Core.GameSetup;

/// <summary>
/// Options required to initialize a new game instance from configuration.
/// This is a pure data carrier and must not contain behavior.
/// </summary>
public sealed class GameInitializationOptions
{
    /// <summary>
    /// Game-level configuration for the match to be created.
    /// </summary>
    public required GameConfig GameConfig { get; init; }

    /// <summary>
    /// Random source used for all non-deterministic decisions during setup
    /// (seat randomization, deck shuffling, hero assignment, etc.).
    /// The caller is responsible for seeding it appropriately.
    /// </summary>
    public required IRandomSource Random { get; init; }

    /// <summary>
    /// Game mode instance that interprets configuration and defines
    /// high level rules such as first player selection.
    /// </summary>
    public required IGameMode GameMode { get; init; }

    /// <summary>
    /// Optional pre-constructed deck list to use instead of building
    /// from <see cref="GameConfig.DeckConfig"/>. Primarily intended for tests.
    /// </summary>
    public IReadOnlyList<string>? PrebuiltDeckCardIds { get; init; }

    /// <summary>
    /// Optional event bus for publishing and subscribing to game events.
    /// If provided and the game mode supports win condition checking, a WinConditionChecker
    /// will be automatically created and registered to listen for PlayerDiedEvent.
    /// </summary>
    public Events.IEventBus? EventBus { get; init; }
}

/// <summary>
/// Structured result of a game initialization attempt.
/// This allows the caller to distinguish between hard failures and warnings.
/// </summary>
public sealed class GameInitializationResult
{
    /// <summary>
    /// Whether the initialization succeeded.
    /// When false, <see cref="Game"/> may be null or partially constructed.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The initialized game instance when <see cref="Success"/> is true.
    /// </summary>
    public Game? Game { get; init; }

    /// <summary>
    /// Optional machine-readable error code for failed initialization.
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Optional human-readable description of the failure.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Non-fatal warnings produced during initialization (e.g. minor config issues).
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = new List<string>();

    /// <summary>
    /// Helper for creating a successful result.
    /// </summary>
    public static GameInitializationResult SuccessResult(Game game, IReadOnlyList<string>? warnings = null) =>
        new()
        {
            Success = true,
            Game = game,
            Warnings = warnings ?? new List<string>()
        };

    /// <summary>
    /// Helper for creating a failed result.
    /// </summary>
    public static GameInitializationResult Failure(string errorCode, string errorMessage) =>
        new()
        {
            Success = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
}

/// <summary>
/// Abstraction over the complete game initialization pipeline that
/// turns configuration and a random source into an initial <see cref="Game"/> state
/// ready for the first turn.
/// </summary>
public interface IGameInitializer
{
    /// <summary>
    /// Initializes a new game instance according to the supplied options.
    /// Implementations must be pure with respect to the provided options:
    /// the same options should always result in an equivalent initial state.
    /// </summary>
    GameInitializationResult Initialize(GameInitializationOptions options);
}

