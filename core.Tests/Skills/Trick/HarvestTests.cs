using System;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests;

[TestClass]
public sealed class HarvestTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Card CreateHarvestCard(int id = 1)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "harvest",
            Name = "五谷丰登",
            CardType = CardType.Trick,
            CardSubType = CardSubType.Harvest,
            Suit = Suit.Heart,
            Rank = 3
        };
    }

    private static Card CreateTestCard(int id, CardSubType subType = CardSubType.Slash)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"test_card_{id}",
            CardType = CardType.Basic,
            CardSubType = subType,
            Suit = Suit.Spade,
            Rank = 5
        };
    }

    #region HarvestResolver Tests

    /// <summary>
    /// Tests that HarvestResolver successfully reveals cards and each target gains one card.
    /// Input: Game with 4 players, all alive, source player uses Harvest.
    /// Expected: 4 cards are revealed, each player gains 1 card, no cards remain in pool.
    /// </summary>
    [TestMethod]
    public void HarvestResolverRevealsCardsAndEachPlayerGainsOne()
    {
        // Arrange
        var game = CreateDefaultGame(4);
        var source = game.Players[0];
        var player1 = game.Players[1];
        var player2 = game.Players[2];
        var player3 = game.Players[3];

        // Add cards to draw pile
        var drawPile = (Zone)game.DrawPile;
        for (int i = 100; i < 110; i++)
        {
            drawPile.MutableCards.Add(CreateTestCard(i));
        }

        var initialDrawPileCount = game.DrawPile.Cards.Count;
        var initialSourceHandCount = source.HandZone.Cards.Count;
        var initialPlayer1HandCount = player1.HandZone.Cards.Count;
        var initialPlayer2HandCount = player2.HandZone.Cards.Count;
        var initialPlayer3HandCount = player3.HandZone.Cards.Count;

        var eventBus = new LegendOfThreeKingdoms.Core.Events.BasicEventBus();
        var skillManager = new LegendOfThreeKingdoms.Core.Skills.SkillManager(new LegendOfThreeKingdoms.Core.Skills.SkillRegistry(), eventBus);
        var cardMoveService = new BasicCardMoveService(eventBus);
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: null, // Harvest has no explicit targets
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: null
        );

        // Track card selections for each player
        var selections = new System.Collections.Generic.Dictionary<int, int>();
        var selectionIndex = 0;
        var poolCards = new System.Collections.Generic.List<Card>();

        // Mock GetPlayerChoice to return cards from pool in order
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            // Handle nullification window requests (no response by default)
            if (request.ChoiceType == ChoiceType.SelectCards && 
                request.AllowedCards is not null && 
                request.AllowedCards.Any(c => c.CardSubType == CardSubType.Wuxiekeji))
            {
                // This is a nullification window request, but player chooses not to respond
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null, // No card selected = pass
                    SelectedOptionId: null,
                    Confirmed: null
                );
            }

            // Card selection from pool
            if (request.ChoiceType == ChoiceType.SelectCards && request.AllowedCards is not null)
            {
                if (request.AllowedCards.Count > 0)
                {
                    var selectedCard = request.AllowedCards[selectionIndex % request.AllowedCards.Count];
                    selectionIndex++;
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: new[] { selectedCard.Id },
                        SelectedOptionId: null,
                        Confirmed: null
                    );
                }
            }

            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var context = new ResolutionContext(
            game,
            source,
            Action: null,
            choice,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: getPlayerChoice,
            IntermediateResults: null,
            EventBus: eventBus,
            LogCollector: null,
            SkillManager: skillManager,
            EquipmentSkillRegistry: null,
            JudgementService: null
        );

        var resolver = new HarvestResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);

        // Execute the resolution stack to apply the effects
        while (!stack.IsEmpty)
        {
            var stackResult = stack.Pop();
            Assert.IsTrue(stackResult.Success, "Stack execution should succeed.");
        }

        // Verify each player gained 1 card
        Assert.AreEqual(initialSourceHandCount + 1, source.HandZone.Cards.Count, "Source player should have 1 more card.");
        Assert.AreEqual(initialPlayer1HandCount + 1, player1.HandZone.Cards.Count, "Player 1 should have 1 more card.");
        Assert.AreEqual(initialPlayer2HandCount + 1, player2.HandZone.Cards.Count, "Player 2 should have 1 more card.");
        Assert.AreEqual(initialPlayer3HandCount + 1, player3.HandZone.Cards.Count, "Player 3 should have 1 more card.");

        // Verify draw pile decreased by 4
        Assert.AreEqual(initialDrawPileCount - 4, game.DrawPile.Cards.Count, "Draw pile should have 4 fewer cards.");
    }

    /// <summary>
    /// Tests that HarvestResolver handles nullification for a single target.
    /// Input: Game with 3 players, source uses Harvest, player 1's gain is nullified.
    /// Expected: Source and player 2 gain cards, player 1 does not gain a card, 1 card remains in pool (then discarded).
    /// </summary>
    [TestMethod]
    public void HarvestResolverHandlesSingleTargetNullification()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        var source = game.Players[0];
        var target1 = game.Players[1];
        var target2 = game.Players[2];

        // Add cards to draw pile
        var drawPile = (Zone)game.DrawPile;
        for (int i = 100; i < 110; i++)
        {
            drawPile.MutableCards.Add(CreateTestCard(i));
        }

        // Add Wuxiekeji card to source's hand for nullification
        var wuxiekeji = new Card
        {
            Id = 200,
            DefinitionId = "wuxiekeji",
            Name = "无懈可击",
            CardType = CardType.Trick,
            CardSubType = CardSubType.Wuxiekeji,
            Suit = Suit.Heart,
            Rank = 2
        };
        ((Zone)source.HandZone).MutableCards.Add(wuxiekeji);

        var initialDrawPileCount = game.DrawPile.Cards.Count;
        var initialSourceHandCount = source.HandZone.Cards.Count;
        var initialTarget1HandCount = target1.HandZone.Cards.Count;
        var initialTarget2HandCount = target2.HandZone.Cards.Count;

        var eventBus = new LegendOfThreeKingdoms.Core.Events.BasicEventBus();
        var skillManager = new LegendOfThreeKingdoms.Core.Skills.SkillManager(new LegendOfThreeKingdoms.Core.Skills.SkillRegistry(), eventBus);
        var cardMoveService = new BasicCardMoveService(eventBus);
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: null
        );

        var selectionIndex = 0;
        var target1Nullified = false;

        // Mock GetPlayerChoice
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            if (request.ChoiceType != ChoiceType.SelectCards || request.AllowedCards is null)
            {
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: null
                );
            }

            // Determine if this is a pool selection or nullification request
            // Pool selection: cards in AllowedCards are NOT in the player's hand
            // Nullification: cards in AllowedCards ARE in the player's hand
            var player = game.Players.FirstOrDefault(p => p.Seat == request.PlayerSeat);
            if (player is null)
            {
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: null
                );
            }

            var handCardIds = player.HandZone.Cards?.Select(c => c.Id).ToHashSet() ?? new HashSet<int>();
            var isPoolSelection = request.AllowedCards.Any(card => !handCardIds.Contains(card.Id));

            if (isPoolSelection)
            {
                // This is pool selection - must select a card
                if (request.AllowedCards.Count == 0)
                {
                    // No cards available, return empty selection
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: null,
                        SelectedOptionId: null,
                        Confirmed: null
                    );
                }

                // Skip target1's selection if nullified (shouldn't happen, but handle gracefully)
                if (target1Nullified && request.PlayerSeat == target1.Seat)
                {
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: null,
                        SelectedOptionId: null,
                        Confirmed: null
                    );
                }

                // Select a card from pool
                var selectedCard = request.AllowedCards[selectionIndex % request.AllowedCards.Count];
                selectionIndex++;
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: new[] { selectedCard.Id },
                    SelectedOptionId: null,
                    Confirmed: null
                );
            }
            else
            {
                // This is a nullification window request (cards are from hand)
                if (request.AllowedCards.Any(c => c.CardSubType == CardSubType.Wuxiekeji))
                {
                    // Source uses Wuxiekeji to nullify target1's gain
                    if (request.PlayerSeat == source.Seat && !target1Nullified)
                    {
                        target1Nullified = true;
                        var wuxiekejiCard = request.AllowedCards.FirstOrDefault(c => c.CardSubType == CardSubType.Wuxiekeji);
                        if (wuxiekejiCard is not null)
                        {
                            return new ChoiceResult(
                                RequestId: request.RequestId,
                                PlayerSeat: request.PlayerSeat,
                                SelectedTargetSeats: null,
                                SelectedCardIds: new[] { wuxiekejiCard.Id },
                                SelectedOptionId: null,
                                Confirmed: null
                            );
                        }
                    }
                    // Other players pass (no nullification)
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: null, // Pass
                        SelectedOptionId: null,
                        Confirmed: null
                    );
                }
                else
                {
                    // No Wuxiekeji in AllowedCards, but this is not a pool selection
                    // This shouldn't happen for nullification requests, but handle gracefully
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: null, // Pass
                        SelectedOptionId: null,
                        Confirmed: null
                    );
                }
            }
        };

        var context = new ResolutionContext(
            game,
            source,
            Action: null,
            choice,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: getPlayerChoice,
            IntermediateResults: null,
            EventBus: eventBus,
            LogCollector: null,
            SkillManager: skillManager,
            EquipmentSkillRegistry: null,
            JudgementService: null
        );

        var resolver = new HarvestResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);

        // Execute the resolution stack
        while (!stack.IsEmpty)
        {
            var stackResult = stack.Pop();
            Assert.IsTrue(stackResult.Success, "Stack execution should succeed.");
        }

        // Verify source gained a card from pool but used Wuxiekeji (net: same hand count)
        // Source: gained 1 card from pool, but used Wuxiekeji (discarded), so hand count stays the same
        Assert.AreEqual(initialSourceHandCount, source.HandZone.Cards.Count, "Source player should have same hand count (gained 1 card, but used Wuxiekeji which was discarded).");
        Assert.IsFalse(source.HandZone.Cards.Any(c => c.CardSubType == CardSubType.Wuxiekeji), "Source should not have Wuxiekeji in hand (it was used and discarded).");
        Assert.AreEqual(initialTarget2HandCount + 1, target2.HandZone.Cards.Count, "Target2 should have 1 more card.");

        // Verify target1 did not gain a card (was nullified)
        Assert.AreEqual(initialTarget1HandCount, target1.HandZone.Cards.Count, "Target1 should not have gained a card (nullified).");

        // Verify draw pile decreased by 3 (3 cards revealed, 2 gained, 1 discarded)
        Assert.AreEqual(initialDrawPileCount - 3, game.DrawPile.Cards.Count, "Draw pile should have 3 fewer cards.");
    }

    /// <summary>
    /// Tests that HarvestResolver handles nullification chain (nullify a nullification).
    /// Input: Game with 3 players, source uses Harvest, player 1's gain is nullified, then the nullification is nullified.
    /// Expected: All players gain cards (nullification chain cancels out).
    /// </summary>
    [TestMethod]
    public void HarvestResolverHandlesNullificationChain()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        var source = game.Players[0];
        var target1 = game.Players[1];
        var target2 = game.Players[2];

        // Add cards to draw pile
        var drawPile = (Zone)game.DrawPile;
        for (int i = 100; i < 110; i++)
        {
            drawPile.MutableCards.Add(CreateTestCard(i));
        }

        // Add Wuxiekeji cards to source and target2's hands for nullification chain
        var wuxiekeji1 = new Card
        {
            Id = 200,
            DefinitionId = "wuxiekeji1",
            Name = "无懈可击",
            CardType = CardType.Trick,
            CardSubType = CardSubType.Wuxiekeji,
            Suit = Suit.Heart,
            Rank = 2
        };
        var wuxiekeji2 = new Card
        {
            Id = 201,
            DefinitionId = "wuxiekeji2",
            Name = "无懈可击",
            CardType = CardType.Trick,
            CardSubType = CardSubType.Wuxiekeji,
            Suit = Suit.Spade,
            Rank = 2
        };
        ((Zone)source.HandZone).MutableCards.Add(wuxiekeji1);
        ((Zone)target2.HandZone).MutableCards.Add(wuxiekeji2);

        var initialSourceHandCount = source.HandZone.Cards.Count;
        var initialTarget1HandCount = target1.HandZone.Cards.Count;
        var initialTarget2HandCount = target2.HandZone.Cards.Count;

        var eventBus = new LegendOfThreeKingdoms.Core.Events.BasicEventBus();
        var skillManager = new LegendOfThreeKingdoms.Core.Skills.SkillManager(new LegendOfThreeKingdoms.Core.Skills.SkillRegistry(), eventBus);
        var cardMoveService = new BasicCardMoveService(eventBus);
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: null
        );

        var selectionIndex = 0;
        var nullificationCount = 0;

        // Mock GetPlayerChoice - simulate nullification chain
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            // Handle nullification requests (SelectCards with Wuxiekeji in AllowedCards)
            if (request.ChoiceType == ChoiceType.SelectCards && 
                request.AllowedCards is not null && 
                request.AllowedCards.Any(c => c.CardSubType == CardSubType.Wuxiekeji))
            {
                nullificationCount++;
                // First nullification (source nullifies target1), second nullification (target2 nullifies the nullification)
                // Odd count = nullified, even count = not nullified
                if (nullificationCount % 2 == 1 && request.PlayerSeat == source.Seat)
                {
                    // Source nullifies target1
                    var wuxiekejiCard = request.AllowedCards.FirstOrDefault(c => c.CardSubType == CardSubType.Wuxiekeji);
                    if (wuxiekejiCard is not null)
                    {
                        return new ChoiceResult(
                            RequestId: request.RequestId,
                            PlayerSeat: request.PlayerSeat,
                            SelectedTargetSeats: null,
                            SelectedCardIds: new[] { wuxiekejiCard.Id },
                            SelectedOptionId: null,
                            Confirmed: null
                        );
                    }
                }
                else if (nullificationCount % 2 == 0 && request.PlayerSeat == target2.Seat)
                {
                    // Target2 nullifies the nullification (cancels it out)
                    var wuxiekejiCard = request.AllowedCards.FirstOrDefault(c => c.CardSubType == CardSubType.Wuxiekeji);
                    if (wuxiekejiCard is not null)
                    {
                        return new ChoiceResult(
                            RequestId: request.RequestId,
                            PlayerSeat: request.PlayerSeat,
                            SelectedTargetSeats: null,
                            SelectedCardIds: new[] { wuxiekejiCard.Id },
                            SelectedOptionId: null,
                            Confirmed: null
                        );
                    }
                }
                // Pass (no nullification)
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null, // Pass
                    SelectedOptionId: null,
                    Confirmed: null
                );
            }

            // Card selection from pool
            if (request.ChoiceType == ChoiceType.SelectCards && request.AllowedCards is not null)
            {
                if (request.AllowedCards.Count > 0)
                {
                    var selectedCard = request.AllowedCards[selectionIndex % request.AllowedCards.Count];
                    selectionIndex++;
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: new[] { selectedCard.Id },
                        SelectedOptionId: null,
                        Confirmed: null
                    );
                }
            }

            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var context = new ResolutionContext(
            game,
            source,
            Action: null,
            choice,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: getPlayerChoice,
            IntermediateResults: null,
            EventBus: eventBus,
            LogCollector: null,
            SkillManager: skillManager,
            EquipmentSkillRegistry: null,
            JudgementService: null
        );

        var resolver = new HarvestResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);

        // Execute the resolution stack
        while (!stack.IsEmpty)
        {
            var stackResult = stack.Pop();
            Assert.IsTrue(stackResult.Success, "Stack execution should succeed.");
        }

        // Verify all players gained cards (nullification chain cancelled out)
        Assert.AreEqual(initialSourceHandCount + 1, source.HandZone.Cards.Count, "Source player should have 1 more card.");
        Assert.AreEqual(initialTarget1HandCount + 1, target1.HandZone.Cards.Count, "Target1 should have 1 more card (nullification was nullified).");
        Assert.AreEqual(initialTarget2HandCount + 1, target2.HandZone.Cards.Count, "Target2 should have 1 more card.");
    }

    /// <summary>
    /// Tests that a player can use a Wuxiekeji card gained from Harvest pool immediately.
    /// Input: Game with 2 players, Harvest reveals a Wuxiekeji card, source gains it first,
    /// then uses it to nullify target's gain.
    /// Expected: Source gains Wuxiekeji, then uses it to nullify target, target doesn't gain a card.
    /// </summary>
    [TestMethod]
    public void HarvestResolverAllowsImmediateUseOfWuxiekejiFromPool()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var target = game.Players[1];

        // Add cards to draw pile - first card is Wuxiekeji
        var drawPile = (Zone)game.DrawPile;
        var wuxiekeji = new Card
        {
            Id = 100,
            DefinitionId = "wuxiekeji",
            Name = "无懈可击",
            CardType = CardType.Trick,
            CardSubType = CardSubType.Wuxiekeji,
            Suit = Suit.Heart,
            Rank = 2
        };
        drawPile.MutableCards.Add(wuxiekeji);
        drawPile.MutableCards.Add(CreateTestCard(101));

        var initialSourceHandCount = source.HandZone.Cards.Count;
        var initialTargetHandCount = target.HandZone.Cards.Count;

        var eventBus = new LegendOfThreeKingdoms.Core.Events.BasicEventBus();
        var skillManager = new LegendOfThreeKingdoms.Core.Skills.SkillManager(new LegendOfThreeKingdoms.Core.Skills.SkillRegistry(), eventBus);
        var cardMoveService = new BasicCardMoveService(eventBus);
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: null
        );

        var sourceSelectedWuxiekeji = false;
        var targetNullified = false;

        // Mock GetPlayerChoice - simplified logic similar to passing tests
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            if (request.ChoiceType != ChoiceType.SelectCards || request.AllowedCards is null)
            {
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: null
                );
            }

            // Check if this is a nullification request: AllowedCards contains Wuxiekeji AND all cards are in player's hand
            var player = game.Players.FirstOrDefault(p => p.Seat == request.PlayerSeat);
            if (player is not null)
            {
                var handCardIds = player.HandZone.Cards?.Select(c => c.Id).ToHashSet() ?? new HashSet<int>();
                var hasWuxiekeji = request.AllowedCards.Any(c => c.CardSubType == CardSubType.Wuxiekeji);
                var allCardsInHand = request.AllowedCards.Count > 0 && request.AllowedCards.All(card => handCardIds.Contains(card.Id));
                
                if (hasWuxiekeji && allCardsInHand)
                {
                    // This is a nullification request
                    // Source uses the Wuxiekeji they just gained from pool to nullify target
                    if (request.PlayerSeat == source.Seat && sourceSelectedWuxiekeji && !targetNullified)
                    {
                        targetNullified = true;
                        var wuxiekejiCard = request.AllowedCards.FirstOrDefault(c => c.CardSubType == CardSubType.Wuxiekeji);
                        if (wuxiekejiCard is not null)
                        {
                            return new ChoiceResult(
                                RequestId: request.RequestId,
                                PlayerSeat: request.PlayerSeat,
                                SelectedTargetSeats: null,
                                SelectedCardIds: new[] { wuxiekejiCard.Id },
                                SelectedOptionId: null,
                                Confirmed: null
                            );
                        }
                    }
                    // Other players pass (no nullification)
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: null, // Pass
                        SelectedOptionId: null,
                        Confirmed: null
                    );
                }
            }

            // Card selection from pool (similar to passing test)
            // This handles both pool selection and any other card selection requests
            if (request.AllowedCards.Count > 0)
            {
                // Source selects Wuxiekeji from pool if available, otherwise first card
                if (request.PlayerSeat == source.Seat)
                {
                    if (!sourceSelectedWuxiekeji)
                    {
                        var wuxiekejiCard = request.AllowedCards.FirstOrDefault(c => c.CardSubType == CardSubType.Wuxiekeji);
                        if (wuxiekejiCard is not null)
                        {
                            sourceSelectedWuxiekeji = true;
                            return new ChoiceResult(
                                RequestId: request.RequestId,
                                PlayerSeat: request.PlayerSeat,
                                SelectedTargetSeats: null,
                                SelectedCardIds: new[] { wuxiekejiCard.Id },
                                SelectedOptionId: null,
                                Confirmed: null
                            );
                        }
                    }
                }
                
                // Select first card (for source if Wuxiekeji not found, or for target, or for any other case)
                var selectedCard = request.AllowedCards[0];
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: new[] { selectedCard.Id },
                    SelectedOptionId: null,
                    Confirmed: null
                );
            }

            // Default: return empty selection (pass)
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var context = new ResolutionContext(
            game,
            source,
            Action: null,
            choice,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: getPlayerChoice,
            IntermediateResults: null,
            EventBus: eventBus,
            LogCollector: null,
            SkillManager: skillManager,
            EquipmentSkillRegistry: null,
            JudgementService: null
        );

        var resolver = new HarvestResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);

        // Execute the resolution stack
        while (!stack.IsEmpty)
        {
            var stackResult = stack.Pop();
            Assert.IsTrue(stackResult.Success, "Stack execution should succeed.");
        }

        // Verify source used Wuxiekeji and it was discarded (hand should be empty)
        Assert.AreEqual(initialSourceHandCount, source.HandZone.Cards.Count, "Source should have empty hand (Wuxiekeji was used and discarded).");
        Assert.IsFalse(source.HandZone.Cards.Any(c => c.CardSubType == CardSubType.Wuxiekeji), "Source should not have Wuxiekeji in hand (it was used and discarded).");

        // Verify target did not gain a card (was nullified)
        Assert.AreEqual(initialTargetHandCount, target.HandZone.Cards.Count, "Target should not have gained a card (nullified).");

        // Verify Wuxiekeji is in discard pile
        Assert.IsTrue(game.DiscardPile.Cards.Any(c => c.CardSubType == CardSubType.Wuxiekeji), "Wuxiekeji should be in discard pile after being used.");
    }

    /// <summary>
    /// Tests that HarvestResolver handles target death during resolution.
    /// Input: Game with 3 players, source uses Harvest, target1 dies before their turn.
    /// Expected: Source and target2 gain cards, target1 is skipped, remaining cards discarded.
    /// </summary>
    [TestMethod]
    public void HarvestResolverHandlesTargetDeathDuringResolution()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        var source = game.Players[0];
        var target1 = game.Players[1];
        var target2 = game.Players[2];

        // Add cards to draw pile
        var drawPile = (Zone)game.DrawPile;
        for (int i = 100; i < 110; i++)
        {
            drawPile.MutableCards.Add(CreateTestCard(i));
        }

        var initialSourceHandCount = source.HandZone.Cards.Count;
        var initialTarget1HandCount = target1.HandZone.Cards.Count;
        var initialTarget2HandCount = target2.HandZone.Cards.Count;

        var eventBus = new LegendOfThreeKingdoms.Core.Events.BasicEventBus();
        var skillManager = new LegendOfThreeKingdoms.Core.Skills.SkillManager(new LegendOfThreeKingdoms.Core.Skills.SkillRegistry(), eventBus);
        var cardMoveService = new BasicCardMoveService(eventBus);
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: null
        );

        var sourceGained = false;

        // Mock GetPlayerChoice
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            // Handle nullification window requests (no response by default)
            if (request.ChoiceType == ChoiceType.SelectCards && 
                request.AllowedCards is not null && 
                request.AllowedCards.Any(c => c.CardSubType == CardSubType.Wuxiekeji))
            {
                // This is a nullification window request, but player chooses not to respond
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null, // No card selected = pass
                    SelectedOptionId: null,
                    Confirmed: null
                );
            }

            // After source gains a card, kill target1
            if (request.ChoiceType == ChoiceType.SelectCards && request.PlayerSeat == source.Seat && !sourceGained)
            {
                sourceGained = true;
                // Kill target1 after source gains card
                target1.IsAlive = false;
                target1.CurrentHealth = 0;

                if (request.AllowedCards is not null && request.AllowedCards.Count > 0)
                {
                    var selectedCard = request.AllowedCards[0];
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: new[] { selectedCard.Id },
                        SelectedOptionId: null,
                        Confirmed: null
                    );
                }
            }

            // Target2 selects a card
            if (request.ChoiceType == ChoiceType.SelectCards && request.PlayerSeat == target2.Seat && request.AllowedCards is not null)
            {
                if (request.AllowedCards.Count > 0)
                {
                    var selectedCard = request.AllowedCards[0];
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: new[] { selectedCard.Id },
                        SelectedOptionId: null,
                        Confirmed: null
                    );
                }
            }

            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var context = new ResolutionContext(
            game,
            source,
            Action: null,
            choice,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: getPlayerChoice,
            IntermediateResults: null,
            EventBus: eventBus,
            LogCollector: null,
            SkillManager: skillManager,
            EquipmentSkillRegistry: null,
            JudgementService: null
        );

        var resolver = new HarvestResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);

        // Execute the resolution stack
        while (!stack.IsEmpty)
        {
            var stackResult = stack.Pop();
            Assert.IsTrue(stackResult.Success, "Stack execution should succeed.");
        }

        // Verify source and target2 gained cards
        Assert.AreEqual(initialSourceHandCount + 1, source.HandZone.Cards.Count, "Source player should have 1 more card.");
        Assert.AreEqual(initialTarget2HandCount + 1, target2.HandZone.Cards.Count, "Target2 should have 1 more card.");

        // Verify target1 did not gain a card (was dead)
        Assert.AreEqual(initialTarget1HandCount, target1.HandZone.Cards.Count, "Target1 should not have gained a card (dead).");
    }

    /// <summary>
    /// Tests that HarvestResolver handles insufficient cards in draw pile.
    /// Input: Game with 4 players, but only 2 cards in draw pile.
    /// Expected: Only 2 cards are revealed, 2 players gain cards, no cards remain.
    /// </summary>
    [TestMethod]
    public void HarvestResolverHandlesInsufficientCardsInDrawPile()
    {
        // Arrange
        var game = CreateDefaultGame(4);
        var source = game.Players[0];
        var player1 = game.Players[1];
        var player2 = game.Players[2];
        var player3 = game.Players[3];

        // Add only 2 cards to draw pile (less than 4 players)
        var drawPile = (Zone)game.DrawPile;
        drawPile.MutableCards.Add(CreateTestCard(100));
        drawPile.MutableCards.Add(CreateTestCard(101));

        var initialDrawPileCount = game.DrawPile.Cards.Count;
        var initialSourceHandCount = source.HandZone.Cards.Count;
        var initialPlayer1HandCount = player1.HandZone.Cards.Count;

        var eventBus = new LegendOfThreeKingdoms.Core.Events.BasicEventBus();
        var skillManager = new LegendOfThreeKingdoms.Core.Skills.SkillManager(new LegendOfThreeKingdoms.Core.Skills.SkillRegistry(), eventBus);
        var cardMoveService = new BasicCardMoveService(eventBus);
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: null
        );

        var selectionIndex = 0;

        // Mock GetPlayerChoice
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            // Handle nullification window requests (no response by default)
            if (request.ChoiceType == ChoiceType.SelectCards && 
                request.AllowedCards is not null && 
                request.AllowedCards.Any(c => c.CardSubType == CardSubType.Wuxiekeji))
            {
                // This is a nullification window request, but player chooses not to respond
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null, // No card selected = pass
                    SelectedOptionId: null,
                    Confirmed: null
                );
            }

            // Card selection from pool
            if (request.ChoiceType == ChoiceType.SelectCards && request.AllowedCards is not null)
            {
                if (request.AllowedCards.Count > 0)
                {
                    var selectedCard = request.AllowedCards[selectionIndex % request.AllowedCards.Count];
                    selectionIndex++;
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: new[] { selectedCard.Id },
                        SelectedOptionId: null,
                        Confirmed: null
                    );
                }
            }

            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var context = new ResolutionContext(
            game,
            source,
            Action: null,
            choice,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: getPlayerChoice,
            IntermediateResults: null,
            EventBus: eventBus,
            LogCollector: null,
            SkillManager: skillManager,
            EquipmentSkillRegistry: null,
            JudgementService: null
        );

        var resolver = new HarvestResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);

        // Execute the resolution stack
        while (!stack.IsEmpty)
        {
            var stackResult = stack.Pop();
            Assert.IsTrue(stackResult.Success, "Stack execution should succeed.");
        }

        // Verify only 2 players gained cards (source and player1, in order)
        Assert.AreEqual(initialSourceHandCount + 1, source.HandZone.Cards.Count, "Source player should have 1 more card.");
        Assert.AreEqual(initialPlayer1HandCount + 1, player1.HandZone.Cards.Count, "Player1 should have 1 more card.");

        // Verify player2 and player3 did not gain cards (pool was empty)
        Assert.AreEqual(0, player2.HandZone.Cards.Count, "Player2 should not have gained a card (pool empty).");
        Assert.AreEqual(0, player3.HandZone.Cards.Count, "Player3 should not have gained a card (pool empty).");

        // Verify draw pile is empty
        Assert.AreEqual(0, game.DrawPile.Cards.Count, "Draw pile should be empty.");
    }

    #endregion
}
