using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Character;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Turns;

namespace LegendOfThreeKingdoms.Core.Identity;

/// <summary>
/// Main flow controller for identity mode minimal flow.
/// Coordinates the complete game flow from creation to end:
/// 1. Create game
/// 2. Assign identities
/// 3. Lord selects hero
/// 4. Other players select heroes
/// 5. Start game
/// 6. Turn loop with win condition checking
/// </summary>
public sealed class IdentityGameFlowService
{
    private readonly IEventBus _eventBus;
    private readonly IRoleAssignmentService _roleAssignmentService;
    private readonly ICharacterSelectionService _characterSelectionService;
    private readonly IWinConditionService _winConditionService;
    private readonly ITurnEngine _turnEngine;
    private readonly ITurnExecutor _turnExecutor;
    private readonly IGameMode _gameMode;
    private readonly IRandomSource _random;

    /// <summary>
    /// Creates a new IdentityGameFlowService.
    /// </summary>
    public IdentityGameFlowService(
        IEventBus eventBus,
        IRoleAssignmentService roleAssignmentService,
        ICharacterSelectionService characterSelectionService,
        IWinConditionService winConditionService,
        ITurnEngine turnEngine,
        ITurnExecutor turnExecutor,
        IGameMode gameMode,
        IRandomSource random)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _roleAssignmentService = roleAssignmentService ?? throw new ArgumentNullException(nameof(roleAssignmentService));
        _characterSelectionService = characterSelectionService ?? throw new ArgumentNullException(nameof(characterSelectionService));
        _winConditionService = winConditionService ?? throw new ArgumentNullException(nameof(winConditionService));
        _turnEngine = turnEngine ?? throw new ArgumentNullException(nameof(turnEngine));
        _turnExecutor = turnExecutor ?? throw new ArgumentNullException(nameof(turnExecutor));
        _gameMode = gameMode ?? throw new ArgumentNullException(nameof(gameMode));
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    /// <summary>
    /// Step 0: Creates a game from configuration.
    /// </summary>
    /// <param name="config">Game configuration.</param>
    /// <returns>The created game with State = Created.</returns>
    public Game CreateGame(Configuration.GameConfig config)
    {
        if (config is null) throw new ArgumentNullException(nameof(config));

        var game = Game.FromConfig(config);
        game.State = GameState.Created;

        _eventBus.Publish(new GameCreatedEvent(game));

        return game;
    }

    /// <summary>
    /// Step 1: Assigns identities to all players.
    /// </summary>
    /// <param name="game">The game in Created state.</param>
    /// <returns>The updated game with State = IdentitiesAssigned.</returns>
    public Game AssignIdentities(Game game)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        if (game.State != GameState.Created)
            throw new InvalidOperationException($"Game must be in Created state, but is in {game.State}");

        var updatedGame = _roleAssignmentService.AssignRoles(game, _random);
        if (updatedGame is null)
            throw new InvalidOperationException("Failed to assign roles to players.");

        // Reveal Lord's role
        _roleAssignmentService.RevealLordRole(updatedGame);
        updatedGame.State = GameState.IdentitiesAssigned;

        // Publish events
        var assignments = updatedGame.Players
            .Select(p => (p.Seat, p.CampId ?? ""))
            .ToList();
        _eventBus.Publish(new IdentitiesAssignedEvent(updatedGame, assignments));

        var lord = updatedGame.Players.FirstOrDefault(p => p.CampId == RoleConstants.Lord);
        if (lord is not null)
        {
            _eventBus.Publish(new LordRevealedEvent(updatedGame, lord.Seat));
        }

