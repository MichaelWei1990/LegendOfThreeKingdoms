using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Rules;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Base class for targeted trick card resolvers that require:
/// 1. Target player selection
/// 2. Card selection from target's zones
/// 3. Nullification window handling
/// 4. Effect application
/// 
/// Examples: Guohe Chaiqiao (过河拆桥), Shunshou Qianyang (顺手牵羊)
/// </summary>
public abstract class TargetedTrickResolverBase : IResolver
{
    /// <summary>
    /// Gets the message key prefix for error messages (e.g., "resolution.guohechaiqiao").
    /// </summary>
    protected abstract string MessageKeyPrefix { get; }

    /// <summary>
    /// Gets the effect key for nullification (e.g., "GuoheChaiqiao.Resolve").
    /// </summary>
    protected abstract string EffectKey { get; }

    /// <summary>
    /// Gets the nullification result key prefix (e.g., "GuoheChaiqiaoNullification").
    /// </summary>
    protected abstract string NullificationResultKeyPrefix { get; }

    /// <summary>
    /// Gets the message key for "cannot target self" error.
    /// Override to provide a custom message key.
    /// </summary>
    protected virtual string CannotTargetSelfMessageKey => $"{MessageKeyPrefix}.cannotTargetSelf";

    /// <summary>
    /// Gets the message key for "no selectable cards" error.
    /// Override to provide a custom message key (e.g., "noDiscardableCards", "noObtainableCards").
    /// </summary>
    protected virtual string NoSelectableCardsMessageKey => $"{MessageKeyPrefix}.noSelectableCards";

