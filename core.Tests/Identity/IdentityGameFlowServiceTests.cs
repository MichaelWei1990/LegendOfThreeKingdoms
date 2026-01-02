using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Character;
using LegendOfThreeKingdoms.Core.Configuration;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.GameMode;
using LegendOfThreeKingdoms.Core.Identity;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Turns;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests.Identity;

[TestClass]
public sealed class IdentityGameFlowServiceTests
{
    private static GameConfig CreateConfig(int playerCount)
    {
        return CoreApi.CreateDefaultConfig(playerCount);
    }

    private static SkillRegistry CreateSkillRegistry()
    {
        var registry = new SkillRegistry();
        
        // Register test skills
        registry.RegisterSkill("test_skill", new TestSkillFactory());
        
        // Register test heroes
        registry.RegisterHeroSkills("test_hero1", new[] { "test_skill" });
        registry.RegisterHeroSkills("test_hero2", new[] { "test_skill" });
        registry.RegisterHeroSkills("test_hero3", new[] { "test_skill" });
        registry.RegisterHeroSkills("test_hero4", new[] { "test_skill" });
        
        return registry;
    }

    // Test skill implementation
    private sealed class TestSkill : ISkill
    {
        public string Id => "test_skill";
        public string Name => "Test Skill";
        public SkillType Type => SkillType.Locked;
        public SkillCapability Capabilities => SkillCapability.None;

        public bool IsActive(Game game, Player owner)
        {
            // Default: skill is active if owner is alive
            return owner.IsAlive;
        }

        public void Attach(Game game, Player player, IEventBus eventBus)
        {
            // No-op for minimal flow
        }

        public void Detach(Game game, Player player, IEventBus eventBus)
        {
            // No-op for minimal flow
        }
    }

    private sealed class TestSkillFactory : ISkillFactory
    {
        public ISkill CreateSkill() => new TestSkill();
    }

    private static IdentityGameFlowService CreateFlowService(
        IEventBus? eventBus = null,
        IRoleAssignmentService? roleAssignmentService = null,
        ICharacterSelectionService? characterSelectionService = null,
        IWinConditionService? winConditionService = null,
        ITurnEngine? turnEngine = null,
        ITurnExecutor? turnExecutor = null,
        IGameMode? gameMode = null,
        IRandomSource? random = null,
        Func<ChoiceRequest, ChoiceResult>? getPlayerChoice = null)
    {
        eventBus = eventBus ?? new BasicEventBus();
        roleAssignmentService = roleAssignmentService ?? new BasicRoleAssignmentService();
        winConditionService = winConditionService ?? new BasicWinConditionService();
        gameMode = gameMode ?? new StandardGameMode(roleAssignmentService, winConditionService);
        turnEngine = turnEngine ?? new BasicTurnEngine(gameMode, eventBus);
        random = random ?? new SeededRandomSource(42);

        var registry = CreateSkillRegistry();
        var catalog = new BasicCharacterCatalog(registry);
        var skillManager = new SkillManager(registry, eventBus);
        characterSelectionService = characterSelectionService ?? new BasicCharacterSelectionService(catalog, skillManager, eventBus);
        
        // Create dependencies for BasicTurnExecutor if not provided
        if (turnExecutor is null)
        {
            var ruleService = new RuleService(skillManager: skillManager);
            var cardMoveService = new BasicCardMoveService(eventBus);
            var actionMapper = new ActionResolutionMapper();
            
            // Register basic action handlers
            actionMapper.RegisterUseSlashHandler(cardMoveService, ruleService, getPlayerChoice);
            
            // Default getPlayerChoice: return empty choice (for minimal tests)
            getPlayerChoice = getPlayerChoice ?? ((request) => new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: false));
            
            turnExecutor = new BasicTurnExecutor(
                turnEngine: turnEngine,
                ruleService: ruleService,
                actionMapper: actionMapper,
                cardMoveService: cardMoveService,
                getPlayerChoice: getPlayerChoice,
                eventBus: eventBus,
                skillManager: skillManager);
        }

