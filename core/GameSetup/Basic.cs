using System;
using System.Collections.Generic;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Configuration;
using LegendOfThreeKingdoms.Core.Content;
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
    /// Builds a complete standard edition deck list based on the supplied <see cref="DeckConfig"/>.
    /// Standard edition contains 108 cards: 53 basic cards, 36 trick cards, and 19 equipment cards.
    /// </summary>
    private static IReadOnlyList<string> BuildDeckCardIds(DeckConfig deckConfig)
    {
        var result = new List<string>();

        if (deckConfig.IncludedPacks.Count == 0)
        {
            return result;
        }

        // Basic cards (53 cards total)
        // 杀 (Slash): 30 cards
        for (var i = 0; i < 30; i++)
        {
            result.Add("Base.Slash");
        }

        // 闪 (Dodge): 15 cards
        for (var i = 0; i < 15; i++)
        {
            result.Add("Base.Dodge");
        }

        // 桃 (Peach): 8 cards
        for (var i = 0; i < 8; i++)
        {
            result.Add("Base.Peach");
        }

        // Trick cards (36 cards total)
        // 过河拆桥 (Guohe Chaiqiao): 6 cards
        for (var i = 0; i < 6; i++)
        {
            result.Add("Trick.GuoheChaiqiao");
        }

        // 顺手牵羊 (Shunshou Qianyang): 5 cards
        for (var i = 0; i < 5; i++)
        {
            result.Add("Trick.ShunshouQianyang");
        }

        // 无中生有 (Wuzhong Shengyou): 4 cards
        for (var i = 0; i < 4; i++)
        {
            result.Add("Trick.WuzhongShengyou");
        }

        // 五谷丰登 (Harvest): 2 cards (桃园结义 TaoyuanJieyi is separate, not included in Harvest)
        for (var i = 0; i < 2; i++)
        {
            result.Add("Trick.Harvest");
        }

        // 桃园结义 (TaoyuanJieyi): 1 card
        result.Add("Trick.TaoyuanJieyi");

        // 乐不思蜀 (Lebusishu): 3 cards
        for (var i = 0; i < 3; i++)
        {
            result.Add("Trick.Lebusishu");
        }

        // 南蛮入侵 (Nanman Rushin): 3 cards
        for (var i = 0; i < 3; i++)
        {
            result.Add("Trick.NanmanRushin");
        }

        // 万箭齐发 (Wanjian Qifa): 1 card
        result.Add("Trick.WanjianQifa");

        // 决斗 (Duel): 3 cards
        for (var i = 0; i < 3; i++)
        {
            result.Add("Trick.Duel");
        }

        // 借刀杀人 (Jie Dao Sha Ren): 2 cards
        for (var i = 0; i < 2; i++)
        {
            result.Add("Trick.JieDaoShaRen");
        }

        // 无懈可击 (Wuxiekeji): 4 cards
        for (var i = 0; i < 4; i++)
        {
            result.Add("Trick.Wuxiekeji");
        }

        // 闪电 (Shandian): 1 card
        result.Add("Trick.Shandian");

        // Equipment cards (19 cards total)
        // Weapons (12 cards)
        // 诸葛连弩 (Zhugeliannu): 2 cards
        for (var i = 0; i < 2; i++)
        {
            result.Add("Equip.Zhugeliannu");
        }

        // Single weapons (1 card each)
        result.Add("Equip.CixiongShuanggujian");  // 雌雄双股剑
        result.Add("Equip.HanbingJian");          // 寒冰剑
        result.Add("Equip.QinglongYanyueDao");    // 青龍偃月刀
        result.Add("Equip.QinggangJian");         // 青釭劍
        result.Add("Equip.QilinGong");            // 麒麟弓
        result.Add("Equip.ZhangbaShemao");        // 丈八蛇矛
        result.Add("Equip.Guanshifu");            // 贯石斧
        result.Add("Equip.FangtianHuaji");        // 方天画戟

        // Armor (3 cards)
        // 八卦阵 (Bagua Zhen): 2 cards
        for (var i = 0; i < 2; i++)
        {
            result.Add("Equip.BaguaZhen");
        }

        // 仁王盾 (Renwang Dun): 1 card
        result.Add("Equip.RenwangDun");

        // Horses (4 cards)
        // Offensive horses (-1 distance)
        result.Add("Equip.Jueying");              // 绝影
        result.Add("Equip.Chitu");                // 赤兔
        result.Add("Equip.ZhaohuangFeidian");     // 爪黄飞电
        result.Add("Equip.Dawan");                // 大宛

        // Defensive horses (+1 distance)
        result.Add("Equip.Dilu");                 // 的卢
        result.Add("Equip.Zixing");               // 紫騂

        // Total: 53 (basic) + 36 (trick) + 19 (equipment) = 108 cards
        // Breakdown: 53 basic + 36 trick + 10 weapons + 3 armor + 6 horses = 108 cards
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
    /// Populates the game's draw pile zone with cards based on the shuffled definition IDs.
    /// Uses CardDefinitionService to set correct CardType, CardSubType, and Name for each card.
    /// </summary>
    private static void PopulateDrawPile(Game game, IReadOnlyList<string> shuffledDefinitionIds)
    {
        if (game.DrawPile is not Zone drawZone)
        {
            // In theory DrawPile should always be a concrete Zone, but guard anyway.
            throw new InvalidOperationException("Game.DrawPile must be a mutable Zone.");
        }

        drawZone.MutableCards.Clear();

        var cardDefinitionService = new BasicCardDefinitionService();
        var nextInstanceId = 1;

        foreach (var defId in shuffledDefinitionIds)
        {
            var definition = cardDefinitionService.GetDefinition(defId);

            var card = new Card
            {
                Id = nextInstanceId++,
                DefinitionId = defId,
                Name = definition?.Name ?? defId,
                Suit = definition?.DefaultSuit ?? Suit.Spade,  // Default suit if not specified
                Rank = 1,  // Default rank (actual rank would come from card definition in full implementation)
                CardType = definition?.CardType ?? CardType.Basic,
                CardSubType = definition?.CardSubType ?? CardSubType.Unknown
            };

            drawZone.MutableCards.Add(card);
        }
    }
}

