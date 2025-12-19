using LegendOfThreeKingdoms.Core;
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
}
