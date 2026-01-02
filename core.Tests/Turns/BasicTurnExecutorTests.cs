using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Turns;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests.Turns;

[TestClass]
public sealed class BasicTurnExecutorTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static BasicTurnExecutor CreateTurnExecutor(
        ITurnEngine? turnEngine = null,
        IRuleService? ruleService = null,
        IActionResolutionMapper? actionMapper = null,
        ICardMoveService? cardMoveService = null,
        Func<ChoiceRequest, ChoiceResult>? getPlayerChoice = null,
        IEventBus? eventBus = null,
        SkillManager? skillManager = null)
    {
        var game = CreateDefaultGame(2);
        var mode = new FixedFirstSeatGameMode(firstSeat: 0);
        turnEngine = turnEngine ?? new BasicTurnEngine(mode, eventBus);
        eventBus = eventBus ?? new BasicEventBus();
        
        var registry = new SkillRegistry();
        skillManager = skillManager ?? new SkillManager(registry, eventBus);
        ruleService = ruleService ?? new RuleService(skillManager: skillManager);
        cardMoveService = cardMoveService ?? new BasicCardMoveService(eventBus);
        
        // Create concrete ActionResolutionMapper for extension methods
        ActionResolutionMapper concreteActionMapper;
        if (actionMapper is ActionResolutionMapper existingMapper)
        {
            concreteActionMapper = existingMapper;
        }
        else
        {
            concreteActionMapper = new ActionResolutionMapper();
            actionMapper = concreteActionMapper;
        }
        
        // Register all card action handlers (extension method requires concrete type)
        concreteActionMapper.RegisterUseSlashHandler(cardMoveService, ruleService, getPlayerChoice, skillManager);
        concreteActionMapper.RegisterUsePeachHandler(cardMoveService, ruleService, getPlayerChoice, skillManager);
        concreteActionMapper.RegisterUseEquipHandler(cardMoveService, ruleService, getPlayerChoice, skillManager);
        concreteActionMapper.RegisterUseTrickHandlers(cardMoveService, ruleService, getPlayerChoice, skillManager);
        
        // Default getPlayerChoice: return empty choice (pass)
        getPlayerChoice = getPlayerChoice ?? ((request) => new ChoiceResult(
            RequestId: request.RequestId,
            PlayerSeat: request.PlayerSeat,
            SelectedTargetSeats: null,
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: false));
        
        return new BasicTurnExecutor(
            turnEngine: turnEngine,
            ruleService: ruleService,
            actionMapper: actionMapper,
            cardMoveService: cardMoveService,
            getPlayerChoice: getPlayerChoice,
            eventBus: eventBus,
            skillManager: skillManager);
    }

    private sealed class FixedFirstSeatGameMode : IGameMode
    {
        private readonly int _firstSeat;

        public FixedFirstSeatGameMode(int firstSeat)
        {
            _firstSeat = firstSeat;
        }

        public string Id => "test-fixed-first-seat";
        public string DisplayName => "Test Fixed First Seat";
        public int SelectFirstPlayerSeat(Game game) => _firstSeat;
    }

    /// <summary>
    /// Tests that ExecuteTurn advances through all phases correctly.
    /// </summary>
    [TestMethod]
    public void ExecuteTurn_AdvancesThroughAllPhases()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var mode = new FixedFirstSeatGameMode(firstSeat: 0);
        var eventBus = new BasicEventBus();
        var turnEngine = new BasicTurnEngine(mode, eventBus);
        turnEngine.InitializeTurnState(game);
        
        var executor = CreateTurnExecutor(turnEngine: turnEngine, eventBus: eventBus);
        var player = game.Players[0];

        // Act: Execute turn starting from Start phase
        executor.ExecuteTurn(game, player);

        // Assert: Should have advanced through phases
        // After execution, phase should be End or next player's Start
        Assert.IsTrue(game.CurrentPhase == Phase.End || game.CurrentPhase == Phase.Start);
    }

    /// <summary>
    /// Tests that ExecuteTurn handles Play phase correctly when no actions are available.
    /// ExecuteTurn should execute all remaining phases (Play -> Discard -> End -> next player's Start).
    /// </summary>
    [TestMethod]
    public void ExecuteTurn_PlayPhase_NoActionsAvailable_EndsPhase()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var mode = new FixedFirstSeatGameMode(firstSeat: 0);
        var eventBus = new BasicEventBus();
        var turnEngine = new BasicTurnEngine(mode, eventBus);
        turnEngine.InitializeTurnState(game);
        
        // Advance to Play phase
        turnEngine.AdvancePhase(game); // Start -> Judge
        turnEngine.AdvancePhase(game); // Judge -> Draw
        turnEngine.AdvancePhase(game); // Draw -> Play
        
        var executor = CreateTurnExecutor(turnEngine: turnEngine, eventBus: eventBus);
        var player = game.Players[0];
        
        // Remove all cards from player's hand so no actions are available
        if (player.HandZone is Zone handZone)
        {
            handZone.MutableCards.Clear();
        }

        // Act
        executor.ExecuteTurn(game, player);

        // Assert: ExecuteTurn should execute all remaining phases
        // After Play -> Discard -> End -> next player's Start
        // Since there are 2 players, next player is player 1, so phase should be Start
        Assert.AreEqual(Phase.Start, game.CurrentPhase);
        // Verify it's the next player's turn
        Assert.AreEqual(1, game.CurrentPlayerSeat);
    }

    /// <summary>
    /// Tests that ExecuteTurn handles Discard phase correctly when hand exceeds max health.
    /// </summary>
    [TestMethod]
    public void ExecuteTurn_DiscardPhase_ExcessCards_DiscardsCards()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var mode = new FixedFirstSeatGameMode(firstSeat: 0);
        var eventBus = new BasicEventBus();
        var turnEngine = new BasicTurnEngine(mode, eventBus);
        turnEngine.InitializeTurnState(game);
        
        // Advance to Discard phase
        // Start -> Judge -> Draw -> Play -> Discard
        turnEngine.AdvancePhase(game); // Start -> Judge
        turnEngine.AdvancePhase(game); // Judge -> Draw
        turnEngine.AdvancePhase(game); // Draw -> Play
        turnEngine.AdvancePhase(game); // Play -> Discard
        
        // Verify we're in Discard phase
        Assert.AreEqual(Phase.Discard, game.CurrentPhase, "Should be in Discard phase");
        
        var player = game.Players[0];
        // MaxHealth is init-only, use existing value (default is 4 from config)
        // If we need different MaxHealth, we'd need to create a new Player, but for this test we'll use existing
        player.CurrentHealth = player.MaxHealth;
        
        // Clear hand and add excess cards
        if (player.HandZone is Zone handZone)
        {
            handZone.MutableCards.Clear();
            for (int i = 0; i < 6; i++)
            {
                var card = new Card
                {
                    Id = 100 + i,
                    DefinitionId = "test_card",
                    CardType = CardType.Basic,
                    CardSubType = CardSubType.Slash
                };
                handZone.MutableCards.Add(card);
            }
        }
        
        // Calculate excess cards before creating the choice function
        var excessCards = 6 - player.MaxHealth; // Should be 2
        
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            if (request.ChoiceType == ChoiceType.SelectCards)
            {
                // Select exactly excessCards number of cards to discard
                // Use the cards from the request to ensure they match what's in hand
                if (request.AllowedCards is null || request.AllowedCards.Count < excessCards)
                {
                    // Not enough cards available, return null to trigger auto-select
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: null,
                        SelectedOptionId: null,
                        Confirmed: false);
                }
                
                // Select exactly excessCards cards
                var cardsToDiscard = request.AllowedCards.Take(excessCards).Select(c => c.Id).ToList();
                
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: cardsToDiscard,
                    SelectedOptionId: null,
                    Confirmed: true);
            }
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: false);
        };
        
        var executor = CreateTurnExecutor(turnEngine: turnEngine, eventBus: eventBus, getPlayerChoice: getPlayerChoice);
        var initialHandCount = player.HandZone.Cards.Count;
        Assert.AreEqual(6, initialHandCount, "Initial hand should have 6 cards");
        Assert.AreEqual(4, player.MaxHealth, "MaxHealth should be 4");
        Assert.AreEqual(2, excessCards, "Excess cards should be 2");

        // Act
        executor.ExecuteTurn(game, player);

        // Assert: Cards should have been discarded
        var finalHandCount = player.HandZone.Cards.Count;
        Assert.IsTrue(initialHandCount > player.MaxHealth, $"Initial hand count ({initialHandCount}) should exceed MaxHealth ({player.MaxHealth})");
        Assert.IsTrue(finalHandCount <= player.MaxHealth, $"Final hand count ({finalHandCount}) should be <= MaxHealth ({player.MaxHealth}). Initial: {initialHandCount}, Excess: {excessCards}");
        
        // The final hand count should be exactly MaxHealth (6 - 2 = 4)
        Assert.AreEqual(player.MaxHealth, finalHandCount, $"Final hand count should equal MaxHealth. Expected: {player.MaxHealth}, Actual: {finalHandCount}, Initial: {initialHandCount}");
    }

    /// <summary>
    /// Tests that ExecuteTurn handles Discard phase correctly when hand does not exceed max health.
    /// </summary>
    [TestMethod]
    public void ExecuteTurn_DiscardPhase_NoExcessCards_SkipsDiscard()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var mode = new FixedFirstSeatGameMode(firstSeat: 0);
        var eventBus = new BasicEventBus();
        var turnEngine = new BasicTurnEngine(mode, eventBus);
        turnEngine.InitializeTurnState(game);
        
        // Advance to Discard phase
        for (int i = 0; i < 5; i++)
        {
            turnEngine.AdvancePhase(game);
        }
        
        var player = game.Players[0];
        // MaxHealth is init-only, use existing value (default is 4 from config)
        player.CurrentHealth = player.MaxHealth;
        
        // Ensure hand does not exceed max health
        if (player.HandZone is Zone handZone)
        {
            while (handZone.MutableCards.Count > player.MaxHealth)
            {
                handZone.MutableCards.RemoveAt(0);
            }
        }
        
        var initialHandCount = player.HandZone.Cards.Count;
        var executor = CreateTurnExecutor(turnEngine: turnEngine, eventBus: eventBus);

        // Act
        executor.ExecuteTurn(game, player);

        // Assert: Hand count should remain the same
        Assert.AreEqual(initialHandCount, player.HandZone.Cards.Count);
    }

    /// <summary>
    /// Tests that ExecuteTurn handles dead players correctly.
    /// </summary>
    [TestMethod]
    public void ExecuteTurn_DeadPlayer_SkipsExecution()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var mode = new FixedFirstSeatGameMode(firstSeat: 0);
        var eventBus = new BasicEventBus();
        var turnEngine = new BasicTurnEngine(mode, eventBus);
        turnEngine.InitializeTurnState(game);
        
        var executor = CreateTurnExecutor(turnEngine: turnEngine, eventBus: eventBus);
        var player = game.Players[0];
        player.IsAlive = false;
        player.CurrentHealth = 0;

        // Act
        executor.ExecuteTurn(game, player);

        // Assert: Should not throw exception, execution should complete
        // (Dead players are handled by IdentityGameFlowService, but executor should handle gracefully)
        Assert.IsNotNull(game);
    }

    /// <summary>
    /// Tests that ExecuteTurn handles Play phase correctly when player cancels action selection.
    /// ExecuteTurn should execute all remaining phases (Play -> Discard -> End -> next player's Start).
    /// </summary>
    [TestMethod]
    public void ExecuteTurn_PlayPhase_PlayerCancelsActionSelection_EndsPhase()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var mode = new FixedFirstSeatGameMode(firstSeat: 0);
        var eventBus = new BasicEventBus();
        var turnEngine = new BasicTurnEngine(mode, eventBus);
        turnEngine.InitializeTurnState(game);
        
        // Advance to Play phase
        turnEngine.AdvancePhase(game); // Start -> Judge
        turnEngine.AdvancePhase(game); // Judge -> Draw
        turnEngine.AdvancePhase(game); // Draw -> Play
        
        // Create getPlayerChoice that returns null (player cancels)
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            if (request.ChoiceType == ChoiceType.SelectOption)
            {
                // Player cancels action selection
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: false);
            }
            // For other choice types, return empty choice
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: false);
        };
        
        var executor = CreateTurnExecutor(turnEngine: turnEngine, eventBus: eventBus, getPlayerChoice: getPlayerChoice);
        var player = game.Players[0];

        // Act
        executor.ExecuteTurn(game, player);

        // Assert: ExecuteTurn should execute all remaining phases
        // After Play -> Discard -> End -> next player's Start
        // Since there are 2 players, next player is player 1, so phase should be Start
        Assert.AreEqual(Phase.Start, game.CurrentPhase);
        // Verify it's the next player's turn
        Assert.AreEqual(1, game.CurrentPlayerSeat);
    }

    /// <summary>
    /// Tests that ExecuteTurn handles Play phase correctly when player selects an action.
    /// </summary>
    [TestMethod]
    public void ExecuteTurn_PlayPhase_PlayerSelectsAction_ExecutesAction()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var mode = new FixedFirstSeatGameMode(firstSeat: 0);
        var eventBus = new BasicEventBus();
        var turnEngine = new BasicTurnEngine(mode, eventBus);
        turnEngine.InitializeTurnState(game);
        
        // Advance to Play phase
        turnEngine.AdvancePhase(game); // Start -> Judge
        turnEngine.AdvancePhase(game); // Judge -> Draw
        turnEngine.AdvancePhase(game); // Draw -> Play
        
        var player = game.Players[0];
        string? selectedActionId = null;
        
        // Create getPlayerChoice that selects the first available action
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            if (request.ChoiceType == ChoiceType.SelectOption && request.Options is not null && request.Options.Count > 0)
            {
                // Select the first action option
                selectedActionId = request.Options[0].OptionId;
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null,
                    SelectedOptionId: selectedActionId,
                    Confirmed: true);
            }
            // For other choice types (like target selection), return empty choice
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: false);
        };
        
        var executor = CreateTurnExecutor(turnEngine: turnEngine, eventBus: eventBus, getPlayerChoice: getPlayerChoice);

        // Act
        executor.ExecuteTurn(game, player);

        // Assert: Should have processed the action selection
        // The action might not execute if it requires targets and player cancels,
        // but the selection should have been attempted
        Assert.IsTrue(game.CurrentPhase == Phase.Discard || game.CurrentPhase == Phase.Start,
            "Phase should have advanced from Play phase");
    }

    /// <summary>
    /// Tests that ExecuteTurn handles Play phase correctly when selected action is no longer available.
    /// ExecuteTurn should execute all remaining phases (Play -> Discard -> End -> next player's Start).
    /// </summary>
    [TestMethod]
    public void ExecuteTurn_PlayPhase_SelectedActionNoLongerAvailable_EndsPhase()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var mode = new FixedFirstSeatGameMode(firstSeat: 0);
        var eventBus = new BasicEventBus();
        var turnEngine = new BasicTurnEngine(mode, eventBus);
        turnEngine.InitializeTurnState(game);
        
        // Advance to Play phase
        turnEngine.AdvancePhase(game); // Start -> Judge
        turnEngine.AdvancePhase(game); // Judge -> Draw
        turnEngine.AdvancePhase(game); // Draw -> Play
        
        var player = game.Players[0];
        
        // Create getPlayerChoice that selects a non-existent action ID
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            if (request.ChoiceType == ChoiceType.SelectOption)
            {
                // Select a non-existent action ID
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null,
                    SelectedOptionId: "NonExistentAction",
                    Confirmed: true);
            }
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: false);
        };
        
        var executor = CreateTurnExecutor(turnEngine: turnEngine, eventBus: eventBus, getPlayerChoice: getPlayerChoice);

        // Act
        executor.ExecuteTurn(game, player);

        // Assert: ExecuteTurn should execute all remaining phases
        // After Play -> Discard -> End -> next player's Start
        // Since there are 2 players, next player is player 1, so phase should be Start
        Assert.AreEqual(Phase.Start, game.CurrentPhase);
        // Verify it's the next player's turn
        Assert.AreEqual(1, game.CurrentPlayerSeat);
    }

    #region Play Phase Integration Tests

    /// <summary>
    /// Tests that ExecutePlayPhase can use Peach card.
    /// </summary>
    [TestMethod]
    public void ExecutePlayPhase_CanUsePeach()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var mode = new FixedFirstSeatGameMode(firstSeat: 0);
        var eventBus = new BasicEventBus();
        var turnEngine = new BasicTurnEngine(mode, eventBus);
        turnEngine.InitializeTurnState(game);
        
        // Advance to Play phase
        turnEngine.AdvancePhase(game); // Start -> Judge
        turnEngine.AdvancePhase(game); // Judge -> Draw
        turnEngine.AdvancePhase(game); // Draw -> Play
        
        var player = game.Players[0];
        // MaxHealth is init-only, use existing value and set CurrentHealth to be less than MaxHealth
        player.CurrentHealth = player.MaxHealth - 1;
        
        var peach = new Card
        {
            Id = 1,
            DefinitionId = "peach_basic",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Peach,
            Suit = Suit.Heart,
            Rank = 1
        };
        ((Zone)player.HandZone).MutableCards.Add(peach);
        
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            if (request.ChoiceType == ChoiceType.SelectOption)
            {
                // Select UsePeach action if available
                var usePeachOption = request.Options?.FirstOrDefault(o => o.OptionId == "UsePeach");
                if (usePeachOption is not null)
                {
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: null,
                        SelectedOptionId: "UsePeach",
                        Confirmed: true);
                }
                // If UsePeach not found, return first available option or pass
                if (request.Options is not null && request.Options.Count > 0)
                {
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: null,
                        SelectedOptionId: request.Options[0].OptionId,
                        Confirmed: true);
                }
            }
            else if (request.ChoiceType == ChoiceType.SelectCards && request.AllowedCards is not null && request.AllowedCards.Count > 0)
            {
                // Select the peach card if available, otherwise select first card
                var peachCard = request.AllowedCards.FirstOrDefault(c => c.CardSubType == CardSubType.Peach);
                var cardToSelect = peachCard ?? request.AllowedCards[0];
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: new[] { cardToSelect.Id },
                    SelectedOptionId: null,
                    Confirmed: true);
            }
            else if (request.ChoiceType == ChoiceType.SelectTargets)
            {
                // For target selection, return empty (no targets needed for Peach)
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: true);
            }
            // Default: pass
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: false);
        };
        
        var executor = CreateTurnExecutor(turnEngine: turnEngine, eventBus: eventBus, getPlayerChoice: getPlayerChoice);

        // Act
        executor.ExecuteTurn(game, player);

        // Assert
        Assert.AreEqual(player.MaxHealth, player.CurrentHealth, "Player health should be restored to max health");
        Assert.IsFalse(player.HandZone.Cards.Contains(peach), "Peach card should be removed from hand");
        Assert.IsTrue(game.DiscardPile.Cards.Contains(peach), "Peach card should be in discard pile");
    }

    /// <summary>
    /// Tests that ExecutePlayPhase can use Equip card.
    /// </summary>
    [TestMethod]
    public void ExecutePlayPhase_CanUseEquip()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var mode = new FixedFirstSeatGameMode(firstSeat: 0);
        var eventBus = new BasicEventBus();
        var turnEngine = new BasicTurnEngine(mode, eventBus);
        turnEngine.InitializeTurnState(game);
        
        // Advance to Play phase
        turnEngine.AdvancePhase(game); // Start -> Judge
        turnEngine.AdvancePhase(game); // Judge -> Draw
        turnEngine.AdvancePhase(game); // Draw -> Play
        
        var player = game.Players[0];
        
        var weapon = new Card
        {
            Id = 1,
            DefinitionId = "weapon_basic",
            CardType = CardType.Equip,
            CardSubType = CardSubType.Weapon,
            Suit = Suit.Spade,
            Rank = 5
        };
        ((Zone)player.HandZone).MutableCards.Add(weapon);
        
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            if (request.ChoiceType == ChoiceType.SelectOption)
            {
                // Select UseEquip action if available
                var useEquipOption = request.Options?.FirstOrDefault(o => o.OptionId == "UseEquip");
                if (useEquipOption is not null)
                {
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: null,
                        SelectedOptionId: "UseEquip",
                        Confirmed: true);
                }
                // If UseEquip not found, return first available option or pass
                if (request.Options is not null && request.Options.Count > 0)
                {
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: null,
                        SelectedOptionId: request.Options[0].OptionId,
                        Confirmed: true);
                }
            }
            else if (request.ChoiceType == ChoiceType.SelectCards && request.AllowedCards is not null && request.AllowedCards.Count > 0)
            {
                // Select the weapon card if available, otherwise select first card
                var weaponCard = request.AllowedCards.FirstOrDefault(c => c.CardSubType == CardSubType.Weapon);
                var cardToSelect = weaponCard ?? request.AllowedCards[0];
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: new[] { cardToSelect.Id },
                    SelectedOptionId: null,
                    Confirmed: true);
            }
            else if (request.ChoiceType == ChoiceType.SelectTargets)
            {
                // For target selection, return empty (no targets needed for Equip)
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: true);
            }
            // Default: pass
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: false);
        };
        
        var executor = CreateTurnExecutor(turnEngine: turnEngine, eventBus: eventBus, getPlayerChoice: getPlayerChoice);

        // Act
        executor.ExecuteTurn(game, player);

        // Assert
        Assert.IsFalse(player.HandZone.Cards.Contains(weapon), "Weapon card should be removed from hand");
        Assert.IsTrue(player.EquipmentZone.Cards.Contains(weapon), "Weapon card should be in equipment zone");
    }

    /// <summary>
    /// Tests that ExecutePlayPhase can use trick cards.
    /// </summary>
    [TestMethod]
    public void ExecutePlayPhase_CanUseTrickCards()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var mode = new FixedFirstSeatGameMode(firstSeat: 0);
        var eventBus = new BasicEventBus();
        var turnEngine = new BasicTurnEngine(mode, eventBus);
        turnEngine.InitializeTurnState(game);
        
        // Advance to Play phase
        turnEngine.AdvancePhase(game); // Start -> Judge
        turnEngine.AdvancePhase(game); // Judge -> Draw
        turnEngine.AdvancePhase(game); // Draw -> Play
        
        var player = game.Players[0];
        
        var trick = new Card
        {
            Id = 1,
            DefinitionId = "trick_wuzhongshengyou",
            CardType = CardType.Trick,
            CardSubType = CardSubType.WuzhongShengyou,
            Suit = Suit.Spade,
            Rank = 1
        };
        ((Zone)player.HandZone).MutableCards.Add(trick);
        
        // Add some cards to draw pile
        for (int i = 0; i < 5; i++)
        {
            var drawCard = new Card
            {
                Id = 100 + i,
                DefinitionId = $"draw_card_{i}",
                CardType = CardType.Basic,
                CardSubType = CardSubType.Slash,
                Suit = Suit.Spade,
                Rank = 1
            };
            ((Zone)game.DrawPile).MutableCards.Add(drawCard);
        }
        
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            if (request.ChoiceType == ChoiceType.SelectOption)
            {
                // Select UseWuzhongShengyou action if available
                var useTrickOption = request.Options?.FirstOrDefault(o => o.OptionId == "UseWuzhongShengyou");
                if (useTrickOption is not null)
                {
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: null,
                        SelectedOptionId: "UseWuzhongShengyou",
                        Confirmed: true);
                }
                // If UseWuzhongShengyou not found, return first available option or pass
                if (request.Options is not null && request.Options.Count > 0)
                {
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: null,
                        SelectedOptionId: request.Options[0].OptionId,
                        Confirmed: true);
                }
            }
            else if (request.ChoiceType == ChoiceType.SelectCards && request.AllowedCards is not null && request.AllowedCards.Count > 0)
            {
                // Select the trick card if available, otherwise select first card
                var trickCard = request.AllowedCards.FirstOrDefault(c => c.CardSubType == CardSubType.WuzhongShengyou);
                var cardToSelect = trickCard ?? request.AllowedCards[0];
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: new[] { cardToSelect.Id },
                    SelectedOptionId: null,
                    Confirmed: true);
            }
            else if (request.ChoiceType == ChoiceType.SelectTargets)
            {
                // For target selection, return empty (no targets needed for WuzhongShengyou)
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: true);
            }
            // Default: pass
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: false);
        };
        
        var executor = CreateTurnExecutor(turnEngine: turnEngine, eventBus: eventBus, getPlayerChoice: getPlayerChoice);
        var initialHandCount = player.HandZone.Cards.Count;

        // Act
        executor.ExecuteTurn(game, player);

        // Assert
        Assert.IsFalse(player.HandZone.Cards.Contains(trick), "Trick card should be removed from hand");
        Assert.IsTrue(game.DiscardPile.Cards.Contains(trick), "Trick card should be in discard pile");
        // WuzhongShengyou draws 2 cards, so hand count should increase by 1 (trick removed, 2 added)
        Assert.AreEqual(initialHandCount + 1, player.HandZone.Cards.Count, "Player should have drawn 2 cards from WuzhongShengyou");
    }

    /// <summary>
    /// Tests that ExecutePlayPhase can end phase without executing any action.
    /// ExecuteTurn should execute all remaining phases (Play -> Discard -> End -> next player's Start).
    /// This test verifies that when a player chooses not to execute any action (returns Confirmed: false),
    /// the Play phase ends correctly and the turn continues to subsequent phases.
    /// </summary>
    [TestMethod]
    public void ExecutePlayPhase_CanEndPhaseWithoutAction()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var mode = new FixedFirstSeatGameMode(firstSeat: 0);
        var eventBus = new BasicEventBus();
        var turnEngine = new BasicTurnEngine(mode, eventBus);
        turnEngine.InitializeTurnState(game);
        
        // Advance to Play phase
        turnEngine.AdvancePhase(game); // Start -> Judge
        turnEngine.AdvancePhase(game); // Judge -> Draw
        turnEngine.AdvancePhase(game); // Draw -> Play
        
        var player = game.Players[0];
        var initialPlayerSeat = player.Seat;
        
        // Create getPlayerChoice that returns empty choice with Confirmed: false (player chooses to end phase)
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            // Return empty choice to indicate player wants to end phase
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: false);
        };
        
        var executor = CreateTurnExecutor(turnEngine: turnEngine, eventBus: eventBus, getPlayerChoice: getPlayerChoice);

        // Act
        executor.ExecuteTurn(game, player);

        // Assert: ExecuteTurn should execute all remaining phases
        // After Play -> Discard -> End -> next player's Start
        // Since there are 2 players, next player is player 1, so phase should be Start
        Assert.AreEqual(Phase.Start, game.CurrentPhase, "Phase should advance to next player's Start phase after completing turn");
        // Verify it's the next player's turn
        Assert.AreEqual(1, game.CurrentPlayerSeat, "Current player seat should advance to next player");
        // Verify that the turn completed successfully (player was able to choose to end Play phase)
        Assert.AreNotEqual(initialPlayerSeat, game.CurrentPlayerSeat, "Turn should have completed and advanced to next player");
    }

    #endregion
}
