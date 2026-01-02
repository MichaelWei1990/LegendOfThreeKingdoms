using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Turns;

/// <summary>
/// Basic implementation of ITurnExecutor for identity mode flow.
/// Coordinates the execution of all phases in a player's turn:
/// Start → Judge → Draw → Play → Discard → End
/// </summary>
public sealed class BasicTurnExecutor : ITurnExecutor
{
    private readonly ITurnEngine _turnEngine;
    private readonly IRuleService _ruleService;
    private readonly IActionResolutionMapper _actionMapper;
    private readonly ICardMoveService _cardMoveService;
    private readonly Func<ChoiceRequest, ChoiceResult> _getPlayerChoice;
    private readonly IEventBus? _eventBus;
    private readonly SkillManager? _skillManager;
    private readonly IChoiceRequestFactory _choiceRequestFactory;

    /// <summary>
    /// Creates a new BasicTurnExecutor with the required dependencies.
    /// </summary>
    /// <param name="turnEngine">The turn engine for phase advancement.</param>
    /// <param name="ruleService">The rule service for querying available actions.</param>
    /// <param name="actionMapper">The action resolution mapper for executing actions.</param>
    /// <param name="cardMoveService">The card move service for discard operations.</param>
    /// <param name="getPlayerChoice">Function to get player choices (required).</param>
    /// <param name="eventBus">Optional event bus for publishing events.</param>
    /// <param name="skillManager">Optional skill manager for skill-related functionality.</param>
    /// <param name="choiceRequestFactory">Optional choice request factory. If null, uses default implementation.</param>
    public BasicTurnExecutor(
        ITurnEngine turnEngine,
        IRuleService ruleService,
        IActionResolutionMapper actionMapper,
        ICardMoveService cardMoveService,
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice,
        IEventBus? eventBus = null,
        SkillManager? skillManager = null,
        IChoiceRequestFactory? choiceRequestFactory = null)
    {
        _turnEngine = turnEngine ?? throw new ArgumentNullException(nameof(turnEngine));
        _ruleService = ruleService ?? throw new ArgumentNullException(nameof(ruleService));
        _actionMapper = actionMapper ?? throw new ArgumentNullException(nameof(actionMapper));
        _cardMoveService = cardMoveService ?? throw new ArgumentNullException(nameof(cardMoveService));
        _getPlayerChoice = getPlayerChoice ?? throw new ArgumentNullException(nameof(getPlayerChoice));
        _eventBus = eventBus;
        _skillManager = skillManager;
        _choiceRequestFactory = choiceRequestFactory ?? new ChoiceRequestFactory();
    }

    /// <inheritdoc />
    public void ExecuteTurn(Game game, Player player)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        if (player is null) throw new ArgumentNullException(nameof(player));

        // Execute phases in order: Start → Judge → Draw → Play → Discard → End
        // Note: Draw and Judge phases are handled automatically by services listening to PhaseStartEvent
        // Loop through all phases until turn is complete