        return updatedGame;
    }

    /// <summary>
    /// Step 2: Lord selects a hero.
    /// </summary>
    /// <param name="game">The game in IdentitiesAssigned state.</param>
    /// <param name="candidateHeroIds">Candidate hero IDs for the Lord.</param>
    /// <param name="selectedHeroId">The hero ID selected by the Lord.</param>
    /// <returns>The updated game.</returns>
    public Game LordSelectsHero(Game game, IReadOnlyList<string> candidateHeroIds, string selectedHeroId)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        if (game.State != GameState.IdentitiesAssigned)
            throw new InvalidOperationException($"Game must be in IdentitiesAssigned state, but is in {game.State}");

        var lord = game.Players.FirstOrDefault(p => p.CampId == RoleConstants.Lord);
        if (lord is null)
            throw new InvalidOperationException("Lord player not found.");

        // Set IsLord flag for skill registration
        lord.Flags["IsLord"] = true;

        // Track updated game through events
        Game? updatedGame = null;
        _eventBus.Subscribe<CharacterSelectedEvent>(OnCharacterSelected);
        try
        {
            // Offer candidates and select
            _characterSelectionService.OfferCharacters(game, lord.Seat, candidateHeroIds);
            _characterSelectionService.SelectCharacter(game, lord.Seat, selectedHeroId);

            // Get updated game from event
            updatedGame = updatedGame ?? game;

            // Verify selection
            var updatedLord = updatedGame.Players.FirstOrDefault(p => p.Seat == lord.Seat);
            if (updatedLord is null || string.IsNullOrWhiteSpace(updatedLord.HeroId))
                throw new InvalidOperationException("Lord hero selection failed.");

            return updatedGame;
        }
        finally
        {
            _eventBus.Unsubscribe<CharacterSelectedEvent>(OnCharacterSelected);
        }

        void OnCharacterSelected(CharacterSelectedEvent evt)
        {
            if (evt.PlayerSeat == lord.Seat)
            {
                updatedGame = evt.Game;
            }
        }
    }

    /// <summary>
    /// Step 3: Other players select heroes (in seat order).
    /// </summary>
    /// <param name="game">The game after Lord has selected.</param>
    /// <param name="getCandidatesForPlayer">Function to get candidate hero IDs for each player.</param>
    /// <param name="getSelectionForPlayer">Function to get the selected hero ID for each player.</param>
    /// <returns>The updated game with State = HeroesSelected.</returns>
    public Game OtherPlayersSelectHeroes(
        Game game,
        Func<Game, Player, IReadOnlyList<string>> getCandidatesForPlayer,
        Func<Game, Player, string> getSelectionForPlayer)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        if (getCandidatesForPlayer is null) throw new ArgumentNullException(nameof(getCandidatesForPlayer));
        if (getSelectionForPlayer is null) throw new ArgumentNullException(nameof(getSelectionForPlayer));

        var lord = game.Players.FirstOrDefault(p => p.CampId == RoleConstants.Lord);
        if (lord is null)
            throw new InvalidOperationException("Lord player not found.");

        // Process non-Lord players in seat order
        var otherPlayers = game.Players
            .Where(p => p.CampId != RoleConstants.Lord)
            .OrderBy(p => p.Seat)
            .ToList();

        // Track updated game through events
        Game currentGame = game;
        _eventBus.Subscribe<CharacterSelectedEvent>(OnCharacterSelected);

        try
        {
            foreach (var player in otherPlayers)
            {
                var candidates = getCandidatesForPlayer(currentGame, player);
                var selected = getSelectionForPlayer(currentGame, player);

                _characterSelectionService.OfferCharacters(currentGame, player.Seat, candidates);
                _characterSelectionService.SelectCharacter(currentGame, player.Seat, selected);
            }

            currentGame.State = GameState.HeroesSelected;
            return currentGame;
        }
        finally
        {
            _eventBus.Unsubscribe<CharacterSelectedEvent>(OnCharacterSelected);
        }

        void OnCharacterSelected(CharacterSelectedEvent evt)
        {
            currentGame = evt.Game;
        }
    }

    /// <summary>
    /// Step 4: Starts the game (from Lord's turn).
    /// </summary>
    /// <param name="game">The game in HeroesSelected state.</param>
    /// <returns>The updated game with State = Running.</returns>
    public Game StartGame(Game game)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        if (game.State != GameState.HeroesSelected)
            throw new InvalidOperationException($"Game must be in HeroesSelected state, but is in {game.State}");

        game.State = GameState.Running;

        // Initialize turn state (sets CurrentPlayerSeat to Lord)
        _turnEngine.InitializeTurnState(game);

        _eventBus.Publish(new GameStartedEvent(game));

        return game;
    }

    /// <summary>
    /// Step 5: Executes one turn cycle (current player's turn).
    /// </summary>
    /// <param name="game">The game in Running state.</param>
    /// <returns>The updated game (may be in Finished state if win condition met).</returns>
    public Game ExecuteTurnCycle(Game game)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        if (game.State != GameState.Running)
            throw new InvalidOperationException($"Game must be in Running state, but is in {game.State}");
        if (game.IsFinished)
            throw new InvalidOperationException("Game is already finished.");

        var currentPlayer = game.Players.FirstOrDefault(p => p.Seat == game.CurrentPlayerSeat);
        if (currentPlayer is null)
            throw new InvalidOperationException($"Player at seat {game.CurrentPlayerSeat} not found.");

        // Skip dead players
        if (!currentPlayer.IsAlive)
        {
            _turnEngine.StartNextTurn(game);
            return game;
        }

        // Execute turn (minimal implementation - no-op for now)
        _turnExecutor.ExecuteTurn(game, currentPlayer);

        // Publish TurnEndEvent after turn execution
        _eventBus.Publish(new TurnEndEvent(game, currentPlayer.Seat, game.TurnNumber));

        // Check win conditions
        var winResult = _winConditionService.CheckWinConditions(game);
        if (winResult.IsGameOver)
        {
            return EndGame(game, winResult);
        }

        // Advance to next turn
        _turnEngine.StartNextTurn(game);

        return game;
    }

    /// <summary>
    /// Step 6: Ends the game when a win condition is met.
    /// </summary>
    /// <param name="game">The game state.</param>
    /// <param name="winResult">The win condition result.</param>
    /// <returns>The updated game with State = Finished.</returns>
    private Game EndGame(Game game, WinConditionResult winResult)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        if (winResult is null) throw new ArgumentNullException(nameof(winResult));
        if (!winResult.IsGameOver)
            throw new ArgumentException("Win result does not indicate game over.", nameof(winResult));

        game.State = GameState.Finished;
        game.IsFinished = true;
        game.WinnerDescription = winResult.EndReason;

        var winningSeats = winResult.WinningPlayers?
            .Select(p => p.Seat)
            .ToList() ?? new List<int>();

        _eventBus.Publish(new GameEndedEvent(
            game,
            winResult.WinType!.Value,
            winningSeats,
            winResult.EndReason ?? "Unknown"));

        return game;
    }

    /// <summary>
    /// Runs the complete game flow until a win condition is met.
    /// This is a convenience method that executes turn cycles until the game ends.
    /// </summary>
    /// <param name="game">The game in Running state.</param>
    /// <param name="maxTurns">Maximum number of turns to execute (safety limit).</param>
    /// <returns>The final game state.</returns>
    public Game RunUntilEnd(Game game, int maxTurns = 1000)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        if (game.State != GameState.Running)
            throw new InvalidOperationException($"Game must be in Running state, but is in {game.State}");

        int turnCount = 0;
        while (!game.IsFinished && turnCount < maxTurns)
        {
            game = ExecuteTurnCycle(game);
            turnCount++;
        }

        if (turnCount >= maxTurns && !game.IsFinished)
        {
            throw new InvalidOperationException($"Game did not end after {maxTurns} turns.");
        }

        return game;
    }
}
