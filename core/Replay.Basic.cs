using System;
using System.Collections.Generic;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.GameSetup;
using LegendOfThreeKingdoms.Core.Logging;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;

namespace LegendOfThreeKingdoms.Core.Replay;

/// <summary>
/// Provides player choices from a replay record's choice sequence.
/// Maintains an index to track which choice to return next.
/// </summary>
public sealed class ReplayChoiceProvider
{
    private readonly IReadOnlyList<ChoiceResult> _choices;
    private int _index;

    public ReplayChoiceProvider(IReadOnlyList<ChoiceResult> choices)
    {
        _choices = choices ?? throw new ArgumentNullException(nameof(choices));
        _index = 0;
    }

    /// <summary>
    /// Gets the next choice from the sequence that matches the given request.
    /// </summary>
    public ChoiceResult? GetNextChoice(ChoiceRequest request)
    {
        if (_index >= _choices.Count)
        {
            return null; // No more choices available
        }

        // For now, we simply return the next choice in sequence.
        // Future enhancements could validate that the choice matches the request.
        var choice = _choices[_index];
        _index++;
        return choice;
    }

    /// <summary>
    /// Gets the number of remaining choices.
    /// </summary>
    public int RemainingChoices => _choices.Count - _index;

    /// <summary>
    /// Gets the number of choices that have been consumed.
    /// </summary>
    public int ChoicesConsumed => _index;
}

/// <summary>
/// Basic implementation of IReplayEngine that rebuilds games from ReplayRecord
/// and provides choices from the recorded sequence.
/// </summary>
public sealed class BasicReplayEngine : IReplayEngine
{
    private readonly IGameInitializer _gameInitializer;

    /// <summary>
    /// Creates a new BasicReplayEngine that uses the given game initializer.
    /// </summary>
    public BasicReplayEngine(IGameInitializer gameInitializer)
    {
        _gameInitializer = gameInitializer ?? throw new ArgumentNullException(nameof(gameInitializer));
    }

    /// <inheritdoc />
    public ReplayResult StartReplay(
        ReplayRecord record,
        IGameMode gameMode,
        ILogCollector? logCollector = null)
    {
        if (record is null) throw new ArgumentNullException(nameof(record));
        if (gameMode is null) throw new ArgumentNullException(nameof(gameMode));

        // Validate input
        if (record.InitialConfig is null)
        {
            return new ReplayResult
            {
                Success = false,
                ErrorCode = "InvalidConfig",
                ErrorMessage = "ReplayRecord.InitialConfig cannot be null.",
                ChoicesReplayed = 0,
                TotalChoices = record.ChoiceSequence?.Count ?? 0
            };
        }

        if (record.ChoiceSequence is null)
        {
            return new ReplayResult
            {
                Success = false,
                ErrorCode = "InvalidChoiceSequence",
                ErrorMessage = "ReplayRecord.ChoiceSequence cannot be null.",
                ChoicesReplayed = 0,
                TotalChoices = 0
            };
        }

        // Create deterministic random source
        IRandomSource random;
        if (record.Seed.HasValue)
        {
            random = new SeededRandomSource(record.Seed.Value);
        }
        else
        {
            // If no seed is provided, use a fixed seed for determinism
            // In practice, replay records should always have a seed
            random = new SeededRandomSource(0);
        }

        // Rebuild game state
        var initOptions = new GameInitializationOptions
        {
            GameConfig = record.InitialConfig,
            Random = random,
            GameMode = gameMode
        };

        var initResult = _gameInitializer.Initialize(initOptions);
        if (!initResult.Success)
        {
            return new ReplayResult
            {
                Success = false,
                ErrorCode = initResult.ErrorCode ?? "InitializationFailed",
                ErrorMessage = initResult.ErrorMessage ?? "Game initialization failed.",
                ChoicesReplayed = 0,
                TotalChoices = record.ChoiceSequence.Count
            };
        }

        var game = initResult.Game!;

        // Create choice provider
        var choiceProvider = new ReplayChoiceProvider(record.ChoiceSequence);

        // Create GetPlayerChoice function that will be used during game execution
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = request =>
        {
            var choice = choiceProvider.GetNextChoice(request);
            if (choice is null)
            {
                // If no more choices are available, we can either throw an exception
                // or return a default choice (pass). For replay, throwing is more appropriate
                // to indicate that the replay record is incomplete.
                throw new InvalidOperationException(
                    $"No more choices available in replay record. Request: {request.RequestId}, Player: {request.PlayerSeat}, Choices consumed: {choiceProvider.ChoicesConsumed}, Total: {record.ChoiceSequence.Count}");
            }
            return choice;
        };

        // Note: The actual game execution loop is not implemented here because it requires
        // many dependencies (ActionResolutionMapper, RuleService, etc.) and complex game logic.
        // The replay engine rebuilds the game state and provides a GetPlayerChoice function.
        // The caller is responsible for executing the game logic and using the GetPlayerChoice function.
        // This is intentional to keep the replay engine simple and focused on its core responsibility
        // of rebuilding game state and providing choices from the recorded sequence.

        // Return result with the initial game state and choice provider information
        // The caller can use the GetPlayerChoice function to execute the game logic.
        return new ReplayResult
        {
            Success = true,
            Game = game,
            ChoicesReplayed = 0,
            TotalChoices = record.ChoiceSequence.Count
        };
    }

    /// <summary>
    /// Creates a GetPlayerChoice function that retrieves choices from the replay record.
    /// This can be used by game execution code to get player choices during replay.
    /// </summary>
    public static Func<ChoiceRequest, ChoiceResult> CreateGetPlayerChoiceFunction(ReplayRecord record, out ReplayChoiceProvider provider)
    {
        if (record is null) throw new ArgumentNullException(nameof(record));
        if (record.ChoiceSequence is null) throw new ArgumentException("ChoiceSequence cannot be null.", nameof(record));

        var choiceProvider = new ReplayChoiceProvider(record.ChoiceSequence);
        provider = choiceProvider;
        
        return request =>
        {
            var choice = choiceProvider.GetNextChoice(request);
            if (choice is null)
            {
                throw new InvalidOperationException(
                    $"No more choices available in replay record. Request: {request.RequestId}, Player: {request.PlayerSeat}, Choices consumed: {choiceProvider.ChoicesConsumed}, Total: {record.ChoiceSequence.Count}");
            }
            return choice;
        };
    }
}
