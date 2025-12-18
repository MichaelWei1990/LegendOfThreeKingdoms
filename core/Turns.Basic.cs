using System;
using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Turns;

/// <summary>
/// Minimal, rules-agnostic implementation of <see cref="ITurnEngine"/>.
///
/// Responsibilities:
/// - Maintain a fixed phase order for each player's turn.
/// - Rotate turns between alive players in seat order.
/// - Provide simple helpers to inspect and mutate the turn-related
///   fields on <see cref="Game"/>.
///
/// This engine intentionally does not inspect card state, skills or
/// higher level rules. Those concerns live in Rules/Actions/Resolvers.
/// </summary>
public sealed class BasicTurnEngine : ITurnEngine
{
    private readonly IGameMode _gameMode;

    private static readonly Phase[] PhaseOrder =
    {
        Phase.Start,
        Phase.Judge,
        Phase.Draw,
        Phase.Play,
        Phase.Discard,
        Phase.End
    };

    /// <summary>
    /// Creates a new <see cref="BasicTurnEngine"/> that uses the given
    /// <see cref="IGameMode"/> to select the first player seat when a game starts.
    /// </summary>
    public BasicTurnEngine(IGameMode gameMode)
    {
        _gameMode = gameMode ?? throw new ArgumentNullException(nameof(gameMode));
    }

    public TurnState InitializeTurnState(Game game)
    {
        if (game is null)
        {
            throw new ArgumentNullException(nameof(game));
        }

        var firstSeat = SelectFirstSeat(game);
        if (firstSeat is null)
        {
            game.IsFinished = true;
            return game.Turn;
        }

        game.CurrentPlayerSeat = firstSeat.Value;
        game.CurrentPhase = Phase.Start;
        game.TurnNumber = 1;

        return game.Turn;
    }

    public TurnTransitionResult AdvancePhase(Game game)
    {
        if (game is null)
        {
            throw new ArgumentNullException(nameof(game));
        }

        if (game.IsFinished)
        {
            return new TurnTransitionResult
            {
                IsSuccess = false,
                TurnState = game.Turn,
                ErrorCode = TurnTransitionErrorCode.NoAlivePlayers
            };
        }

        // If current phase is not recognized (e.g. None), treat this as starting at the first phase.
        var currentIndex = Array.IndexOf(PhaseOrder, game.CurrentPhase);
        if (currentIndex < 0)
        {
            game.CurrentPhase = PhaseOrder[0];
            return new TurnTransitionResult
            {
                IsSuccess = true,
                TurnState = game.Turn,
                ErrorCode = TurnTransitionErrorCode.None
            };
        }

        // If we are at the last phase, move to the next player's Start phase.
        if (currentIndex == PhaseOrder.Length - 1)
        {
            var nextTurn = StartNextTurn(game);
            return nextTurn;
        }

        game.CurrentPhase = PhaseOrder[currentIndex + 1];

        return new TurnTransitionResult
        {
            IsSuccess = true,
            TurnState = game.Turn,
            ErrorCode = TurnTransitionErrorCode.None
        };
    }

    public TurnTransitionResult StartNextTurn(Game game)
    {
        if (game is null)
        {
            throw new ArgumentNullException(nameof(game));
        }

        var nextSeat = FindNextAliveSeat(game, game.CurrentPlayerSeat);
        if (nextSeat is null)
        {
            game.IsFinished = true;
            return new TurnTransitionResult
            {
                IsSuccess = false,
                TurnState = game.Turn,
                ErrorCode = TurnTransitionErrorCode.NoAlivePlayers
            };
        }

        game.CurrentPlayerSeat = nextSeat.Value;
        game.CurrentPhase = Phase.Start;
        game.TurnNumber = Math.Max(1, game.TurnNumber + 1);

        return new TurnTransitionResult
        {
            IsSuccess = true,
            TurnState = game.Turn,
            ErrorCode = TurnTransitionErrorCode.None
        };
    }

    public TurnState GetCurrentTurnState(Game game)
    {
        if (game is null)
        {
            throw new ArgumentNullException(nameof(game));
        }

        return game.Turn;
    }

    public bool CanEndCurrentPhase(Game game)
    {
        if (game is null)
        {
            throw new ArgumentNullException(nameof(game));
        }

        if (game.IsFinished)
        {
            return false;
        }

        // For the minimal engine we allow ending any phase as long as the game is not finished.
        // Higher layers (rules/actions/game modes) are responsible for applying stricter rules,
        // e.g. disallowing early end of Play phase when mandatory actions remain.
        return true;
    }

    private int? SelectFirstSeat(Game game)
    {
        // Ask the game mode for its preferred first player seat.
        // If the returned seat is invalid or not alive, fall back to
        // the first alive player in seat order so the engine can still run.
        try
        {
            var seat = _gameMode.SelectFirstPlayerSeat(game);
            if (IsAliveSeat(game, seat))
            {
                return seat;
            }
        }
        catch
        {
            // Game mode misconfiguration should not make the engine unusable;
            // we will fall back to a simple alive-seat search below.
        }

        var firstAlive = game.Players.FirstOrDefault(p => p.IsAlive);
        return firstAlive?.Seat;
    }

    private static bool IsAliveSeat(Game game, int seat)
    {
        return game.Players.Any(p => p.Seat == seat && p.IsAlive);
    }

    private static int? FindNextAliveSeat(Game game, int currentSeat)
    {
        if (game.Players.Count == 0)
        {
            return null;
        }

        var total = game.Players.Count;

        // advance at least one step to avoid staying on the same player
        for (var offset = 1; offset <= total; offset++)
        {
            var seat = (currentSeat + offset) % total;
            var player = game.Players.FirstOrDefault(p => p.Seat == seat && p.IsAlive);
            if (player is not null)
            {
                return player.Seat;
            }
        }

        // no alive players
        return null;
    }
}
