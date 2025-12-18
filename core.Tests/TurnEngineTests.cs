using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Turns;

namespace core.Tests;

[TestClass]
public sealed class TurnEngineTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
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
    /// Verifies that BasicTurnEngine uses the game mode to select
    /// the first player seat when initializing turn state.
    /// </summary>
    [TestMethod]
    public void basicTurnEngineUsesGameModeForFirstPlayerSeat()
    {
        var game = CreateDefaultGame(3);
        var mode = new FixedFirstSeatGameMode(firstSeat: 1);
        var engine = new BasicTurnEngine(mode);

        var turn = engine.InitializeTurnState(game);

        Assert.AreEqual(1, turn.CurrentPlayerSeat);
    }

    /// <summary>
    /// Verifies a minimal end-to-end flow that combines RuleService and BasicTurnEngine
    /// from game initialization through one full turn and into the next player's turn.
    /// </summary>
    [TestMethod]
    public void turnEngineAndRulesDriveBasicTurnFlow()
    {
        // Arrange: 3-player game with a fixed first-seat game mode and basic turn engine.
        var game = CreateDefaultGame(3);
        var mode = new FixedFirstSeatGameMode(firstSeat: 0);
        var turnEngine = new BasicTurnEngine(mode);
        var ruleService = new RuleService();

        // Act: initialize turn state.
        var initialTurn = turnEngine.InitializeTurnState(game);

        // Assert initial state.
        Assert.AreEqual(1, initialTurn.TurnNumber);
        Assert.AreEqual(0, initialTurn.CurrentPlayerSeat);
        Assert.AreEqual(Phase.Start, initialTurn.CurrentPhase);

        // Advance phases: Start -> Judge -> Draw -> Play.
        turnEngine.AdvancePhase(game); // Start -> Judge
        Assert.AreEqual(Phase.Judge, game.CurrentPhase);

        turnEngine.AdvancePhase(game); // Judge -> Draw
        Assert.AreEqual(Phase.Draw, game.CurrentPhase);

        turnEngine.AdvancePhase(game); // Draw -> Play
        Assert.AreEqual(Phase.Play, game.CurrentPhase);

        // In Play phase, EndPlayPhase should be available as a basic action.
        var activePlayer = game.Players[game.CurrentPlayerSeat];
        var ruleContext = new RuleContext(game, activePlayer);
        var actionsResult = ruleService.GetAvailableActions(ruleContext);
        Assert.IsTrue(actionsResult.HasAny);
        var actionIds = actionsResult.Items.Select(a => a.ActionId).ToArray();
        CollectionAssert.Contains(actionIds, "EndPlayPhase");

        var endPlayAction = actionsResult.Items.Single(a => a.ActionId == "EndPlayPhase");

        // Validate the action before resolution; for now this should always be allowed.
        var validateResult = ruleService.ValidateActionBeforeResolve(ruleContext, endPlayAction, choice: null);
        Assert.IsTrue(validateResult.IsAllowed);

        // Turn engine should structurally allow ending the current phase.
        Assert.IsTrue(turnEngine.CanEndCurrentPhase(game));

        // Advance from Play -> Discard.
        var afterDiscard = turnEngine.AdvancePhase(game);
        Assert.IsTrue(afterDiscard.IsSuccess);
        Assert.AreEqual(Phase.Discard, afterDiscard.TurnState.CurrentPhase);
        Assert.AreEqual(0, afterDiscard.TurnState.CurrentPlayerSeat);
        Assert.AreEqual(1, afterDiscard.TurnState.TurnNumber);

        // Advance from Discard -> End.
        var afterEnd = turnEngine.AdvancePhase(game);
        Assert.IsTrue(afterEnd.IsSuccess);
        Assert.AreEqual(Phase.End, afterEnd.TurnState.CurrentPhase);
        Assert.AreEqual(0, afterEnd.TurnState.CurrentPlayerSeat);
        Assert.AreEqual(1, afterEnd.TurnState.TurnNumber);

        // Advance from End => next player's Start, turn number increases.
        var nextTurn = turnEngine.AdvancePhase(game);
        Assert.IsTrue(nextTurn.IsSuccess);
        Assert.AreEqual(Phase.Start, nextTurn.TurnState.CurrentPhase);
        Assert.AreEqual(1, nextTurn.TurnState.CurrentPlayerSeat);
        Assert.AreEqual(2, nextTurn.TurnState.TurnNumber);
    }
}
