using System;
using System.Collections.Generic;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Configuration;
using LegendOfThreeKingdoms.Core.Identity;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Turns;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.GameSetup;

/// <summary>
/// Basic implementation of <see cref="IGameInitializer"/> that builds a deck
/// from <see cref="DeckConfig"/>, shuffles it using <see cref="IRandomSource"/>
/// and writes it into the game's draw pile.
/// Later phases will extend this class with player hand distribution and
/// integration with <see cref="ITurnEngine"/>.
/// </summary>
public sealed class BasicGameInitializer : IGameInitializer
{
    private readonly ICardMoveService _cardMoveService;

    /// <summary>
    /// Creates a basic initializer that uses <see cref="BasicCardMoveService"/>
    /// for all card movement during setup.
    /// </summary>
    public BasicGameInitializer()
        : this(new BasicCardMoveService())
    {
    }

    /// <summary>
    /// Creates an initializer that uses the provided <see cref="ICardMoveService"/>
    /// for all card movement during setup. This overload exists primarily to
    /// facilitate testing or future customisation.
    /// </summary>
    public BasicGameInitializer(ICardMoveService cardMoveService)
    {
        _cardMoveService = cardMoveService ?? throw new ArgumentNullException(nameof(cardMoveService));
    }

    public GameInitializationResult Initialize(GameInitializationOptions options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        if (options.GameConfig is null) throw new ArgumentException("GameConfig must be provided.", nameof(options));
        if (options.GameMode is null) throw new ArgumentException("GameMode must be provided.", nameof(options));

        var config = options.GameConfig;

        if (config.PlayerConfigs is null || config.PlayerConfigs.Count == 0)
        {
            return GameInitializationResult.Failure(
                errorCode: "NoPlayers",
                errorMessage: "GameConfig must contain at least one player configuration.");
        }

        // Phase 1: map configuration to a bare Game state.
        var game = Game.FromConfig(config);

        // Phase 1.5: assign roles if the game mode supports it (identity mode).
        var roleAssignmentService = options.GameMode.GetRoleAssignmentService();
        if (roleAssignmentService is not null)
        {
            var updatedGame = roleAssignmentService.AssignRoles(game, options.Random);
            if (updatedGame is null)
            {
                return GameInitializationResult.Failure(
                    errorCode: "RoleAssignmentFailed",
                    errorMessage: "Failed to assign roles to players.");
            }
            game = updatedGame;
            
            // Reveal Lord's role
            roleAssignmentService.RevealLordRole(game);
        }

        // Phase 2: build and shuffle the deck, then write it into the draw pile.
        var deckCardIds = options.PrebuiltDeckCardIds ?? BuildDeckCardIds(config.DeckConfig);
        if (deckCardIds.Count == 0)
        {
            return GameInitializationResult.Failure(
                errorCode: "EmptyDeck",
                errorMessage: "Deck configuration produced an empty deck.");
        }

        var shuffledDeck = Shuffle(deckCardIds, options.Random);
        PopulateDrawPile(game, shuffledDeck);

        // Phase 3: distribute initial hands to each player from the draw pile.
        var handDistributionResult = DealInitialHands(game, config.InitialHandCardCount);
        if (!handDistributionResult.Success)
        {
            return handDistributionResult;
        }

        // Phase 4: initialize the first turn using the basic turn engine and game mode.
        var turnEngine = new BasicTurnEngine(options.GameMode);
        _ = turnEngine.InitializeTurnState(game);

        // Phase 5: set up win condition checking if event bus and win condition service are available.
        if (options.EventBus is not null)
        {
            var winConditionService = options.GameMode.GetWinConditionService();
            if (winConditionService is not null)
            {
                // Create WinConditionChecker which will automatically subscribe to PlayerDiedEvent
                _ = new WinConditionChecker(winConditionService, options.EventBus);
            }
        }

        return GameInitializationResult.SuccessResult(game);
    }

