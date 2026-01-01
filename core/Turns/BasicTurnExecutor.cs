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

        // 1. Start phase - typically empty, but can trigger skills
        if (game.CurrentPhase == Phase.Start)
        {
            _turnEngine.AdvancePhase(game);
        }

        // 2. Judge phase - handled automatically by JudgePhaseService listening to PhaseStartEvent
        if (game.CurrentPhase == Phase.Judge)
        {
            // JudgePhaseService automatically handles judgement cards, just advance
            _turnEngine.AdvancePhase(game);
        }

        // 3. Draw phase - handled automatically by DrawPhaseService listening to PhaseStartEvent
        if (game.CurrentPhase == Phase.Draw)
        {
            // DrawPhaseService automatically draws cards, just advance
            _turnEngine.AdvancePhase(game);
        }

        // 4. Play phase - action execution loop (core logic)
        if (game.CurrentPhase == Phase.Play)
        {
            ExecutePlayPhase(game, player);
        }

        // 5. Discard phase - forced discard if hand exceeds max health
        if (game.CurrentPhase == Phase.Discard)
        {
            ExecuteDiscardPhase(game, player);
            // Only advance phase if we're still in Discard phase (in case phase was changed)
            if (game.CurrentPhase == Phase.Discard)
            {
                _turnEngine.AdvancePhase(game);
            }
        }

        // 6. End phase - turn end, can trigger skills
        if (game.CurrentPhase == Phase.End)
        {
            _turnEngine.AdvancePhase(game);
        }
    }

    /// <summary>
    /// Executes the play phase: loops through available actions until player ends the phase.
    /// </summary>
    private void ExecutePlayPhase(Game game, Player player)
    {
        // Loop until player ends play phase or no actions available
        while (game.CurrentPhase == Phase.Play && player.IsAlive)
        {
            // Query available actions
            var context = new RuleContext(game, player);
            var actionsResult = _ruleService.GetAvailableActions(context);

            // If no actions available, automatically end play phase
            if (actionsResult.Items.Count == 0)
            {
                break;
            }

            // If only EndPlayPhase action is available, automatically select it
            var endPlayPhaseAction = actionsResult.Items.FirstOrDefault(a => a.ActionId == "EndPlayPhase");
            if (actionsResult.Items.Count == 1 && endPlayPhaseAction is not null)
            {
                break;
            }

            // Filter out EndPlayPhase from available actions for player selection
            var playableActions = actionsResult.Items
                .Where(a => a.ActionId != "EndPlayPhase")
                .ToList();

            // If no playable actions (only EndPlayPhase), end phase
            if (playableActions.Count == 0)
            {
                break;
            }

            // Get player's action choice
            // For now, we'll use a simple approach: try actions in order until one succeeds
            // In a full implementation, this would be handled by UI/network layer with proper action selection
            ActionDescriptor? selectedAction = null;
            ChoiceRequest? choiceRequest = null;
            ChoiceResult? choiceResult = null;
            bool actionExecuted = false;

            // Try each playable action until one succeeds or all fail
            foreach (var action in playableActions)
            {
                try
                {
                    selectedAction = action;

                    // If action requires targets, create choice request and get player choice
                    if (selectedAction.RequiresTargets)
                    {
                        try
                        {
                            choiceRequest = _choiceRequestFactory.CreateForAction(context, selectedAction);
                            choiceResult = _getPlayerChoice(choiceRequest);

                            // Validate choice result
                            if (choiceResult is null)
                            {
                                // Player cancelled, try next action
                                continue;
                            }
                        }
                        catch (InvalidOperationException)
                        {
                            // Action doesn't require explicit choice, continue without choice request
                            // This shouldn't happen if RequiresTargets is true, but handle gracefully
                            continue;
                        }
                    }
                    else
                    {
                        // Action doesn't require targets, but might need card selection
                        // For actions like "UsePeach", the card is already in CardCandidates
                        // We need to create a ChoiceResult with the selected card
                        if (selectedAction.CardCandidates is not null && selectedAction.CardCandidates.Count > 0)
                        {
                            // For simplicity, select the first available card
                            // In a full implementation, player would choose which card to use
                            var selectedCard = selectedAction.CardCandidates.First();
                            choiceResult = new ChoiceResult(
                                RequestId: Guid.NewGuid().ToString("N"),
                                PlayerSeat: player.Seat,
                                SelectedTargetSeats: null,
                                SelectedCardIds: new[] { selectedCard.Id },
                                SelectedOptionId: null,
                                Confirmed: null);
                        }
                    }

                    // Validate action before execution
                    var validationResult = _ruleService.ValidateActionBeforeResolve(context, selectedAction, choiceRequest);
                    if (!validationResult.IsAllowed)
                    {
                        // Action is no longer valid, try next action
                        continue;
                    }

                    // Execute action
                    // Note: ActionResolutionMapper.Resolve will create its own stack and execute it
                    try
                    {
                        _actionMapper.Resolve(context, selectedAction, choiceRequest, choiceResult);
                        actionExecuted = true;
                        break; // Action executed successfully, exit loop
                    }
                    catch (InvalidOperationException)
                    {
                        // Action execution failed (e.g., no handler registered), try next action
                        continue;
                    }
                    catch (Exception)
                    {
                        // Other errors during execution, try next action
                        // In a production system, this would be logged
                        continue;
                    }
                }
                catch (Exception)
                {
                    // Error during action setup, try next action
                    continue;
                }
            }

            // If no action was executed, end play phase
            if (!actionExecuted)
            {
                break;
            }

            // After action execution, check if we should continue
            // The phase might have been changed by the action (e.g., skill that skips phases)
            if (game.CurrentPhase != Phase.Play)
            {
                break;
            }

            // Check if player is still alive (might have died during action execution)
            if (!player.IsAlive)
            {
                break;
            }

            // Refresh player reference in case it changed
            var refreshedPlayer = game.Players.FirstOrDefault(p => p.Seat == player.Seat);
            if (refreshedPlayer is null || !refreshedPlayer.IsAlive)
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