        while (!IsTurnComplete(game, player))
        {
            var currentPhase = game.CurrentPhase;

            // Execute logic for current phase
            switch (currentPhase)
            {
                case Phase.Start:
                    // Start phase - typically empty, but can trigger skills
                    _turnEngine.AdvancePhase(game);
                    break;

                case Phase.Judge:
                    // Judge phase - handled automatically by JudgePhaseService listening to PhaseStartEvent
                    _turnEngine.AdvancePhase(game);
                    break;

                case Phase.Draw:
                    // Draw phase - handled automatically by DrawPhaseService listening to PhaseStartEvent
                    _turnEngine.AdvancePhase(game);
                    break;

                case Phase.Play:
                    // Play phase - action execution loop (core logic)
                    ExecutePlayPhase(game, player);
                    // ExecutePlayPhase internally advances to Discard phase when done
                    break;

                case Phase.Discard:
                    // Discard phase - forced discard if hand exceeds max health
                    ExecuteDiscardPhase(game, player);
                    // Only advance phase if we're still in Discard phase (in case phase was changed)
                    if (game.CurrentPhase == Phase.Discard)
                    {
                        _turnEngine.AdvancePhase(game);
                    }
                    break;

                case Phase.End:
                    // End phase - turn end, can trigger skills
                    // AdvancePhase will call StartNextTurn when at End phase, moving to next player's Start
                    _turnEngine.AdvancePhase(game);
                    break;
            }

            // Refresh player reference (may have changed during phase execution)
            var refreshedPlayer = game.Players.FirstOrDefault(p => p.Seat == player.Seat);
            if (refreshedPlayer is null || !refreshedPlayer.IsAlive)
            {
                break;
            }
            player = refreshedPlayer;
        }
    }

    /// <summary>
    /// Determines if the turn is complete.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="player">The player whose turn is being executed.</param>
    /// <returns>True if the turn is complete, false otherwise.</returns>
    private bool IsTurnComplete(Game game, Player player)
    {
        // Turn is complete if:
        // 1. Game is finished
        if (game.IsFinished)
        {
            return true;
        }

        // 2. Player is null or dead
        if (player is null || !player.IsAlive)
        {
            return true;
        }

        // 3. Current player seat has changed (turn has advanced to next player)
        if (game.CurrentPlayerSeat != player.Seat)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Executes the play phase: loops through available actions until player ends the phase.
    /// </summary>
    private void ExecutePlayPhase(Game game, Player player)
    {
        while (game.CurrentPhase == Phase.Play && player.IsAlive)
        {
            var context = new RuleContext(game, player);
            
            // Get playable actions (filtered and validated)
            var playableActions = GetPlayableActions(context);
            if (playableActions.Count == 0)
            {
                break;
            }

            // Get player's action selection
            var selectedAction = GetPlayerActionChoice(game, player, playableActions);
            if (selectedAction is null)
            {
                break;
            }

            // Execute the selected action
            var actionExecuted = ExecuteSelectedAction(context, selectedAction);
            if (!actionExecuted)
            {
                break;
            }

            // Check if we should continue the play phase and refresh player reference
            if (!ShouldContinuePlayPhase(game, player, out var refreshedPlayer))
            {
                break;
            }
            player = refreshedPlayer;
        }

        // End play phase by advancing to discard phase
        if (game.CurrentPhase == Phase.Play)
        {
            _turnEngine.AdvancePhase(game);
        }
    }

    /// <summary>
    /// Gets playable actions for the current context, filtering out EndPlayPhase.
    /// </summary>
    private List<ActionDescriptor> GetPlayableActions(RuleContext context)
    {
        var actionsResult = _ruleService.GetAvailableActions(context);

        // If no actions available, return empty list
        if (actionsResult.Items.Count == 0)
        {
            return new List<ActionDescriptor>();
        }

        // If only EndPlayPhase action is available, return empty list (will end phase)
        var endPlayPhaseAction = actionsResult.Items.FirstOrDefault(a => a.ActionId == "EndPlayPhase");
        if (actionsResult.Items.Count == 1 && endPlayPhaseAction is not null)
        {
            return new List<ActionDescriptor>();
        }

        // Filter out EndPlayPhase from available actions
        return actionsResult.Items
            .Where(a => a.ActionId != "EndPlayPhase")
            .ToList();
    }

    /// <summary>
    /// Gets the player's action choice from available playable actions.
    /// </summary>
    private ActionDescriptor? GetPlayerActionChoice(Game game, Player player, List<ActionDescriptor> playableActions)
    {
        // Create action selection request
        var actionSelectionRequest = CreateActionSelectionRequest(player, playableActions);

        // Get player's action choice
        ChoiceResult? actionSelectionResult = null;
        try
        {
            actionSelectionResult = _getPlayerChoice(actionSelectionRequest);
        }
        catch (Exception)
        {
            // If getting choice fails, treat as player ending play phase
            return null;
        }

        // Validate player's choice
        if (actionSelectionResult is null || 
            actionSelectionResult.Confirmed == false ||
            string.IsNullOrEmpty(actionSelectionResult.SelectedOptionId))
        {
            return null;
        }

        // Find the selected action
        var selectedActionId = actionSelectionResult.SelectedOptionId;
        var selectedAction = playableActions.FirstOrDefault(a => a.ActionId == selectedActionId);
        
        if (selectedAction is null)
        {
            return null;
        }

        // Re-validate the selected action (game state might have changed)
        var context = new RuleContext(game, player);
        var preValidationResult = _ruleService.ValidateActionBeforeResolve(context, selectedAction, null);
        if (!preValidationResult.IsAllowed)
        {
            return null;
        }

        return selectedAction;
    }

    /// <summary>
    /// Creates an action selection request for the player.
    /// </summary>
    private ChoiceRequest CreateActionSelectionRequest(Player player, List<ActionDescriptor> playableActions)
    {
        var actionOptions = playableActions
            .Select(a => new ChoiceOption(
                OptionId: a.ActionId,
                DisplayKey: a.DisplayKey ?? a.ActionId))
            .ToList();

        return new ChoiceRequest(
            RequestId: Guid.NewGuid().ToString("N"),
            PlayerSeat: player.Seat,
            ChoiceType: ChoiceType.SelectOption,
            TargetConstraints: null,
            AllowedCards: null,
            ResponseWindowId: null,
            CanPass: true, // Player can pass to end play phase
            Options: actionOptions);
    }

    /// <summary>
    /// Executes the selected action, handling target and card selection as needed.
    /// </summary>
    private bool ExecuteSelectedAction(RuleContext context, ActionDescriptor selectedAction)
    {
        ChoiceRequest? choiceRequest = null;
        ChoiceResult? choiceResult = null;

        try
        {
            // Prepare choice request and result based on action requirements
            if (!PrepareActionChoice(context, selectedAction, out choiceRequest, out choiceResult))
            {
                return false;
            }

            // Validate action before execution
            var validationResult = _ruleService.ValidateActionBeforeResolve(context, selectedAction, choiceRequest);
            if (!validationResult.IsAllowed)
            {
                return false;
            }

            // Execute action
            try
            {
                _actionMapper.Resolve(context, selectedAction, choiceRequest, choiceResult);
                return true;
            }
            catch (InvalidOperationException)
            {
                // Action execution failed (e.g., no handler registered)
                return false;
            }
            catch (Exception)
            {
                // Other errors during execution
                // In a production system, this would be logged
                return false;
            }
        }
        catch (Exception)
        {
            // Error during action setup
            return false;
        }
    }

    /// <summary>
    /// Prepares the choice request and result for action execution.
    /// Handles both target selection and card selection scenarios.
    /// </summary>
    private bool PrepareActionChoice(
        RuleContext context, 
        ActionDescriptor selectedAction, 
        out ChoiceRequest? choiceRequest, 
        out ChoiceResult? choiceResult)
    {
        choiceRequest = null;
        choiceResult = null;

        if (selectedAction.RequiresTargets)
        {
            return PrepareTargetChoice(context, selectedAction, out choiceRequest, out choiceResult);
        }
        else
        {
            return PrepareCardChoice(context, selectedAction, out choiceResult);
        }
    }

    /// <summary>
    /// Prepares target choice for actions that require targets.
    /// </summary>
    private bool PrepareTargetChoice(
        RuleContext context,
        ActionDescriptor selectedAction,
        out ChoiceRequest? choiceRequest,
        out ChoiceResult? choiceResult)
    {
        choiceRequest = null;
        choiceResult = null;

        try
        {
            choiceRequest = _choiceRequestFactory.CreateForAction(context, selectedAction);
            choiceResult = _getPlayerChoice(choiceRequest);

            // Validate choice result
            if (choiceResult is null)
            {
                // Player cancelled target selection
                return false;
            }

            return true;
        }
        catch (InvalidOperationException)
        {
            // Action doesn't require explicit choice, but RequiresTargets is true
            // This shouldn't happen, but handle gracefully
            return false;
        }
    }

    /// <summary>
    /// Prepares card choice for actions that don't require targets but need card selection.
    /// </summary>
    private bool PrepareCardChoice(
        RuleContext context,
        ActionDescriptor selectedAction,
        out ChoiceResult? choiceResult)
    {
        choiceResult = null;

        // Action doesn't require targets, but might need card selection
        // For actions like "UsePeach" or "UseEquip", the card is already in CardCandidates
        if (selectedAction.CardCandidates is not null && selectedAction.CardCandidates.Count > 0)
        {
            // For simplicity, select the first available card
            // In a full implementation, player would choose which card to use
            var selectedCard = selectedAction.CardCandidates.First();
            choiceResult = new ChoiceResult(
                RequestId: Guid.NewGuid().ToString("N"),
                PlayerSeat: context.CurrentPlayer.Seat,
                SelectedTargetSeats: null,
                SelectedCardIds: new[] { selectedCard.Id },
                SelectedOptionId: null,
                Confirmed: null);
            return true;
        }

        // If CardCandidates is empty or null, we cannot prepare a valid choice
        // This should not happen for valid actions, but handle gracefully
        return false;
    }

    /// <summary>
    /// Checks if the play phase should continue after action execution.
    /// Returns the refreshed player reference if the phase should continue.
    /// </summary>
    private bool ShouldContinuePlayPhase(Game game, Player player, out Player refreshedPlayer)
    {
        refreshedPlayer = player;

        // Check if phase has changed (e.g., skill that skips phases)
        if (game.CurrentPhase != Phase.Play)
        {
            return false;
        }

        // Check if player is still alive
        if (!player.IsAlive)
        {
            return false;
        }

        // Refresh player reference in case it changed
        var foundPlayer = game.Players.FirstOrDefault(p => p.Seat == player.Seat);
        if (foundPlayer is null || !foundPlayer.IsAlive)
        {
            return false;
        }

        refreshedPlayer = foundPlayer;
        return true;
    }

    /// <summary>
    /// Executes the discard phase: forces player to discard excess cards if hand exceeds max health.
    /// </summary>
    private void ExecuteDiscardPhase(Game game, Player player)
    {
        // Calculate excess cards
        var handCount = player.HandZone.Cards.Count;
        var maxHealth = player.MaxHealth;
        var excessCards = handCount - maxHealth;

        // If no excess, no discard needed
        if (excessCards <= 0)
        {
            return;
        }

        // Force discard: create choice request for discarding cards
        var discardRequest = new ChoiceRequest(
            RequestId: Guid.NewGuid().ToString("N"),
            PlayerSeat: player.Seat,
            ChoiceType: ChoiceType.SelectCards,
            TargetConstraints: null,
            AllowedCards: player.HandZone.Cards.ToList(),
            CanPass: false // Cannot skip, must discard
        );

        // Get player's discard choice
        ChoiceResult? discardChoice = null;
        try
        {
            discardChoice = _getPlayerChoice(discardRequest);
        }
        catch (Exception)
        {
            // If getting choice fails, auto-select first N cards
            discardChoice = null;
        }

        // Validate and execute discard
        List<Card> cardsToDiscard;
        if (discardChoice?.SelectedCardIds is not null && discardChoice.SelectedCardIds.Count == excessCards)
        {
            // Player selected correct number of cards
            cardsToDiscard = player.HandZone.Cards
                .Where(c => discardChoice.SelectedCardIds.Contains(c.Id))
                .ToList();
            
            // If we couldn't find all selected cards, fall back to auto-select
            if (cardsToDiscard.Count != excessCards)
            {
                cardsToDiscard = player.HandZone.Cards.Take(excessCards).ToList();
            }
        }
        else
        {
            // Invalid selection or failed to get choice, auto-select first N cards
            cardsToDiscard = player.HandZone.Cards.Take(excessCards).ToList();
        }

        // Execute discard
        if (cardsToDiscard.Count > 0)
        {
            try
            {
                _cardMoveService.DiscardFromHand(game, player, cardsToDiscard);
            }
            catch (Exception ex)
            {
                // Discard operation failed - this is a serious error
                throw new InvalidOperationException(
                    $"Failed to discard cards during discard phase: {ex.Message}", ex);
            }
        }

        // Verify discard result (defensive programming)
        // Recalculate after discard in case hand changed
        var remainingExcess = player.HandZone.Cards.Count - player.MaxHealth;
        if (remainingExcess > 0)
        {
            // Still have excess cards, this shouldn't happen but handle it
            // In a production system, this would be logged as an error
            // For now, we'll try one more time with auto-select
            var additionalCards = player.HandZone.Cards.Take(remainingExcess).ToList();
            if (additionalCards.Count > 0)
            {
                _cardMoveService.DiscardFromHand(game, player, additionalCards);
            }
        }
    }
}