    /// <summary>
    /// Builds a simple deck list based on the supplied <see cref="DeckConfig"/>.
    /// The first implementation only honors included packs and ignores overrides.
    /// Card definition to ID mapping is delegated to content; here we only
    /// keep definition identifiers as strings.
    /// </summary>
    private static IReadOnlyList<string> BuildDeckCardIds(DeckConfig deckConfig)
    {
        var result = new List<string>();

        // Minimal placeholder implementation:
        // for now, if at least one pack is enabled we create a small deterministic
        // set of base cards sufficient for tests. Real content loading will replace this.
        if (deckConfig.IncludedPacks.Count == 0)
        {
            return result;
        }

        // Basic skeleton deck: a few Slash / Dodge / Peach definitions.
        // Definition ids are opaque strings; tests can assert on their order.
        for (var i = 0; i < 20; i++)
        {
            result.Add("Base.Slash");
        }

        for (var i = 0; i < 10; i++)
        {
            result.Add("Base.Dodge");
        }

        for (var i = 0; i < 6; i++)
        {
            result.Add("Base.Peach");
        }

        return result;
    }

    /// <summary>
    /// Deals the configured number of initial cards from the draw pile to each player's hand zone.
    /// </summary>
    /// <param name="game">Game whose draw pile and players will be mutated.</param>
    /// <param name="initialHandCardCount">Number of cards each player should receive.</param>
    private GameInitializationResult DealInitialHands(Game game, int initialHandCardCount)
    {
        if (initialHandCardCount <= 0)
        {
            return GameInitializationResult.SuccessResult(game);
        }

        if (game.DrawPile is not Zone drawZone)
        {
            throw new InvalidOperationException("Game.DrawPile must be a mutable Zone.");
        }

        var totalRequired = initialHandCardCount * game.Players.Count;
        if (drawZone.Cards.Count < totalRequired)
        {
            return GameInitializationResult.Failure(
                errorCode: "NotEnoughCardsForInitialHands",
                errorMessage: $"Deck does not contain enough cards to deal {initialHandCardCount} initial cards to {game.Players.Count} players.");
        }

        foreach (var player in game.Players)
        {
            // Use the shared card move service to draw the configured number
            // of cards from the global draw pile into this player's hand.
            _ = _cardMoveService.DrawCards(game, player, initialHandCardCount);
        }

        return GameInitializationResult.SuccessResult(game);
    }

    /// <summary>
    /// Performs an in-place Fisher–Yates shuffle using <see cref="_random"/>.
    /// Returns a new list instance containing the shuffled elements to keep
    /// the input immutable from the caller's perspective.
    /// </summary>
    private static IReadOnlyList<string> Shuffle(IReadOnlyList<string> source, IRandomSource random)
    {
        var list = new List<string>(source);

        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = random.NextInt(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }

    /// <summary>
    /// Populates the game's draw pile zone with card placeholders that reference
    /// the shuffled definition ids. At this stage we only need deterministic
    /// ordering; full card metadata will be supplied by the content layer later.
    /// </summary>
    private static void PopulateDrawPile(Game game, IReadOnlyList<string> shuffledDefinitionIds)
    {
        if (game.DrawPile is not Zone drawZone)
        {
            // In theory DrawPile should always be a concrete Zone, but guard anyway.
            throw new InvalidOperationException("Game.DrawPile must be a mutable Zone.");
        }

        drawZone.MutableCards.Clear();

        var nextInstanceId = 1;
        foreach (var defId in shuffledDefinitionIds)
        {
            // For now we only care about deterministic ordering. Suit/rank/type
            // will be refined when the content layer is introduced.
            var card = new Card
            {
                Id = nextInstanceId++,
                DefinitionId = defId,
                Suit = Suit.Spade,
                Rank = 1,
                CardType = CardType.Basic,
                CardSubType = CardSubType.Unknown
            };

            drawZone.MutableCards.Add(card);
        }
    }
}