    /// <summary>
    /// Validates the target player. Override to add custom validation (e.g., distance check).
    /// </summary>
    /// <param name="context">The resolution context.</param>
    /// <param name="sourcePlayer">The source player.</param>
    /// <param name="target">The target player to validate.</param>
    /// <returns>Null if validation passes, otherwise a failure result.</returns>
    protected virtual ResolutionResult? ValidateTarget(
        ResolutionContext context,
        Player sourcePlayer,
        Player target)
    {
        // Default validation: target must be alive and not self
        if (!target.IsAlive)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.TargetNotAlive,
                messageKey: $"{MessageKeyPrefix}.targetNotAlive",
                details: new { TargetSeat = target.Seat });
        }

        if (target.Seat == sourcePlayer.Seat)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: CannotTargetSelfMessageKey,
                details: new { TargetSeat = target.Seat });
        }

        return null; // Validation passed
    }

    /// <summary>
    /// Collects cards that can be selected from the target player's zones.
    /// Override to customize which zones are included.
    /// </summary>
    /// <param name="target">The target player.</param>
    /// <returns>List of cards that can be selected.</returns>
    protected virtual List<Card> CollectSelectableCards(Player target)
    {
        var cards = new List<Card>();
        
        if (target.HandZone.Cards is not null)
        {
            cards.AddRange(target.HandZone.Cards);
        }
        
        if (target.EquipmentZone.Cards is not null)
        {
            cards.AddRange(target.EquipmentZone.Cards);
        }
        
        if (target.JudgementZone.Cards is not null)
        {
            cards.AddRange(target.JudgementZone.Cards);
        }

        return cards;
    }

    /// <summary>
    /// Gets the source zone for a selected card.
    /// </summary>
    /// <param name="target">The target player.</param>
    /// <param name="selectedCardId">The ID of the selected card.</param>
    /// <returns>The source zone, or null if not found.</returns>
    protected virtual IZone? GetSourceZone(Player target, int selectedCardId)
    {
        if (target.HandZone.Cards?.Any(c => c.Id == selectedCardId) == true)
        {
            return target.HandZone;
        }
        
        if (target.EquipmentZone.Cards?.Any(c => c.Id == selectedCardId) == true)
        {
            return target.EquipmentZone;
        }
        
        if (target.JudgementZone.Cards?.Any(c => c.Id == selectedCardId) == true)
        {
            return target.JudgementZone;
        }

        return null;
    }

    /// <summary>
    /// Creates the effect handler resolver that will apply the effect after nullification check.
    /// </summary>
    /// <param name="target">The target player.</param>
    /// <param name="selectedCard">The selected card.</param>
    /// <param name="sourceZone">The source zone of the selected card.</param>
    /// <param name="context">The resolution context (for accessing source player if needed).</param>
    /// <returns>The effect handler resolver instance.</returns>
    protected abstract IResolver CreateEffectHandlerResolver(
        Player target,
        Card selectedCard,
        IZone sourceZone,
        ResolutionContext context);

    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var game = context.Game;
        var sourcePlayer = context.SourcePlayer;
        var choice = context.Choice;

        // Step 1: Validate choice
        if (choice is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: $"{MessageKeyPrefix}.noChoice");
        }

        // Step 2: Extract and validate target
        var targetValidationResult = ExtractAndValidateTarget(context, game, sourcePlayer, choice);
        if (targetValidationResult is not null)
        {
            return targetValidationResult;
        }

        var targetSeat = choice.SelectedTargetSeats![0];
        var target = game.Players.FirstOrDefault(p => p.Seat == targetSeat);

        if (target is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: $"{MessageKeyPrefix}.targetNotFound",
                details: new { TargetSeat = targetSeat });
        }

        // Step 3: Validate target (custom validation)
        var customValidationResult = ValidateTarget(context, sourcePlayer, target);
        if (customValidationResult is not null)
        {
            return customValidationResult;
        }

        // Step 4: Collect selectable cards
        var selectableCards = CollectSelectableCards(target);

        if (selectableCards.Count == 0)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: NoSelectableCardsMessageKey,
                details: new { TargetSeat = target.Seat });
        }

        // Step 5: Request player to choose a card
        var cardSelectionResult = RequestCardSelection(context, sourcePlayer, selectableCards);
        if (cardSelectionResult.FailureResult is not null)
        {
            return cardSelectionResult.FailureResult;
        }

        // Step 6: Get selected card and source zone
        var selectedCard = cardSelectionResult.SelectedCard!;
        var sourceZone = GetSourceZone(target, selectedCard.Id);
        if (sourceZone is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: $"{MessageKeyPrefix}.sourceZoneNotFound",
                details: new { CardId = selectedCard.Id });
        }

        // Step 7: Open nullification window and push effect handler
        OpenNullificationWindowAndPushHandler(
            context,
            target,
            selectedCard,
            sourceZone);

        return ResolutionResult.SuccessResult;
    }

    /// <summary>
    /// Extracts and validates the target from the choice.
    /// </summary>
    private ResolutionResult? ExtractAndValidateTarget(
        ResolutionContext context,
        Game game,
        Player sourcePlayer,
        ChoiceResult choice)
    {
        var selectedTargetSeats = choice.SelectedTargetSeats;
        if (selectedTargetSeats is null || selectedTargetSeats.Count == 0)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: $"{MessageKeyPrefix}.noTarget");
        }

        return null; // Success
    }

    /// <summary>
    /// Result of card selection request.
    /// </summary>
    private sealed record CardSelectionResult(
        Card? SelectedCard,
        ResolutionResult? FailureResult);

    /// <summary>
    /// Requests the player to select a card.
    /// </summary>
    private CardSelectionResult RequestCardSelection(
        ResolutionContext context,
        Player sourcePlayer,
        List<Card> selectableCards)
    {
        if (context.GetPlayerChoice is null)
        {
            return new CardSelectionResult(
                null,
                ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: $"{MessageKeyPrefix}.getPlayerChoiceNotAvailable"));
        }

        var choiceRequest = new ChoiceRequest(
            RequestId: Guid.NewGuid().ToString("N"),
            PlayerSeat: sourcePlayer.Seat,
            ChoiceType: ChoiceType.SelectCards,
            TargetConstraints: null,
            AllowedCards: selectableCards,
            CanPass: false  // Must select one card
        );

        var playerChoice = context.GetPlayerChoice(choiceRequest);

        if (playerChoice is null)
        {
            return new CardSelectionResult(
                null,
                ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: $"{MessageKeyPrefix}.playerChoiceNull"));
        }

        var selectedCardIds = playerChoice.SelectedCardIds;
        if (selectedCardIds is null || selectedCardIds.Count == 0)
        {
            return new CardSelectionResult(
                null,
                ResolutionResult.Failure(
                    ResolutionErrorCode.CardNotFound,
                    messageKey: $"{MessageKeyPrefix}.noCardSelected"));
        }

        var selectedCardId = selectedCardIds[0];
        var selectedCard = selectableCards.FirstOrDefault(c => c.Id == selectedCardId);

        if (selectedCard is null)
        {
            return new CardSelectionResult(
                null,
                ResolutionResult.Failure(
                    ResolutionErrorCode.CardNotFound,
                    messageKey: $"{MessageKeyPrefix}.cardNotFound",
                    details: new { CardId = selectedCardId }));
        }

        return new CardSelectionResult(selectedCard, null);
    }

    /// <summary>
    /// Opens the nullification window and pushes the effect handler resolver.
    /// </summary>
    private void OpenNullificationWindowAndPushHandler(
        ResolutionContext context,
        Player target,
        Card selectedCard,
        IZone sourceZone)
    {
        // Create nullifiable effect
        var causingCard = context.ExtractCausingCard();
        var nullifiableEffect = NullificationHelper.CreateNullifiableEffect(
            effectKey: EffectKey,
            targetPlayer: target,
            causingCard: causingCard,
            isNullifiable: true);

        var nullificationResultKey = $"{NullificationResultKeyPrefix}_{target.Seat}";

        // Create handler context
        var handlerContext = new ResolutionContext(
            context.Game,
            context.SourcePlayer,
            context.Action,
            context.Choice,
            context.Stack,
            context.CardMoveService,
            context.RuleService,
            context.PendingDamage,
            context.LogSink,
            context.GetPlayerChoice,
            context.IntermediateResults,
            context.EventBus,
            context.LogCollector,
            context.SkillManager,
            context.EquipmentSkillRegistry,
            context.JudgementService
        );

        // Push handler resolver first (will execute after nullification window due to LIFO)
        var handlerResolver = CreateEffectHandlerResolver(target, selectedCard, sourceZone, context);
        context.Stack.Push(handlerResolver, handlerContext);

        // Open nullification window (push last so it executes first)
        NullificationHelper.OpenNullificationWindow(context, nullifiableEffect, nullificationResultKey);
    }
}
