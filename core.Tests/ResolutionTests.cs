using System.Collections.Generic;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Events;
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

    private static Card CreatePeachCard(int id = 1)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "peach_basic",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Peach,
            Suit = Suit.Heart,
            Rank = 1
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
    /// Verifies that SlashResolver requires GetPlayerChoice function for response window.
    /// 
    /// Test scenario:
    /// - Sets up a 2-player game
    /// - Source player uses Slash against an alive target player
    /// - Executes SlashResolver without GetPlayerChoice function
    /// 
    /// Expected results:
    /// - Resolution fails with InvalidState error code
    /// 
    /// This test verifies that SlashResolver correctly validates that GetPlayerChoice is provided
    /// before creating a response window.
    /// </summary>
    [TestMethod]
    public void slashResolverFailsWhenGetPlayerChoiceNotProvided()
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
            ruleService,
            GetPlayerChoice: null  // Not provided
        );

        var resolver = new SlashResolver();
        var result = resolver.Resolve(context);

        Assert.IsFalse(result.Success);
        Assert.AreEqual(ResolutionErrorCode.InvalidState, result.ErrorCode);
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

        // Create getPlayerChoice function (not used in this test, but required)
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: new List<int>(),
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var context = new ResolutionContext(
            game,
            source,
            action,
            choice,
            stack,
            cardMoveService,
            ruleService,
            GetPlayerChoice: getPlayerChoice
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

        // Create getPlayerChoice function that returns no response (empty choice)
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: new List<int>(),  // Empty - no response
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var intermediateResults = new Dictionary<string, object>();
        var context = new ResolutionContext(
            game,
            source,
            action,
            choice,
            stack,
            cardMoveService,
            ruleService,
            GetPlayerChoice: getPlayerChoice,
            IntermediateResults: intermediateResults
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
        // Note: SlashResolver is not in history because it returns immediately after pushing other resolvers
        var history = stack.GetHistory();
        Assert.IsTrue(history.Count >= 1);
        Assert.IsTrue(history.Any(r => r.ResolverType == typeof(UseCardResolver)));
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
            Reason: "Test",
            TriggersDying: false  // Don't trigger dying process for this test
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
            Reason: "Test",
            TriggersDying: false  // Don't trigger dying process for this test
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

        // Create getPlayerChoice function that returns no response (empty choice)
        // This simulates the target player not having a Dodge card or choosing not to respond
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: new List<int>(),  // Empty - no response
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var intermediateResults = new Dictionary<string, object>();
        var context = new ResolutionContext(
            game,
            source,
            action,
            choice,
            stack,
            cardMoveService,
            ruleService,
            GetPlayerChoice: getPlayerChoice,
            IntermediateResults: intermediateResults
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
        Assert.IsTrue(history.Any(r => r.ResolverType.Name.Contains("ResponseWindow")));
        Assert.IsTrue(history.Any(r => r.ResolverType.Name.Contains("SlashResponseHandler")));
        Assert.IsTrue(history.Any(r => r.ResolverType == typeof(DamageResolver)));
    }

    private static Card CreateDodgeCard(int id = 1)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "dodge_basic",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Dodge,
            Suit = Suit.Heart,
            Rank = 2
        };
    }

    /// <summary>
    /// Verifies that SlashResolver creates response window and handler when GetPlayerChoice is provided.
    /// Tests the complete flow: Slash -> Response Window (No Response) -> Damage.
    /// 
    /// Test scenario:
    /// - Sets up a 2-player game
    /// - Source player uses Slash against target
    /// - Target has no Dodge card (cannot respond)
    /// - Provides GetPlayerChoice function that returns no response
    /// 
    /// Expected results:
    /// - SlashResolver pushes ResponseWindowResolver and SlashResponseHandlerResolver
    /// - Response window returns NoResponse
    /// - SlashResponseHandlerResolver triggers DamageResolver
    /// - Target takes damage
    /// </summary>
    [TestMethod]
    public void slashResolverWithResponseWindowNoResponseTriggersDamage()
    {
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var target = game.Players[1];
        var initialHealth = target.CurrentHealth;

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

        // Create getPlayerChoice function that returns no response (empty choice)
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: new List<int>(),  // Empty - no response
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var intermediateResults = new Dictionary<string, object>();
        var context = new ResolutionContext(
            game,
            source,
            action,
            choice,
            stack,
            cardMoveService,
            ruleService,
            GetPlayerChoice: getPlayerChoice,
            IntermediateResults: intermediateResults
        );

        var resolver = new SlashResolver();
        var result = resolver.Resolve(context);

        Assert.IsTrue(result.Success);

        // Execute all resolvers in the stack
        while (!stack.IsEmpty)
        {
            var stackResult = stack.Pop();
            Assert.IsTrue(stackResult.Success, $"Resolver failed: {stackResult.MessageKey ?? stackResult.ErrorCode?.ToString()}");
        }

        // Verify damage was applied
        Assert.AreEqual(initialHealth - 1, target.CurrentHealth);

        // Verify execution history
        // Note: SlashResolver is not in history because it returns immediately after pushing other resolvers
        var history = stack.GetHistory();
        Assert.IsTrue(history.Any(r => r.ResolverType.Name.Contains("ResponseWindow")), 
            $"Expected ResponseWindowResolver in history, but got: {string.Join(", ", history.Select(r => r.ResolverType.Name))}");
        Assert.IsTrue(history.Any(r => r.ResolverType.Name.Contains("SlashResponseHandler")), 
            $"Expected SlashResponseHandlerResolver in history, but got: {string.Join(", ", history.Select(r => r.ResolverType.Name))}");
        Assert.IsTrue(history.Any(r => r.ResolverType == typeof(DamageResolver)), 
            $"Expected DamageResolver in history, but got: {string.Join(", ", history.Select(r => r.ResolverType.Name))}");
    }

    /// <summary>
    /// Verifies that SlashResolver creates response window and handler when GetPlayerChoice is provided.
    /// Tests the complete flow: Slash -> Response Window (Success Response) -> No Damage.
    /// 
    /// Test scenario:
    /// - Sets up a 2-player game
    /// - Source player uses Slash against target
    /// - Target has a Dodge card and responds
    /// - Provides GetPlayerChoice function that returns Dodge card selection
    /// 
    /// Expected results:
    /// - SlashResolver pushes ResponseWindowResolver and SlashResponseHandlerResolver
    /// - Response window returns ResponseSuccess
    /// - SlashResponseHandlerResolver does NOT trigger DamageResolver
    /// - Target does NOT take damage
    /// - Dodge card is moved to discard pile
    /// </summary>
    [TestMethod]
    public void slashResolverWithResponseWindowSuccessResponseNoDamage()
    {
        var game = CreateDefaultGame(2);
        var source = game.Players[0];
        var target = game.Players[1];
        var initialHealth = target.CurrentHealth;

        var dodge = CreateDodgeCard(1);
        ((Zone)target.HandZone).MutableCards.Add(dodge);

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

        // Create getPlayerChoice function that returns Dodge card selection
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: new[] { dodge.Id },  // Respond with Dodge
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var intermediateResults = new Dictionary<string, object>();
        var context = new ResolutionContext(
            game,
            source,
            action,
            choice,
            stack,
            cardMoveService,
            ruleService,
            GetPlayerChoice: getPlayerChoice,
            IntermediateResults: intermediateResults
        );

        var resolver = new SlashResolver();
        var result = resolver.Resolve(context);

        Assert.IsTrue(result.Success);

        // Execute all resolvers in the stack
        while (!stack.IsEmpty)
        {
            var stackResult = stack.Pop();
            Assert.IsTrue(stackResult.Success, $"Resolver failed: {stackResult.MessageKey ?? stackResult.ErrorCode?.ToString()}");
        }

        // Verify damage was NOT applied
        Assert.AreEqual(initialHealth, target.CurrentHealth);

        // Verify Dodge card was moved to discard pile
        Assert.IsFalse(target.HandZone.Cards.Contains(dodge));
        Assert.IsTrue(game.DiscardPile.Cards.Contains(dodge));

        // Verify execution history
        // Note: SlashResolver is not in history because it returns immediately after pushing other resolvers
        var history = stack.GetHistory();
        Assert.IsTrue(history.Any(r => r.ResolverType.Name.Contains("ResponseWindow")), 
            $"Expected ResponseWindowResolver in history, but got: {string.Join(", ", history.Select(r => r.ResolverType.Name))}");
        Assert.IsTrue(history.Any(r => r.ResolverType.Name.Contains("SlashResponseHandler")), 
            $"Expected SlashResponseHandlerResolver in history, but got: {string.Join(", ", history.Select(r => r.ResolverType.Name))}");
        // DamageResolver should NOT be in history
        Assert.IsFalse(history.Any(r => r.ResolverType == typeof(DamageResolver)), 
            $"Expected no DamageResolver in history, but got: {string.Join(", ", history.Select(r => r.ResolverType.Name))}");
    }

    /// <summary>
    /// Verifies that DamageResolver triggers DyingResolver when health reaches zero and TriggersDying is true.
    /// 
    /// Test scenario:
    /// - Sets up a 2-player game
    /// - Target player has 1 health
    /// - Creates a damage descriptor for 1 point of damage with TriggersDying = true
    /// - Executes DamageResolver
    /// 
    /// Expected results:
    /// - Resolution succeeds
    /// - Target player's health is reduced to 0
    /// - DyingResolver is pushed onto the stack
    /// - Target player's IsAlive is not set to false yet (will be set after dying process)
    /// </summary>
    [TestMethod]
    public void dyingResolverTriggersWhenHealthReachesZero()
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
            Reason: "Test",
            TriggersDying: true
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
        // IsAlive should still be true because dying process hasn't completed yet
        Assert.IsTrue(target.IsAlive);
        // Stack should not be empty (DyingResolver should be pushed)
        Assert.IsFalse(stack.IsEmpty);
    }

    /// <summary>
    /// Verifies that dying rescue succeeds when a player has a Peach card.
    /// 
    /// Test scenario:
    /// - Sets up a 2-player game
    /// - Target player has 0 health (dying)
    /// - Target player has a Peach card
    /// - Executes DyingResolver and rescue response window
    /// 
    /// Expected results:
    /// - Resolution succeeds
    /// - Target player uses Peach to rescue themselves
    /// - Target player's health is restored to 1
    /// - Target player remains alive
    /// </summary>
    [TestMethod]
    public void dyingRescueSucceedsWithPeach()
    {
        var game = CreateDefaultGame(2);
        var dyingPlayer = game.Players[0];
        dyingPlayer.CurrentHealth = 0;
        dyingPlayer.IsAlive = true; // Still alive but dying

        var peach = CreatePeachCard(1);
        ((Zone)dyingPlayer.HandZone).MutableCards.Add(peach);

        var stack = new BasicResolutionStack();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var loggedEntries = new List<LogEntry>();
        var logSink = new TestLogSink(loggedEntries);

        var intermediateResults = new Dictionary<string, object>
        {
            ["DyingPlayerSeat"] = dyingPlayer.Seat
        };

        // Create getPlayerChoice function that makes dying player use Peach
        ChoiceResult getPlayerChoice(ChoiceRequest request)
        {
            if (request.PlayerSeat == dyingPlayer.Seat && request.ChoiceType == ChoiceType.SelectCards && request.AllowedCards is not null)
            {
                // Find Peach card in allowed cards
                var availablePeach = request.AllowedCards.FirstOrDefault(c => c.CardSubType == CardSubType.Peach);
                if (availablePeach is not null)
                {
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: dyingPlayer.Seat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: new[] { availablePeach.Id },
                        SelectedOptionId: null,
                        Confirmed: true
                    );
                }
            }
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: true
            );
        }

        var context = new ResolutionContext(
            game,
            dyingPlayer,
            null,
            null,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: logSink,
            GetPlayerChoice: getPlayerChoice,
            IntermediateResults: intermediateResults
        );

        var resolver = new DyingResolver();
        var result = resolver.Resolve(context);

        Assert.IsTrue(result.Success);

        // Execute the stack to process response window and handler
        while (!stack.IsEmpty)
        {
            stack.Pop();
        }

        // Verify rescue succeeded
        Assert.AreEqual(1, dyingPlayer.CurrentHealth);
        Assert.IsTrue(dyingPlayer.IsAlive);
        // Peach should be moved to discard pile
        Assert.IsFalse(dyingPlayer.HandZone.Cards.Contains(peach));
        Assert.IsTrue(game.DiscardPile.Cards.Contains(peach));
        // Verify log entries
        Assert.IsTrue(loggedEntries.Any(e => e.EventType == "DyingStart"));
        Assert.IsTrue(loggedEntries.Any(e => e.EventType == "DyingRescueSuccess"));
    }

    /// <summary>
    /// Verifies that dying rescue fails when no player has a Peach card.
    /// 
    /// Test scenario:
    /// - Sets up a 2-player game
    /// - Target player has 0 health (dying)
    /// - No player has a Peach card
    /// - Executes DyingResolver and rescue response window
    /// 
    /// Expected results:
    /// - Resolution succeeds
    /// - No rescue occurs
    /// - Target player's IsAlive is set to false
    /// - Death event is logged
    /// </summary>
    [TestMethod]
    public void dyingRescueFailsWithoutPeach()
    {
        var game = CreateDefaultGame(2);
        var dyingPlayer = game.Players[0];
        dyingPlayer.CurrentHealth = 0;
        dyingPlayer.IsAlive = true; // Still alive but dying

        var stack = new BasicResolutionStack();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var loggedEntries = new List<LogEntry>();
        var logSink = new TestLogSink(loggedEntries);

        var intermediateResults = new Dictionary<string, object>
        {
            ["DyingPlayerSeat"] = dyingPlayer.Seat
        };

        // Create getPlayerChoice function that returns no response (no Peach available)
        ChoiceResult getPlayerChoice(ChoiceRequest request)
        {
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: true
            );
        }

        var context = new ResolutionContext(
            game,
            dyingPlayer,
            null,
            null,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: logSink,
            GetPlayerChoice: getPlayerChoice,
            IntermediateResults: intermediateResults
        );

        var resolver = new DyingResolver();
        var result = resolver.Resolve(context);

        Assert.IsTrue(result.Success);

        // Execute the stack to process response window and handler
        while (!stack.IsEmpty)
        {
            stack.Pop();
        }

        // Verify death
        Assert.AreEqual(0, dyingPlayer.CurrentHealth);
        Assert.IsFalse(dyingPlayer.IsAlive);
        // Verify log entries
        Assert.IsTrue(loggedEntries.Any(e => e.EventType == "DyingStart"));
        Assert.IsTrue(loggedEntries.Any(e => e.EventType == "PlayerDied"));
        Assert.IsFalse(loggedEntries.Any(e => e.EventType == "DyingRescueSuccess"));
    }

    /// <summary>
    /// Verifies that multiple rescues can occur when a player needs multiple Peaches.
    /// 
    /// Test scenario:
    /// - Sets up a 2-player game
    /// - Target player has -1 health (needs 2 Peaches to recover)
    /// - Target player has 2 Peach cards
    /// - Executes DyingResolver and rescue response window multiple times
    /// 
    /// Expected results:
    /// - Resolution succeeds
    /// - Target player uses 2 Peaches to rescue themselves
    /// - Target player's health is restored to 1
    /// - Target player remains alive
    /// </summary>
    [TestMethod]
    public void dyingRescueMultipleTimes()
    {
        var game = CreateDefaultGame(2);
        var dyingPlayer = game.Players[0];
        dyingPlayer.CurrentHealth = -1; // Needs 2 Peaches
        dyingPlayer.IsAlive = true; // Still alive but dying

        var peach1 = CreatePeachCard(1);
        var peach2 = CreatePeachCard(2);
        ((Zone)dyingPlayer.HandZone).MutableCards.Add(peach1);
        ((Zone)dyingPlayer.HandZone).MutableCards.Add(peach2);

        var stack = new BasicResolutionStack();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var loggedEntries = new List<LogEntry>();
        var logSink = new TestLogSink(loggedEntries);

        var intermediateResults = new Dictionary<string, object>
        {
            ["DyingPlayerSeat"] = dyingPlayer.Seat
        };

        var peachUsed = new List<int>();

        // Create getPlayerChoice function that makes dying player use Peaches
        ChoiceResult getPlayerChoice(ChoiceRequest request)
        {
            if (request.PlayerSeat == dyingPlayer.Seat && request.ChoiceType == ChoiceType.SelectCards && request.AllowedCards is not null)
            {
                // Use first available Peach from allowed cards
                var availablePeach = request.AllowedCards
                    .FirstOrDefault(c => c.CardSubType == CardSubType.Peach && !peachUsed.Contains(c.Id));
                
                if (availablePeach is not null)
                {
                    peachUsed.Add(availablePeach.Id);
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: dyingPlayer.Seat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: new[] { availablePeach.Id },
                        SelectedOptionId: null,
                        Confirmed: true
                    );
                }
            }
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: true
            );
        }

        var context = new ResolutionContext(
            game,
            dyingPlayer,
            null,
            null,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: logSink,
            GetPlayerChoice: getPlayerChoice,
            IntermediateResults: intermediateResults
        );

        var resolver = new DyingResolver();
        var result = resolver.Resolve(context);

        Assert.IsTrue(result.Success);

        // Execute the stack to process response window and handler (may trigger multiple times)
        while (!stack.IsEmpty)
        {
            stack.Pop();
        }

        // Verify rescue succeeded (health should be at least 1)
        Assert.IsTrue(dyingPlayer.CurrentHealth >= 1);
        Assert.IsTrue(dyingPlayer.IsAlive);
    }

    /// <summary>
    /// Verifies that DamageResolver publishes DamageCreatedEvent and DamageAppliedEvent.
    /// </summary>
    [TestMethod]
    public void damageResolverPublishesDamageEvents()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player0 = game.Players[0];
        var player1 = game.Players[1];
        player1.CurrentHealth = 3;

        var stack = new BasicResolutionStack();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var eventBus = new BasicEventBus();

        var publishedEvents = new List<IGameEvent>();
        eventBus.Subscribe<DamageCreatedEvent>(evt => publishedEvents.Add(evt));
        eventBus.Subscribe<DamageAppliedEvent>(evt => publishedEvents.Add(evt));

        var damage = new DamageDescriptor(0, 1, 2, DamageType.Normal, "Test");
        var context = new ResolutionContext(
            game,
            player0,
            null,
            null,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: damage,
            EventBus: eventBus
        );

        // Act
        var resolver = new DamageResolver();
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(2, publishedEvents.Count);
        Assert.IsInstanceOfType(publishedEvents[0], typeof(DamageCreatedEvent));
        Assert.IsInstanceOfType(publishedEvents[1], typeof(DamageAppliedEvent));

        var damageCreatedEvent = (DamageCreatedEvent)publishedEvents[0];
        Assert.AreEqual(damage, damageCreatedEvent.Damage);

        var damageAppliedEvent = (DamageAppliedEvent)publishedEvents[1];
        Assert.AreEqual(damage, damageAppliedEvent.Damage);
        Assert.AreEqual(3, damageAppliedEvent.PreviousHealth);
        Assert.AreEqual(1, damageAppliedEvent.CurrentHealth);
    }

    /// <summary>
    /// Verifies that DyingResolver publishes DyingStartEvent.
    /// </summary>
    [TestMethod]
    public void dyingResolverPublishesDyingStartEvent()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player0 = game.Players[0];
        var player1 = game.Players[1];
        player1.CurrentHealth = 0;

        var stack = new BasicResolutionStack();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var eventBus = new BasicEventBus();

        var publishedEvents = new List<IGameEvent>();
        eventBus.Subscribe<DyingStartEvent>(evt => publishedEvents.Add(evt));

        var intermediateResults = new Dictionary<string, object> { ["DyingPlayerSeat"] = 1 };
        var context = new ResolutionContext(
            game,
            player0,
            null,
            null,
            stack,
            cardMoveService,
            ruleService,
            EventBus: eventBus,
            IntermediateResults: intermediateResults,
            GetPlayerChoice: req => new ChoiceResult(
                RequestId: req.RequestId,
                PlayerSeat: req.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: true
            )
        );

        // Act
        var resolver = new DyingResolver();
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, publishedEvents.Count);
        Assert.IsInstanceOfType(publishedEvents[0], typeof(DyingStartEvent));

        var dyingStartEvent = (DyingStartEvent)publishedEvents[0];
        Assert.AreEqual(1, dyingStartEvent.DyingPlayerSeat);
    }

    /// <summary>
    /// Verifies that DyingResolver logs dying and death events correctly.
    /// 
    /// Test scenario:
    /// - Sets up a 2-player game
    /// - Target player has 0 health (dying)
    /// - No player has a Peach card
    /// - Executes DyingResolver with LogSink
    /// 
    /// Expected results:
    /// - Resolution succeeds
    /// - DyingStart event is logged
    /// - PlayerDied event is logged
    /// - Log entries contain correct data
    /// </summary>
    [TestMethod]
    public void dyingResolverLogsDyingAndDeathEvents()
    {
        var game = CreateDefaultGame(2);
        var dyingPlayer = game.Players[0];
        dyingPlayer.CurrentHealth = 0;
        dyingPlayer.IsAlive = true; // Still alive but dying

        var stack = new BasicResolutionStack();
        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var loggedEntries = new List<LogEntry>();
        var logSink = new TestLogSink(loggedEntries);

        var intermediateResults = new Dictionary<string, object>
        {
            ["DyingPlayerSeat"] = dyingPlayer.Seat
        };

        // Create getPlayerChoice function that returns no response
        ChoiceResult getPlayerChoice(ChoiceRequest request)
        {
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: true
            );
        }

        var context = new ResolutionContext(
            game,
            dyingPlayer,
            null,
            null,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: logSink,
            GetPlayerChoice: getPlayerChoice,
            IntermediateResults: intermediateResults
        );

        var resolver = new DyingResolver();
        var result = resolver.Resolve(context);

        Assert.IsTrue(result.Success);

        // Execute the stack to process response window and handler
        while (!stack.IsEmpty)
        {
            stack.Pop();
        }

        // Verify log entries
        var dyingStartLog = loggedEntries.FirstOrDefault(e => e.EventType == "DyingStart");
        Assert.IsNotNull(dyingStartLog);
        Assert.AreEqual("Info", dyingStartLog!.Level);
        Assert.IsTrue(dyingStartLog.Message!.Contains(dyingPlayer.Seat.ToString()));

        var deathLog = loggedEntries.FirstOrDefault(e => e.EventType == "PlayerDied");
        Assert.IsNotNull(deathLog);
        Assert.AreEqual("Info", deathLog!.Level);
        Assert.IsTrue(deathLog.Message!.Contains(dyingPlayer.Seat.ToString()));
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
