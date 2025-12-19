using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;

namespace core.Tests;

[TestClass]
public sealed class ResolutionTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Card CreateSlashCard(int id = 1)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "slash_basic",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = Suit.Spade,
            Rank = 7
        };
    }

    /// <summary>
    /// Verifies that BasicResolutionStack correctly manages resolver execution order and history tracking.
    /// 
    /// Test scenario:
    /// - Creates a new resolution stack
    /// - Pushes a UseCardResolver onto the stack
    /// - Verifies the stack is not empty after pushing
    /// - Pops and executes the resolver
    /// - Verifies the stack is empty after popping
    /// - Verifies the execution history contains the executed resolver
    /// 
    /// Expected: The stack correctly tracks resolver execution order and maintains a history
    /// of completed resolvers for debugging and logging purposes.
    /// </summary>
    [TestMethod]
    public void basicResolutionStackManagesExecutionOrder()
    {
        var stack = new BasicResolutionStack();
        var game = CreateDefaultGame();
        var player = game.Players[0];
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();

        var context = new ResolutionContext(
            game,
            player,
            null,
            null,
            stack,
            cardMoveService,
            ruleService
        );

        // Push a resolver
        var resolver = new UseCardResolver();
        stack.Push(resolver, context);

        Assert.IsFalse(stack.IsEmpty);

        // Pop and execute
        var result = stack.Pop();

        Assert.IsTrue(stack.IsEmpty);
        Assert.IsNotNull(result);

        // Check history
        var history = stack.GetHistory();
        Assert.AreEqual(1, history.Count);
        Assert.AreEqual(typeof(UseCardResolver), history[0].ResolverType);
    }

    /// <summary>
    /// Verifies that UseCardResolver successfully processes a valid Slash usage through the complete flow.
    /// 
    /// Test scenario:
    /// - Sets up a 2-player game in Play phase
    /// - Source player has a Slash card in hand
    /// - Creates a UseSlash action with valid target selection
    /// - Executes UseCardResolver with the action and choice
    /// 
    /// Expected results:
    /// - Resolution succeeds
    /// - Slash card is moved from source player's hand to the discard pile
    /// - SlashResolver is pushed onto the stack for further processing
    /// 
    /// This test verifies the core functionality of UseCardResolver: validation, card movement,
    /// and delegation to specific resolvers based on card type.
    /// </summary>
    [TestMethod]
    public void useCardResolverProcessesValidSlash()
    {
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];
        var target = game.Players[1];

        var slash = CreateSlashCard();
        ((Zone)source.HandZone).MutableCards.Add(slash);

        var stack = new BasicResolutionStack();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();

        var action = new ActionDescriptor(
            ActionId: "UseSlash",
            DisplayKey: "action.useSlash",
            RequiresTargets: true,
            TargetConstraints: new TargetConstraints(1, 1, TargetFilterType.Enemies),
            CardCandidates: new[] { slash }
        );

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { target.Seat },
            SelectedCardIds: new[] { slash.Id },
            SelectedOptionId: null,
            Confirmed: null
        );

        var context = new ResolutionContext(
            game,
            source,
            action,
            choice,
            stack,
            cardMoveService,
            ruleService
        );

        var resolver = new UseCardResolver();
        var result = resolver.Resolve(context);

        Assert.IsTrue(result.Success);
        
        // Verify card was moved from hand to discard pile
        Assert.IsFalse(source.HandZone.Cards.Contains(slash));
        Assert.IsTrue(game.DiscardPile.Cards.Contains(slash));

        // Verify SlashResolver was pushed onto the stack
        Assert.IsFalse(stack.IsEmpty);
    }

    /// <summary>
    /// Verifies that UseCardResolver properly handles the error case when the selected card
    /// is not found in the player's hand.
    /// 
    /// Test scenario:
    /// - Sets up a 2-player game in Play phase
    /// - Creates a Slash card but does NOT add it to the source player's hand
    /// - Creates a UseSlash action that references the non-existent card
    /// - Executes UseCardResolver with invalid card selection
    /// 
    /// Expected results:
    /// - Resolution fails with CardNotFound error code
    /// - Card is NOT moved (validation fails before card movement)
    /// - Game state remains unchanged
    /// 
    /// This test ensures that UseCardResolver validates card ownership before attempting
    /// to move cards, preventing invalid state changes.
    /// </summary>
    [TestMethod]
    public void useCardResolverFailsWhenCardNotFound()
    {
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];
        var target = game.Players[1];

        // Don't add card to hand - it should fail
        var slash = CreateSlashCard();

        var stack = new BasicResolutionStack();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();

        var action = new ActionDescriptor(
            ActionId: "UseSlash",
            DisplayKey: "action.useSlash",
            RequiresTargets: true,
            TargetConstraints: new TargetConstraints(1, 1, TargetFilterType.Enemies),
            CardCandidates: new[] { slash }
        );

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { target.Seat },
            SelectedCardIds: new[] { slash.Id },
            SelectedOptionId: null,
            Confirmed: null
        );

        var context = new ResolutionContext(
            game,
            source,
            action,
            choice,
            stack,
            cardMoveService,
            ruleService
        );

        var resolver = new UseCardResolver();
        var result = resolver.Resolve(context);

        Assert.IsFalse(result.Success);
        Assert.AreEqual(ResolutionErrorCode.CardNotFound, result.ErrorCode);
        
        // Verify card was NOT moved (validation failed before card movement)
        Assert.IsFalse(source.HandZone.Cards.Contains(slash));
        Assert.IsFalse(game.DiscardPile.Cards.Contains(slash));
    }

    /// <summary>
    /// Verifies that SlashResolver successfully processes a valid Slash hit when the target is alive.
    /// 
    /// Test scenario:
    /// - Sets up a 2-player game
    /// - Source player uses Slash against an alive target player
    /// - Executes SlashResolver with valid target selection
    /// 
    /// Expected results:
    /// - Resolution succeeds, indicating the Slash hit the target
    /// - Note: Damage calculation is handled by DamageResolver (step 10), not in this test
    /// 
    /// This test verifies that SlashResolver correctly validates target state and confirms
    /// a successful hit. The actual damage application will be tested when DamageResolver is implemented.
    /// </summary>
    [TestMethod]
    public void slashResolverProcessesValidHit()
    {
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var target = game.Players[1];

        var stack = new BasicResolutionStack();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();

        var action = new ActionDescriptor(
            ActionId: "UseSlash",
            DisplayKey: "action.useSlash",
            RequiresTargets: true,
            TargetConstraints: new TargetConstraints(1, 1, TargetFilterType.Enemies),
            CardCandidates: null
        );

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { target.Seat },
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: null
        );

        var context = new ResolutionContext(
            game,
            source,
            action,
            choice,
            stack,
            cardMoveService,
            ruleService
        );

        var resolver = new SlashResolver();
        var result = resolver.Resolve(context);

        Assert.IsTrue(result.Success);
    }

    /// <summary>
    /// Verifies that SlashResolver properly rejects attempts to use Slash against a dead target.
    /// 
    /// Test scenario:
    /// - Sets up a 2-player game
    /// - Target player is marked as not alive (IsAlive = false)
    /// - Source player attempts to use Slash against the dead target
    /// - Executes SlashResolver with invalid target
    /// 
    /// Expected results:
    /// - Resolution fails with TargetNotAlive error code
    /// - Game state remains unchanged
    /// 
    /// This test ensures that SlashResolver validates target state before processing,
    /// preventing invalid actions against dead players.
    /// </summary>
    [TestMethod]
    public void slashResolverFailsWhenTargetNotAlive()
    {
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var target = game.Players[1];
        target.IsAlive = false; // Target is dead

        var stack = new BasicResolutionStack();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();

        var action = new ActionDescriptor(
            ActionId: "UseSlash",
            DisplayKey: "action.useSlash",
            RequiresTargets: true,
            TargetConstraints: new TargetConstraints(1, 1, TargetFilterType.Enemies),
            CardCandidates: null
        );

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { target.Seat },
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: null
        );

        var context = new ResolutionContext(
            game,
            source,
            action,
            choice,
            stack,
            cardMoveService,
            ruleService
        );

        var resolver = new SlashResolver();
        var result = resolver.Resolve(context);

        Assert.IsFalse(result.Success);
        Assert.AreEqual(ResolutionErrorCode.TargetNotAlive, result.ErrorCode);
    }

    /// <summary>
    /// Verifies the complete end-to-end flow of using a Slash card through the entire resolution pipeline.
    /// 
    /// Test scenario:
    /// - Sets up a 2-player game in Play phase
    /// - Source player has a Slash card in hand
    /// - Creates a UseSlash action with valid target selection
    /// - Pushes UseCardResolver onto the resolution stack
    /// - Executes all resolvers in the stack until completion
    /// 
    /// Expected results:
    /// - All resolvers execute successfully (UseCardResolver -> SlashResolver)
    /// - Slash card is moved from hand to discard pile
    /// - Execution history contains both UseCardResolver and SlashResolver records
    /// - Stack is empty after all resolvers complete
    /// 
    /// This integration test verifies that the complete resolution pipeline works correctly,
    /// from initial action through card movement to final hit confirmation. It ensures that
    /// resolver chaining (UseCardResolver pushing SlashResolver) works as designed.
    /// </summary>
    [TestMethod]
    public void completeSlashResolutionFlow()
    {
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];
        var target = game.Players[1];

        var slash = CreateSlashCard();
        ((Zone)source.HandZone).MutableCards.Add(slash);

        var stack = new BasicResolutionStack();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();

        var action = new ActionDescriptor(
            ActionId: "UseSlash",
            DisplayKey: "action.useSlash",
            RequiresTargets: true,
            TargetConstraints: new TargetConstraints(1, 1, TargetFilterType.Enemies),
            CardCandidates: new[] { slash }
        );

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { target.Seat },
            SelectedCardIds: new[] { slash.Id },
            SelectedOptionId: null,
            Confirmed: null
        );

        var context = new ResolutionContext(
            game,
            source,
            action,
            choice,
            stack,
            cardMoveService,
            ruleService
        );

        // Start with UseCardResolver
        var useCardResolver = new UseCardResolver();
        stack.Push(useCardResolver, context);

        // Execute all resolvers
        while (!stack.IsEmpty)
        {
            var result = stack.Pop();
            Assert.IsTrue(result.Success, $"Resolver failed: {result.MessageKey ?? result.ErrorCode?.ToString()}");
        }

        // Verify final state
        Assert.IsFalse(source.HandZone.Cards.Contains(slash));
        Assert.IsTrue(game.DiscardPile.Cards.Contains(slash));

        // Verify execution history
        var history = stack.GetHistory();
        Assert.IsTrue(history.Count >= 1);
        Assert.IsTrue(history.Any(r => r.ResolverType == typeof(UseCardResolver)));
        Assert.IsTrue(history.Any(r => r.ResolverType == typeof(SlashResolver)));
    }

    /// <summary>
    /// Verifies that DamageResolver successfully applies normal damage to a target player.
    /// 
    /// Test scenario:
    /// - Sets up a 2-player game
    /// - Target player has 4 health
    /// - Creates a damage descriptor for 1 point of normal damage
    /// - Executes DamageResolver
    /// 
    /// Expected results:
    /// - Resolution succeeds
    /// - Target player's health is reduced by 1 (from 4 to 3)
    /// - Target player remains alive
    /// </summary>
    [TestMethod]
    public void damageResolverAppliesNormalDamage()
    {
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var target = game.Players[1];
        
        var initialHealth = target.CurrentHealth;
        Assert.AreEqual(4, initialHealth);

        var damage = new DamageDescriptor(
            SourceSeat: source.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Test"
        );

        var stack = new BasicResolutionStack();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();

        var context = new ResolutionContext(
            game,
            source,
            null,
            null,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: damage
        );

        var resolver = new DamageResolver();
        var result = resolver.Resolve(context);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(initialHealth - 1, target.CurrentHealth);
        Assert.IsTrue(target.IsAlive);
    }

    /// <summary>
    /// Verifies that DamageResolver correctly handles damage that reduces health to exactly 0.
    /// 
    /// Test scenario:
    /// - Sets up a 2-player game
    /// - Target player has 1 health
    /// - Creates a damage descriptor for 1 point of damage
    /// - Executes DamageResolver
    /// 
    /// Expected results:
    /// - Resolution succeeds
    /// - Target player's health is reduced to 0
    /// - Target player's IsAlive is set to false
    /// </summary>
    [TestMethod]
    public void damageResolverSetsIsAliveToFalseWhenHealthReachesZero()
    {
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var target = game.Players[1];
        target.CurrentHealth = 1; // Set to 1 health

        var damage = new DamageDescriptor(
            SourceSeat: source.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Test"
        );

        var stack = new BasicResolutionStack();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();

        var context = new ResolutionContext(
            game,
            source,
            null,
            null,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: damage
        );

        var resolver = new DamageResolver();
        var result = resolver.Resolve(context);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(0, target.CurrentHealth);
        Assert.IsFalse(target.IsAlive);
    }

    /// <summary>
    /// Verifies that DamageResolver prevents health from going below 0.
    /// 
    /// Test scenario:
    /// - Sets up a 2-player game
    /// - Target player has 2 health
    /// - Creates a damage descriptor for 5 points of damage (more than current health)
    /// - Executes DamageResolver
    /// 
    /// Expected results:
    /// - Resolution succeeds
    /// - Target player's health is reduced to 0 (not negative)
    /// - Target player's IsAlive is set to false
    /// </summary>
    [TestMethod]
    public void damageResolverPreventsNegativeHealth()
    {
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var target = game.Players[1];
        target.CurrentHealth = 2; // Set to 2 health

        var damage = new DamageDescriptor(
            SourceSeat: source.Seat,
            TargetSeat: target.Seat,
            Amount: 5, // More than current health
            Type: DamageType.Normal,
            Reason: "Test"
        );

        var stack = new BasicResolutionStack();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();

        var context = new ResolutionContext(
            game,
            source,
            null,
            null,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: damage
        );

        var resolver = new DamageResolver();
        var result = resolver.Resolve(context);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(0, target.CurrentHealth);
        Assert.IsFalse(target.IsAlive);
    }

    /// <summary>
    /// Verifies that DamageResolver fails when no pending damage is provided.
    /// 
    /// Test scenario:
    /// - Sets up a 2-player game
    /// - Creates a context without PendingDamage
    /// - Executes DamageResolver
    /// 
    /// Expected results:
    /// - Resolution fails with InvalidState error code
    /// - Target player's health remains unchanged
    /// </summary>
    [TestMethod]
    public void damageResolverFailsWhenNoPendingDamage()
    {
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var target = game.Players[1];
        var initialHealth = target.CurrentHealth;

        var stack = new BasicResolutionStack();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();

        var context = new ResolutionContext(
            game,
            source,
            null,
            null,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null // No pending damage
        );

        var resolver = new DamageResolver();
        var result = resolver.Resolve(context);

        Assert.IsFalse(result.Success);
        Assert.AreEqual(ResolutionErrorCode.InvalidState, result.ErrorCode);
        Assert.AreEqual(initialHealth, target.CurrentHealth); // Health unchanged
    }

    /// <summary>
    /// Verifies that DamageResolver fails when target player is not alive.
    /// 
    /// Test scenario:
    /// - Sets up a 2-player game
    /// - Target player is marked as not alive
    /// - Creates a damage descriptor
    /// - Executes DamageResolver
    /// 
    /// Expected results:
    /// - Resolution fails with TargetNotAlive error code
    /// - Target player's health remains unchanged
    /// </summary>
    [TestMethod]
    public void damageResolverFailsWhenTargetNotAlive()
    {
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var target = game.Players[1];
        target.IsAlive = false; // Target is dead
        var initialHealth = target.CurrentHealth;

        var damage = new DamageDescriptor(
            SourceSeat: source.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Test"
        );

        var stack = new BasicResolutionStack();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();

        var context = new ResolutionContext(
            game,
            source,
            null,
            null,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: damage
        );

        var resolver = new DamageResolver();
        var result = resolver.Resolve(context);

        Assert.IsFalse(result.Success);
        Assert.AreEqual(ResolutionErrorCode.TargetNotAlive, result.ErrorCode);
        Assert.AreEqual(initialHealth, target.CurrentHealth); // Health unchanged
    }

    /// <summary>
    /// Verifies that DamageResolver fails when target player is not found.
    /// 
    /// Test scenario:
    /// - Sets up a 2-player game
    /// - Creates a damage descriptor with invalid target seat
    /// - Executes DamageResolver
    /// 
    /// Expected results:
    /// - Resolution fails with InvalidTarget error code
    /// </summary>
    [TestMethod]
    public void damageResolverFailsWhenTargetNotFound()
    {
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var invalidTargetSeat = 999; // Non-existent seat

        var damage = new DamageDescriptor(
            SourceSeat: source.Seat,
            TargetSeat: invalidTargetSeat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Test"
        );

        var stack = new BasicResolutionStack();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();

        var context = new ResolutionContext(
            game,
            source,
            null,
            null,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: damage
        );

        var resolver = new DamageResolver();
        var result = resolver.Resolve(context);

        Assert.IsFalse(result.Success);
        Assert.AreEqual(ResolutionErrorCode.InvalidTarget, result.ErrorCode);
    }

    /// <summary>
    /// Verifies that DamageResolver logs damage events when LogSink is provided.
    /// 
    /// Test scenario:
    /// - Sets up a 2-player game
    /// - Creates a mock LogSink to capture log entries
    /// - Creates a damage descriptor
    /// - Executes DamageResolver with LogSink
    /// 
    /// Expected results:
    /// - Resolution succeeds
    /// - LogSink receives a log entry with EventType "DamageApplied"
    /// - Log entry contains all relevant damage information
    /// </summary>
    [TestMethod]
    public void damageResolverLogsDamageEvent()
    {
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var target = game.Players[1];
        var initialHealth = target.CurrentHealth;

        var damage = new DamageDescriptor(
            SourceSeat: source.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Test"
        );

        var loggedEntries = new List<LogEntry>();
        var logSink = new TestLogSink(loggedEntries);

        var stack = new BasicResolutionStack();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();

        var context = new ResolutionContext(
            game,
            source,
            null,
            null,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: damage,
            LogSink: logSink
        );

        var resolver = new DamageResolver();
        var result = resolver.Resolve(context);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, loggedEntries.Count);
        
        var logEntry = loggedEntries[0];
        Assert.AreEqual("DamageApplied", logEntry.EventType);
        Assert.IsNotNull(logEntry.Data);
    }

    /// <summary>
    /// Verifies that DamageResolver works correctly without LogSink (optional dependency).
    /// 
    /// Test scenario:
    /// - Sets up a 2-player game
    /// - Creates a damage descriptor
    /// - Executes DamageResolver without LogSink
    /// 
    /// Expected results:
    /// - Resolution succeeds
    /// - Damage is applied correctly
    /// - No exceptions are thrown
    /// </summary>
    [TestMethod]
    public void damageResolverWorksWithoutLogSink()
    {
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var target = game.Players[1];
        var initialHealth = target.CurrentHealth;

        var damage = new DamageDescriptor(
            SourceSeat: source.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Test"
        );

        var stack = new BasicResolutionStack();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();

        var context = new ResolutionContext(
            game,
            source,
            null,
            null,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: damage,
            LogSink: null // No log sink
        );

        var resolver = new DamageResolver();
        var result = resolver.Resolve(context);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(initialHealth - 1, target.CurrentHealth);
    }

    /// <summary>
    /// Verifies the complete end-to-end flow: Slash -> Damage resolution.
    /// 
    /// Test scenario:
    /// - Sets up a 2-player game in Play phase
    /// - Source player has a Slash card in hand
    /// - Executes complete resolution flow (UseCardResolver -> SlashResolver -> DamageResolver)
    /// 
    /// Expected results:
    /// - All resolvers execute successfully
    /// - Slash card is moved to discard pile
    /// - Target player's health is reduced by 1
    /// - Execution history contains all three resolvers
    /// </summary>
    [TestMethod]
    public void completeSlashToDamageResolutionFlow()
    {
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];
        var target = game.Players[1];
        var initialHealth = target.CurrentHealth;

        var slash = CreateSlashCard();
        ((Zone)source.HandZone).MutableCards.Add(slash);

        var stack = new BasicResolutionStack();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();

        var action = new ActionDescriptor(
            ActionId: "UseSlash",
            DisplayKey: "action.useSlash",
            RequiresTargets: true,
            TargetConstraints: new TargetConstraints(1, 1, TargetFilterType.Enemies),
            CardCandidates: new[] { slash }
        );

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { target.Seat },
            SelectedCardIds: new[] { slash.Id },
            SelectedOptionId: null,
            Confirmed: null
        );

        var context = new ResolutionContext(
            game,
            source,
            action,
            choice,
            stack,
            cardMoveService,
            ruleService
        );

        // Start with UseCardResolver
        var useCardResolver = new UseCardResolver();
        stack.Push(useCardResolver, context);

        // Execute all resolvers
        while (!stack.IsEmpty)
        {
            var result = stack.Pop();
            Assert.IsTrue(result.Success, $"Resolver failed: {result.MessageKey ?? result.ErrorCode?.ToString()}");
        }

        // Verify final state
        Assert.IsFalse(source.HandZone.Cards.Contains(slash));
        Assert.IsTrue(game.DiscardPile.Cards.Contains(slash));
        Assert.AreEqual(initialHealth - 1, target.CurrentHealth);

        // Verify execution history
        var history = stack.GetHistory();
        Assert.IsTrue(history.Any(r => r.ResolverType == typeof(UseCardResolver)));
        Assert.IsTrue(history.Any(r => r.ResolverType == typeof(SlashResolver)));
        Assert.IsTrue(history.Any(r => r.ResolverType == typeof(DamageResolver)));
    }

    /// <summary>
    /// Test implementation of ILogSink that captures log entries for testing.
    /// </summary>
    private sealed class TestLogSink : ILogSink
    {
        private readonly List<LogEntry> _entries;

        public TestLogSink(List<LogEntry> entries)
        {
            _entries = entries;
        }

        public void Log(LogEntry entry)
        {
            _entries.Add(entry);
        }
    }
}