        return new IdentityGameFlowService(
            eventBus,
            roleAssignmentService,
            characterSelectionService,
            winConditionService,
            turnEngine,
            turnExecutor,
            gameMode,
            random);
    }

    [TestMethod]
    public void CreateGame_SetsStateToCreated()
    {
        // Arrange
        var service = CreateFlowService();
        var config = CreateConfig(4);

        // Act
        var game = service.CreateGame(config);

        // Assert
        Assert.AreEqual(GameState.Created, game.State);
        Assert.AreEqual(4, game.Players.Count);
    }

    [TestMethod]
    public void AssignIdentities_SetsStateToIdentitiesAssigned()
    {
        // Arrange
        var service = CreateFlowService();
        var config = CreateConfig(4);
        var game = service.CreateGame(config);

        // Act
        game = service.AssignIdentities(game);

        // Assert
        Assert.AreEqual(GameState.IdentitiesAssigned, game.State);
        var lord = game.Players.FirstOrDefault(p => p.CampId == RoleConstants.Lord);
        Assert.IsNotNull(lord, "Lord should be assigned");
        Assert.IsTrue(lord.RoleRevealed, "Lord's role should be revealed");
    }

    [TestMethod]
    public void LordSelectsHero_UpdatesLordPlayer()
    {
        // Arrange
        var service = CreateFlowService();
        var config = CreateConfig(4);
        var game = service.CreateGame(config);
        game = service.AssignIdentities(game);

        var lord = game.Players.FirstOrDefault(p => p.CampId == RoleConstants.Lord);
        Assert.IsNotNull(lord);

        // Act
        game = service.LordSelectsHero(game, new[] { "test_hero1", "test_hero2" }, "test_hero1");

        // Assert
        var updatedLord = game.Players.FirstOrDefault(p => p.Seat == lord.Seat);
        Assert.IsNotNull(updatedLord);
        Assert.AreEqual("test_hero1", updatedLord.HeroId);
        Assert.IsTrue(updatedLord.MaxHealth > 0);
    }

    [TestMethod]
    public void OtherPlayersSelectHeroes_UpdatesAllPlayers()
    {
        // Arrange
        var service = CreateFlowService();
        var config = CreateConfig(4);
        var game = service.CreateGame(config);
        game = service.AssignIdentities(game);
        game = service.LordSelectsHero(game, new[] { "test_hero1", "test_hero2" }, "test_hero1");

        var heroIds = new[] { "test_hero1", "test_hero2", "test_hero3", "test_hero4" };
        var heroIndex = 0;

        // Act
        game = service.OtherPlayersSelectHeroes(
            game,
            (g, p) => heroIds,
            (g, p) => heroIds[heroIndex++ % heroIds.Length]);

        // Assert
        Assert.AreEqual(GameState.HeroesSelected, game.State);
        foreach (var player in game.Players)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(player.HeroId), $"Player at seat {player.Seat} should have a hero");
        }
    }

    [TestMethod]
    public void StartGame_SetsStateToRunning()
    {
        // Arrange
        var service = CreateFlowService();
        var config = CreateConfig(4);
        var game = service.CreateGame(config);
        game = service.AssignIdentities(game);
        game = service.LordSelectsHero(game, new[] { "test_hero1", "test_hero2" }, "test_hero1");

        var heroIds = new[] { "test_hero1", "test_hero2", "test_hero3", "test_hero4" };
        var heroIndex = 1;
        game = service.OtherPlayersSelectHeroes(
            game,
            (g, p) => heroIds,
            (g, p) => heroIds[heroIndex++ % heroIds.Length]);

        // Act
        game = service.StartGame(game);

        // Assert
        Assert.AreEqual(GameState.Running, game.State);
        var lord = game.Players.FirstOrDefault(p => p.CampId == RoleConstants.Lord);
        Assert.IsNotNull(lord);
        Assert.AreEqual(lord.Seat, game.CurrentPlayerSeat, "First turn should start with Lord");
    }

    [TestMethod]
    public void ExecuteTurnCycle_AdvancesToNextPlayer()
    {
        // Arrange
        var service = CreateFlowService();
        var config = CreateConfig(4);
        var game = service.CreateGame(config);
        game = service.AssignIdentities(game);
        game = service.LordSelectsHero(game, new[] { "test_hero1", "test_hero2" }, "test_hero1");

        var heroIds = new[] { "test_hero1", "test_hero2", "test_hero3", "test_hero4" };
        var heroIndex = 1;
        game = service.OtherPlayersSelectHeroes(
            game,
            (g, p) => heroIds,
            (g, p) => heroIds[heroIndex++ % heroIds.Length]);
        game = service.StartGame(game);

        var initialSeat = game.CurrentPlayerSeat;

        // Act
        game = service.ExecuteTurnCycle(game);

        // Assert
        Assert.AreNotEqual(initialSeat, game.CurrentPlayerSeat, "Turn should advance to next player");
    }

    [TestMethod]
    public void CompleteFlow_FromCreationToEnd()
    {
        // Arrange
        var eventBus = new BasicEventBus();
        var publishedEvents = new List<IGameEvent>();
        
        // Subscribe to all event types we want to track
        eventBus.Subscribe<GameCreatedEvent>(evt => publishedEvents.Add(evt));
        eventBus.Subscribe<IdentitiesAssignedEvent>(evt => publishedEvents.Add(evt));
        eventBus.Subscribe<LordRevealedEvent>(evt => publishedEvents.Add(evt));
        eventBus.Subscribe<GameStartedEvent>(evt => publishedEvents.Add(evt));
        eventBus.Subscribe<TurnStartEvent>(evt => publishedEvents.Add(evt));

        var service = CreateFlowService(eventBus: eventBus);
        var config = CreateConfig(4);
        
        // Step 0: Create game
        var game = service.CreateGame(config);
        Assert.AreEqual(GameState.Created, game.State);

        // Step 1: Assign identities
        game = service.AssignIdentities(game);
        Assert.AreEqual(GameState.IdentitiesAssigned, game.State);

        // Step 2: Lord selects hero
        game = service.LordSelectsHero(game, new[] { "test_hero1", "test_hero2" }, "test_hero1");

        // Step 3: Other players select heroes
        var heroIds = new[] { "test_hero1", "test_hero2", "test_hero3", "test_hero4" };
        var heroIndex = 1;
        game = service.OtherPlayersSelectHeroes(
            game,
            (g, p) => heroIds,
            (g, p) => heroIds[heroIndex++ % heroIds.Length]);
        Assert.AreEqual(GameState.HeroesSelected, game.State);

        // Step 4: Start game
        game = service.StartGame(game);
        Assert.AreEqual(GameState.Running, game.State);

        // Step 5: Execute a few turns
        for (int i = 0; i < 3 && !game.IsFinished; i++)
        {
            game = service.ExecuteTurnCycle(game);
        }

        // Verify events were published
        Assert.IsTrue(publishedEvents.Any(e => e is GameCreatedEvent));
        Assert.IsTrue(publishedEvents.Any(e => e is IdentitiesAssignedEvent));
        Assert.IsTrue(publishedEvents.Any(e => e is LordRevealedEvent));
        Assert.IsTrue(publishedEvents.Any(e => e is GameStartedEvent));
        Assert.IsTrue(publishedEvents.Any(e => e is TurnStartEvent));
    }

    /// <summary>
    /// Tests the complete minimal flow from game creation to game end.
    /// This test verifies the entire identity mode flow:
    /// 1. Create game
    /// 2. Assign identities
    /// 3. Lord selects hero
    /// 4. Other players select heroes
    /// 5. Start game
    /// 6. Execute turns until win condition is met
    /// 7. Verify game ends with correct winner
    /// </summary>
    [TestMethod]
    public void MinimalFlow_FromCreationToGameEnd()
    {
        // Arrange
        var eventBus = new BasicEventBus();
        var publishedEvents = new List<IGameEvent>();
        
        // Subscribe to all event types we want to track
        eventBus.Subscribe<GameCreatedEvent>(evt => publishedEvents.Add(evt));
        eventBus.Subscribe<IdentitiesAssignedEvent>(evt => publishedEvents.Add(evt));
        eventBus.Subscribe<LordRevealedEvent>(evt => publishedEvents.Add(evt));
        eventBus.Subscribe<GameStartedEvent>(evt => publishedEvents.Add(evt));
        eventBus.Subscribe<TurnStartEvent>(evt => publishedEvents.Add(evt));
        eventBus.Subscribe<TurnEndEvent>(evt => publishedEvents.Add(evt));
        eventBus.Subscribe<GameEndedEvent>(evt => publishedEvents.Add(evt));

        var service = CreateFlowService(eventBus: eventBus);
        var config = CreateConfig(4); // 4 players: 1 Lord, 1 Loyalist, 1 Rebel, 1 Renegade
        
        // Step 0: Create game
        var game = service.CreateGame(config);
        Assert.AreEqual(GameState.Created, game.State);
        Assert.AreEqual(4, game.Players.Count);

        // Step 1: Assign identities
        game = service.AssignIdentities(game);
        Assert.AreEqual(GameState.IdentitiesAssigned, game.State);
        
        var lord = game.Players.FirstOrDefault(p => p.CampId == RoleConstants.Lord);
        var loyalist = game.Players.FirstOrDefault(p => p.CampId == RoleConstants.Loyalist);
        var rebel = game.Players.FirstOrDefault(p => p.CampId == RoleConstants.Rebel);
        var renegade = game.Players.FirstOrDefault(p => p.CampId == RoleConstants.Renegade);
        
        Assert.IsNotNull(lord, "Lord should be assigned");
        Assert.IsNotNull(loyalist, "Loyalist should be assigned");
        Assert.IsNotNull(rebel, "Rebel should be assigned");
        Assert.IsNotNull(renegade, "Renegade should be assigned");
        Assert.IsTrue(lord.RoleRevealed, "Lord's role should be revealed");

        // Step 2: Lord selects hero
        game = service.LordSelectsHero(game, new[] { "test_hero1", "test_hero2" }, "test_hero1");
        var updatedLord = game.Players.FirstOrDefault(p => p.CampId == RoleConstants.Lord);
        Assert.IsNotNull(updatedLord);
        Assert.AreEqual("test_hero1", updatedLord.HeroId);

        // Step 3: Other players select heroes
        var heroIds = new[] { "test_hero1", "test_hero2", "test_hero3", "test_hero4" };
        var heroIndex = 1;
        game = service.OtherPlayersSelectHeroes(
            game,
            (g, p) => heroIds,
            (g, p) => heroIds[heroIndex++ % heroIds.Length]);
        Assert.AreEqual(GameState.HeroesSelected, game.State);
        
        foreach (var player in game.Players)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(player.HeroId), 
                $"Player at seat {player.Seat} should have a hero");
        }

        // Step 4: Start game
        game = service.StartGame(game);
        Assert.AreEqual(GameState.Running, game.State);
        Assert.IsFalse(game.IsFinished);
        
        var lordAfterStart = game.Players.FirstOrDefault(p => p.CampId == RoleConstants.Lord);
        Assert.IsNotNull(lordAfterStart);
        Assert.AreEqual(lordAfterStart.Seat, game.CurrentPlayerSeat, 
            "First turn should start with Lord");

        // Step 5: Execute a few turns to verify turn progression
        var initialTurnNumber = game.TurnNumber;
        var initialSeat = game.CurrentPlayerSeat;
        
        game = service.ExecuteTurnCycle(game);
        Assert.IsTrue(game.TurnNumber > initialTurnNumber || game.CurrentPlayerSeat != initialSeat,
            "Turn should advance after execution");
        Assert.IsFalse(game.IsFinished, "Game should not be finished yet");

        // Step 6: Trigger win condition by eliminating all Rebels and Renegades
        // This should result in Lord and Loyalists winning
        var rebelAfterTurn = game.Players.FirstOrDefault(p => p.CampId == RoleConstants.Rebel);
        var renegadeAfterTurn = game.Players.FirstOrDefault(p => p.CampId == RoleConstants.Renegade);
        
        Assert.IsNotNull(rebelAfterTurn);
        Assert.IsNotNull(renegadeAfterTurn);
        
        // Set Rebel and Renegade as dead
        rebelAfterTurn.IsAlive = false;
        rebelAfterTurn.CurrentHealth = 0;
        renegadeAfterTurn.IsAlive = false;
        renegadeAfterTurn.CurrentHealth = 0;

        // Step 7: Execute turn cycles until win condition is checked
        // We may need to execute multiple cycles if current player is dead
        int maxCycles = 10;
        int cyclesExecuted = 0;
        while (!game.IsFinished && cyclesExecuted < maxCycles)
        {
            game = service.ExecuteTurnCycle(game);
            cyclesExecuted++;
        }

        // Verify game ended with correct winner
        Assert.IsTrue(game.IsFinished, "Game should be finished");
        Assert.AreEqual(GameState.Finished, game.State);
        Assert.IsNotNull(game.WinnerDescription);
        Assert.IsFalse(string.IsNullOrWhiteSpace(game.WinnerDescription));

        // Verify events were published throughout the flow
        Assert.IsTrue(publishedEvents.Any(e => e is GameCreatedEvent), 
            "GameCreatedEvent should be published");
        Assert.IsTrue(publishedEvents.Any(e => e is IdentitiesAssignedEvent), 
            "IdentitiesAssignedEvent should be published");
        Assert.IsTrue(publishedEvents.Any(e => e is LordRevealedEvent), 
            "LordRevealedEvent should be published");
        Assert.IsTrue(publishedEvents.Any(e => e is GameStartedEvent), 
            "GameStartedEvent should be published");
        Assert.IsTrue(publishedEvents.Any(e => e is TurnStartEvent), 
            "TurnStartEvent should be published");
        Assert.IsTrue(publishedEvents.Any(e => e is TurnEndEvent), 
            "TurnEndEvent should be published");
        Assert.IsTrue(publishedEvents.Any(e => e is GameEndedEvent), 
            "GameEndedEvent should be published");

        // Verify GameEndedEvent details
        var gameEndedEvent = publishedEvents.OfType<GameEndedEvent>().FirstOrDefault();
        Assert.IsNotNull(gameEndedEvent);
        Assert.AreEqual(WinType.LordAndLoyalists, gameEndedEvent.WinType);
        Assert.IsNotNull(gameEndedEvent.WinningPlayerSeats);
        Assert.IsTrue(gameEndedEvent.WinningPlayerSeats.Count >= 2, 
            "Should have at least Lord and one Loyalist as winners");
    }

    /// <summary>
    /// Tests the minimal flow with Rebel win condition (Lord dies).
    /// This test verifies that when the Lord dies, Rebels win the game.
    /// </summary>
    [TestMethod]
    public void MinimalFlow_RebelWinCondition()
    {
        // Arrange
        var eventBus = new BasicEventBus();
        var publishedEvents = new List<IGameEvent>();
        
        // Subscribe to all event types we want to track
        eventBus.Subscribe<GameCreatedEvent>(evt => publishedEvents.Add(evt));
        eventBus.Subscribe<IdentitiesAssignedEvent>(evt => publishedEvents.Add(evt));
        eventBus.Subscribe<LordRevealedEvent>(evt => publishedEvents.Add(evt));
        eventBus.Subscribe<GameStartedEvent>(evt => publishedEvents.Add(evt));
        eventBus.Subscribe<TurnStartEvent>(evt => publishedEvents.Add(evt));
        eventBus.Subscribe<TurnEndEvent>(evt => publishedEvents.Add(evt));
        eventBus.Subscribe<GameEndedEvent>(evt => publishedEvents.Add(evt));
        
        var service = CreateFlowService(eventBus: eventBus);
        var config = CreateConfig(4);
        
        // Step 0-4: Complete setup
        var game = service.CreateGame(config);
        game = service.AssignIdentities(game);
        game = service.LordSelectsHero(game, new[] { "test_hero1", "test_hero2" }, "test_hero1");
        
        var heroIds = new[] { "test_hero1", "test_hero2", "test_hero3", "test_hero4" };
        var heroIndex = 1;
        game = service.OtherPlayersSelectHeroes(
            game,
            (g, p) => heroIds,
            (g, p) => heroIds[heroIndex++ % heroIds.Length]);
        game = service.StartGame(game);

        // Step 5: Kill the Lord to trigger Rebel win
        var lord = game.Players.FirstOrDefault(p => p.CampId == RoleConstants.Lord);
        Assert.IsNotNull(lord);
        
        lord.IsAlive = false;
        lord.CurrentHealth = 0;

        // Step 6: Execute turn cycles until win condition is checked
        // We may need to execute multiple cycles if current player is dead
        int maxCycles = 10;
        int cyclesExecuted = 0;
        while (!game.IsFinished && cyclesExecuted < maxCycles)
        {
            game = service.ExecuteTurnCycle(game);
            cyclesExecuted++;
        }

        // Verify game ended with Rebel win
        Assert.IsTrue(game.IsFinished);
        Assert.AreEqual(GameState.Finished, game.State);
        
        var gameEndedEvent = publishedEvents.OfType<GameEndedEvent>().FirstOrDefault();
        Assert.IsNotNull(gameEndedEvent);
        Assert.AreEqual(WinType.Rebels, gameEndedEvent.WinType);
    }

    /// <summary>
    /// Tests the minimal flow with Renegade win condition (sole survivor).
    /// This test verifies that when Renegade is the sole survivor, Renegade wins.
    /// </summary>
    [TestMethod]
    public void MinimalFlow_RenegadeWinCondition()
    {
        // Arrange
        var eventBus = new BasicEventBus();
        var publishedEvents = new List<IGameEvent>();
        
        // Subscribe to all event types we want to track
        eventBus.Subscribe<GameCreatedEvent>(evt => publishedEvents.Add(evt));
        eventBus.Subscribe<IdentitiesAssignedEvent>(evt => publishedEvents.Add(evt));
        eventBus.Subscribe<LordRevealedEvent>(evt => publishedEvents.Add(evt));
        eventBus.Subscribe<GameStartedEvent>(evt => publishedEvents.Add(evt));
        eventBus.Subscribe<TurnStartEvent>(evt => publishedEvents.Add(evt));
        eventBus.Subscribe<TurnEndEvent>(evt => publishedEvents.Add(evt));
        eventBus.Subscribe<GameEndedEvent>(evt => publishedEvents.Add(evt));
        
        var service = CreateFlowService(eventBus: eventBus);
        var config = CreateConfig(4);
        
        // Step 0-4: Complete setup
        var game = service.CreateGame(config);
        game = service.AssignIdentities(game);
        game = service.LordSelectsHero(game, new[] { "test_hero1", "test_hero2" }, "test_hero1");
        
        var heroIds = new[] { "test_hero1", "test_hero2", "test_hero3", "test_hero4" };
        var heroIndex = 1;
        game = service.OtherPlayersSelectHeroes(
            game,
            (g, p) => heroIds,
            (g, p) => heroIds[heroIndex++ % heroIds.Length]);
        game = service.StartGame(game);

        // Step 5: Kill all players except Renegade
        var renegade = game.Players.FirstOrDefault(p => p.CampId == RoleConstants.Renegade);
        Assert.IsNotNull(renegade);
        
        foreach (var player in game.Players)
        {
            if (player.Seat != renegade.Seat)
            {
                player.IsAlive = false;
                player.CurrentHealth = 0;
            }
        }

        // Step 6: Execute turn cycles until win condition is checked
        // After killing all other players, ExecuteTurnCycle will check win conditions
        // before executing the turn, so it should end immediately if Renegade is the sole survivor
        // We may need to execute at most 1-2 cycles: one to check win condition, 
        // and possibly one more if current player is not Renegade
        int maxCycles = 3;
        int cyclesExecuted = 0;
        while (!game.IsFinished && cyclesExecuted < maxCycles)
        {
            game = service.ExecuteTurnCycle(game);
            cyclesExecuted++;
        }

        // Verify game ended with Renegade win
        Assert.IsTrue(game.IsFinished);
        Assert.AreEqual(GameState.Finished, game.State);
        
        var gameEndedEvent = publishedEvents.OfType<GameEndedEvent>().FirstOrDefault();
        Assert.IsNotNull(gameEndedEvent);
        Assert.AreEqual(WinType.Renegade, gameEndedEvent.WinType);
        Assert.IsTrue(gameEndedEvent.WinningPlayerSeats.Contains(renegade.Seat));
    }

    /// <summary>
    /// Integration test: Verifies that a complete turn cycle executes all phases correctly.
    /// This test verifies the full flow from Start to End phase.
    /// </summary>
    [TestMethod]
    public void IntegrationTest_CompleteTurnCycle_ExecutesAllPhases()
    {
        // Arrange
        var eventBus = new BasicEventBus();
        var phaseEvents = new List<(Phase Phase, int PlayerSeat)>();
        
        // Subscribe to phase events to track phase progression
        eventBus.Subscribe<PhaseStartEvent>(evt => phaseEvents.Add((evt.Phase, evt.PlayerSeat)));
        
        var service = CreateFlowService(eventBus: eventBus);
        var config = CreateConfig(4); // Identity mode requires at least 4 players
        
        // Complete setup
        var game = service.CreateGame(config);
        game = service.AssignIdentities(game);
        game = service.LordSelectsHero(game, new[] { "test_hero1", "test_hero2" }, "test_hero1");
        
        var heroIds = new[] { "test_hero1", "test_hero2" };
        var heroIndex = 1;
        game = service.OtherPlayersSelectHeroes(
            game,
            (g, p) => heroIds,
            (g, p) => heroIds[heroIndex++ % heroIds.Length]);
        game = service.StartGame(game);

        // Verify initial state
        Assert.AreEqual(GameState.Running, game.State);
        Assert.IsNotNull(game.CurrentPlayerSeat);
        
        var initialPhase = game.CurrentPhase;
        var initialTurnNumber = game.TurnNumber;
        var currentPlayer = game.Players.FirstOrDefault(p => p.Seat == game.CurrentPlayerSeat);
        Assert.IsNotNull(currentPlayer);

        // Act: Execute one complete turn cycle
        game = service.ExecuteTurnCycle(game);

        // Assert: Verify phase progression
        // Should have seen at least Start, Draw, Play, Discard, End phases
        var phasesSeen = phaseEvents.Select(e => e.Phase).Distinct().ToList();
        Assert.IsTrue(phasesSeen.Contains(Phase.Start) || initialPhase == Phase.Start);
        Assert.IsTrue(phasesSeen.Contains(Phase.Draw));
        Assert.IsTrue(phasesSeen.Contains(Phase.Play));
        Assert.IsTrue(phasesSeen.Contains(Phase.Discard));
        Assert.IsTrue(phasesSeen.Contains(Phase.End) || game.CurrentPhase == Phase.Start);
        
        // Verify turn number increased or phase advanced to next player
        Assert.IsTrue(game.TurnNumber > initialTurnNumber || game.CurrentPhase == Phase.Start);
    }

    /// <summary>
    /// Integration test: Verifies that multiple turn cycles can be executed in sequence.
    /// </summary>
    [TestMethod]
    public void IntegrationTest_MultipleTurnCycles_ExecuteCorrectly()
    {
        // Arrange
        var eventBus = new BasicEventBus();
        var turnEndEvents = new List<TurnEndEvent>();
        
        eventBus.Subscribe<TurnEndEvent>(evt => turnEndEvents.Add(evt));
        
        var service = CreateFlowService(eventBus: eventBus);
        var config = CreateConfig(4); // Identity mode requires at least 4 players
        
        // Complete setup
        var game = service.CreateGame(config);
        game = service.AssignIdentities(game);
        game = service.LordSelectsHero(game, new[] { "test_hero1", "test_hero2" }, "test_hero1");
        
        var heroIds = new[] { "test_hero1", "test_hero2" };
        var heroIndex = 1;
        game = service.OtherPlayersSelectHeroes(
            game,
            (g, p) => heroIds,
            (g, p) => heroIds[heroIndex++ % heroIds.Length]);
        game = service.StartGame(game);

        var initialTurnNumber = game.TurnNumber;

        // Act: Execute 3 turn cycles
        for (int i = 0; i < 3 && !game.IsFinished; i++)
        {
            game = service.ExecuteTurnCycle(game);
        }

        // Assert: Should have executed multiple turns
        Assert.IsTrue(game.TurnNumber > initialTurnNumber || turnEndEvents.Count >= 1);
        Assert.IsTrue(turnEndEvents.Count >= 1);
    }
}
