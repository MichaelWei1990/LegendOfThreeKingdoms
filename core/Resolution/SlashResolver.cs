using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Logging;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Response;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Resolver that handles the result of a Slash response window.
/// Decides whether to trigger damage based on the response result.
/// </summary>
public sealed class SlashResponseHandlerResolver : IResolver
{
    private readonly DamageDescriptor _pendingDamage;

    /// <summary>
    /// Initializes a new instance of the <see cref="SlashResponseHandlerResolver"/> class.
    /// </summary>
    /// <param name="pendingDamage">The damage descriptor to apply if no response was made.</param>
    public SlashResponseHandlerResolver(DamageDescriptor pendingDamage)
    {
        _pendingDamage = pendingDamage ?? throw new ArgumentNullException(nameof(pendingDamage));
    }

    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        // Read response window result from IntermediateResults dictionary
        var intermediateResults = context.IntermediateResults;
        if (intermediateResults is null || !intermediateResults.TryGetValue("LastResponseResult", out var resultObj))
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.slash.noResponseResult");
        }

        if (resultObj is not ResponseWindowResult responseResult)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.slash.invalidResponseResult");
        }

        // Decide whether to trigger damage based on response result
        if (responseResult.State == ResponseWindowState.NoResponse)
        {
            // No response - trigger damage
            // Note: Card effect validation (e.g., Renwang Shield) is done before response window
            var damageContext = new ResolutionContext(
                context.Game,
                context.SourcePlayer,
                context.Action,
                context.Choice,
                context.Stack,
                context.CardMoveService,
                context.RuleService,
                PendingDamage: _pendingDamage,
                LogSink: context.LogSink,
                context.GetPlayerChoice,
                context.IntermediateResults,
                context.EventBus,
                context.LogCollector,
                context.SkillManager,
                context.EquipmentSkillRegistry
            );

            context.Stack.Push(new DamageResolver(), damageContext);
        }
        else if (responseResult.State == ResponseWindowState.ResponseSuccess)
        {
            // Response successful - slash dodged, no damage
            // Just return success
        }

        return ResolutionResult.SuccessResult;
    }
}

/// <summary>
/// Resolver for Slash card usage.
/// Handles the special resolution flow for Slash cards.
/// </summary>
public sealed class SlashResolver : IResolver
{
    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        // Step 1: Validate input
        var validationResult = ValidateInput(context);
        if (!validationResult.Success)
            return validationResult.Result!;

        // Step 2: Extract and validate target
        var targetResult = ExtractAndValidateTarget(context);
        if (!targetResult.Success)
            return targetResult.Result!;

        var target = targetResult.Target!;

        // Step 3: Get Slash card
        var cardResult = GetSlashCard(context);
        if (!cardResult.Success)
            return cardResult.Result!;

        var slashCard = cardResult.Card!;

        // Step 4: Validate card effect on target
        var effectValidationResult = ValidateAndHandleCardEffect(context, slashCard, target);
        if (!effectValidationResult.Success)
            return effectValidationResult.Result!;

        // Step 5: Setup Slash resolution (damage, modifiers, etc.)
        var setupResult = SetupSlashResolution(context, slashCard, target);

        // Step 6: Setup resolution stack (response window and handler)
        SetupResolutionStack(context, setupResult, target, slashCard);

        return ResolutionResult.SuccessResult;
    }

    /// <summary>
    /// Validates the input context for Slash resolution.
    /// </summary>
    private static ValidationResult ValidateInput(ResolutionContext context)
    {
        if (context.Choice is null)
        {
            return ValidationResult.CreateFailure(ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.slash.noChoice"));
        }

        if (context.GetPlayerChoice is null)
        {
            return ValidationResult.CreateFailure(ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.slash.getPlayerChoiceRequired"));
        }

        return ValidationResult.CreateSuccess();
    }

    /// <summary>
    /// Extracts and validates the target player from the choice.
    /// </summary>
    private static TargetExtractionResult ExtractAndValidateTarget(ResolutionContext context)
    {
        var game = context.Game;
        var choice = context.Choice!;

        var selectedTargetSeats = choice.SelectedTargetSeats;
        if (selectedTargetSeats is null || selectedTargetSeats.Count == 0)
        {
            return TargetExtractionResult.CreateFailure(ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: "resolution.slash.noTarget"));
        }

        var targetSeat = selectedTargetSeats[0];
        var target = game.Players.FirstOrDefault(p => p.Seat == targetSeat);

        if (target is null)
        {
            return TargetExtractionResult.CreateFailure(ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: "resolution.slash.targetNotFound",
                details: new { TargetSeat = targetSeat }));
        }

        if (!target.IsAlive)
        {
            return TargetExtractionResult.CreateFailure(ResolutionResult.Failure(
                ResolutionErrorCode.TargetNotAlive,
                messageKey: "resolution.slash.targetNotAlive",
                details: new { TargetSeat = targetSeat }));
        }

        return TargetExtractionResult.CreateSuccess(target);
    }

    /// <summary>
    /// Gets the Slash card being used from the choice.
    /// </summary>
    private static CardExtractionResult GetSlashCard(ResolutionContext context)
    {
        var choice = context.Choice!;
        var sourcePlayer = context.SourcePlayer;

        Card? slashCard = null;
        if (choice.SelectedCardIds is not null && choice.SelectedCardIds.Count > 0)
        {
            var cardId = choice.SelectedCardIds[0];
            // Try to find card from Action.CardCandidates first
            if (context.Action?.CardCandidates is not null)
            {
                slashCard = context.Action.CardCandidates.FirstOrDefault(c => c.Id == cardId);
            }
            // If not found, try to find from source player's hand
            if (slashCard is null && sourcePlayer.HandZone.Cards is not null)
            {
                slashCard = sourcePlayer.HandZone.Cards.FirstOrDefault(c => c.Id == cardId);
            }
        }

        if (slashCard is null)
        {
            return CardExtractionResult.CreateFailure(ResolutionResult.Failure(
                ResolutionErrorCode.CardNotFound,
                messageKey: "resolution.slash.cardNotFound"));
        }

        return CardExtractionResult.CreateSuccess(slashCard);
    }

    /// <summary>
    /// Validates card effect on target and handles veto cases.
    /// </summary>
    private static EffectValidationResult ValidateAndHandleCardEffect(
        ResolutionContext context,
        Card slashCard,
        Player target)
    {
        var game = context.Game;
        var sourcePlayer = context.SourcePlayer;

        // Validate card effect on target (before response window)
        // This is where equipment like Renwang Shield can invalidate the effect
        var effectContext = new CardEffectContext(
            Game: game,
            Card: slashCard,
            SourcePlayer: sourcePlayer,
            TargetPlayer: target
        );

        var isEffective = ValidateCardEffectOnTarget(context, effectContext, out var vetoReason);

        if (!isEffective)
        {
            // Card effect is invalidated (e.g., black Slash on Renwang Shield)
            // Log the veto and return success without creating response window or damage
            if (context.LogSink is not null && vetoReason is not null)
            {
                var logEntry = new LogEntry
                {
                    EventType = "CardEffectVetoed",
                    Level = "Info",
                    Message = $"Card effect vetoed: {vetoReason.Reason}",
                    Data = new
                    {
                        Source = vetoReason.Source,
                        Reason = vetoReason.Reason,
                        Details = vetoReason.Details,
                        CardId = slashCard.Id,
                        SourceSeat = sourcePlayer.Seat,
                        TargetSeat = target.Seat
                    }
                };
                context.LogSink.Log(logEntry);
            }

            // Publish event if available
            if (context.EventBus is not null)
            {
                // TODO: Create CardEffectVetoedEvent if needed
            }

            // Effect is invalidated, no response window, no damage
            return EffectValidationResult.CreateFailure(ResolutionResult.SuccessResult);
        }

        return EffectValidationResult.CreateSuccess();
    }

    /// <summary>
    /// Sets up Slash resolution: creates damage descriptor, initializes IntermediateResults,
    /// and processes Slash response modifiers.
    /// </summary>
    private static SlashSetupResult SetupSlashResolution(
        ResolutionContext context,
        Card slashCard,
        Player target)
    {
        var sourcePlayer = context.SourcePlayer;

        // Create damage descriptor (will be used if no response is made)
        var damage = new DamageDescriptor(
            SourceSeat: sourcePlayer.Seat,
            TargetSeat: target.Seat,
            Amount: 1,  // Basic Slash deals 1 damage
            Type: DamageType.Normal,
            Reason: "Slash",
            CausingCard: slashCard  // The Slash card that causes the damage
        );

        // Initialize IntermediateResults dictionary if not present
        // This dictionary will be shared across all resolvers in this resolution chain
        var intermediateResults = context.IntermediateResults;
        if (intermediateResults is null)
        {
            intermediateResults = new Dictionary<string, object>();
        }

        // Process Slash response modifiers (e.g., Tieqi) before creating response window
        // This allows skills to perform judgements and mark targets as unable to use Dodge
        ProcessSlashResponseModifiers(
            context,
            sourcePlayer,
            slashCard,
            target,
            intermediateResults);

        // Create contexts for response window and handler
        var responseContext = CreateResponseContext(context, intermediateResults);
        var handlerContext = CreateHandlerContext(context, intermediateResults);

        return new SlashSetupResult
        {
            Damage = damage,
            IntermediateResults = intermediateResults,
            ResponseContext = responseContext,
            HandlerContext = handlerContext
        };
    }

    /// <summary>
    /// Creates the response context for the response window.
    /// </summary>
    private static ResolutionContext CreateResponseContext(
        ResolutionContext context,
        Dictionary<string, object> intermediateResults)
    {
        return new ResolutionContext(
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
            intermediateResults,
            context.EventBus,
            context.LogCollector,
            context.SkillManager,
            context.EquipmentSkillRegistry
        );
    }

    /// <summary>
    /// Creates the handler context for the response handler resolver.
    /// </summary>
    private static ResolutionContext CreateHandlerContext(
        ResolutionContext context,
        Dictionary<string, object> intermediateResults)
    {
        return new ResolutionContext(
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
            intermediateResults,
            context.EventBus,
            context.LogCollector,
            context.SkillManager,
            context.EquipmentSkillRegistry
        );
    }

    /// <summary>
    /// Sets up the resolution stack with response window and handler resolver.
    /// </summary>
    private static void SetupResolutionStack(
        ResolutionContext context,
        SlashSetupResult setupResult,
        Player target,
        Card slashCard)
    {
        var sourcePlayer = context.SourcePlayer;

        // Push SlashResponseHandlerResolver onto stack first (will execute after response window due to LIFO)
        context.Stack.Push(new SlashResponseHandlerResolver(setupResult.Damage), setupResult.HandlerContext);

        // Create response window for Jink
        // Include SlashCard in sourceEvent for equipment skills that need to check armor validity
        var responseWindow = setupResult.ResponseContext.CreateJinkResponseWindow(
            targetPlayer: target,
            sourceEvent: new { Type = "Slash", SourceSeat = sourcePlayer.Seat, TargetSeat = target.Seat, SlashCard = slashCard },
            getPlayerChoice: context.GetPlayerChoice!);

        // Push response window onto stack last (will execute first due to LIFO)
        context.Stack.Push(responseWindow, setupResult.ResponseContext);
    }

    /// <summary>
    /// Validates if a card effect is effective on the target.
    /// Checks all card effect filters (e.g., Renwang Shield) and armor ignore providers.
    /// </summary>
    private static bool ValidateCardEffectOnTarget(
        ResolutionContext context,
        CardEffectContext effectContext,
        out EffectVetoReason? vetoReason)
    {
        vetoReason = null;

        // Check if armor should be ignored (e.g., by Qinggang Sword)
        var shouldIgnoreArmor = ShouldIgnoreArmor(context, effectContext);
        
        // If armor is ignored, skip armor-based filters
        if (shouldIgnoreArmor)
        {
            return true; // Effect is effective (armor ignored)
        }

        // Check card effect filters from target's skills
        if (context.SkillManager is not null)
        {
            var targetSkills = context.SkillManager.GetActiveSkills(effectContext.Game, effectContext.TargetPlayer);
            foreach (var skill in targetSkills)
            {
                if (skill is ICardEffectFilteringSkill filteringSkill)
                {
                    var isEffective = filteringSkill.IsEffective(effectContext, out var reason);
                    if (!isEffective)
                    {
                        vetoReason = reason;
                        return false; // Effect is vetoed
                    }
                }
            }
        }

        return true; // Effect is effective
    }

    /// <summary>
    /// Checks if armor effects should be ignored for a card effect.
    /// </summary>
    private static bool ShouldIgnoreArmor(ResolutionContext context, CardEffectContext effectContext)
    {
        // Check if source player has skills/equipment that ignore armor
        if (context.SkillManager is not null)
        {
            var sourceSkills = context.SkillManager.GetActiveSkills(effectContext.Game, effectContext.SourcePlayer);
            foreach (var skill in sourceSkills)
            {
                if (skill is IArmorIgnoreProvider ignoreProvider)
                {
                    if (ignoreProvider.ShouldIgnoreArmor(effectContext))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Processes Slash response modifiers (e.g., Tieqi skill) when Slash targets are confirmed.
    /// This method is called before creating the response window to allow skills to perform
    /// actions like judgement and mark targets as unable to use Dodge.
    /// </summary>
    /// <param name="context">The resolution context.</param>
    /// <param name="sourcePlayer">The player who used the Slash.</param>
    /// <param name="slashCard">The Slash card being used.</param>
    /// <param name="targetPlayer">The target player.</param>
    /// <param name="intermediateResults">The intermediate results dictionary to store results.</param>
    /// <returns>True if the target cannot use Dodge to respond to this Slash, false otherwise.</returns>
    private static bool ProcessSlashResponseModifiers(
        ResolutionContext context,
        Player sourcePlayer,
        Card slashCard,
        Player targetPlayer,
        Dictionary<string, object> intermediateResults)
    {
        if (context.SkillManager is null)
            return false;

        // Check source player's skills for Slash response modifiers
        var sourceSkills = context.SkillManager.GetActiveSkills(context.Game, sourcePlayer);
        foreach (var skill in sourceSkills)
        {
            if (skill is ISlashResponseModifier modifier)
            {
                if (context.JudgementService is null || context.CardMoveService is null)
                    continue;

                var cannotUseDodge = modifier.ProcessSlashTargetConfirmed(
                    context.Game,
                    sourcePlayer,
                    slashCard,
                    targetPlayer,
                    context.JudgementService,
                    context.CardMoveService,
                    context.EventBus);

                if (cannotUseDodge)
                {
                    // Store the result in intermediateResults for the response window to use
                    // Use a key that includes the target seat to support multiple targets
                    var key = $"SlashCannotUseDodge_{slashCard.Id}_{targetPlayer.Seat}";
                    intermediateResults[key] = true;
                    return true;
                }
            }
        }

        return false;
    }
}

/// <summary>
/// Helper class to store validation result for input validation.
/// </summary>
internal sealed class ValidationResult
{
    public bool Success { get; private init; }
    public ResolutionResult? Result { get; private init; }

    private ValidationResult(bool success, ResolutionResult? result = null)
    {
        Success = success;
        Result = result;
    }

    public static ValidationResult CreateSuccess() => new(true);
    public static ValidationResult CreateFailure(ResolutionResult result) => new(false, result);
}

/// <summary>
/// Helper class to store target extraction result.
/// </summary>
internal sealed class TargetExtractionResult
{
    public bool Success { get; private init; }
    public ResolutionResult? Result { get; private init; }
    public Player? Target { get; private init; }

    private TargetExtractionResult(bool success, ResolutionResult? result = null, Player? target = null)
    {
        Success = success;
        Result = result;
        Target = target;
    }

    public static TargetExtractionResult CreateSuccess(Player target) => new(true, target: target);
    public static TargetExtractionResult CreateFailure(ResolutionResult result) => new(false, result);
}

/// <summary>
/// Helper class to store card extraction result.
/// </summary>
internal sealed class CardExtractionResult
{
    public bool Success { get; private init; }
    public ResolutionResult? Result { get; private init; }
    public Card? Card { get; private init; }

    private CardExtractionResult(bool success, ResolutionResult? result = null, Card? card = null)
    {
        Success = success;
        Result = result;
        Card = card;
    }

    public static CardExtractionResult CreateSuccess(Card card) => new(true, card: card);
    public static CardExtractionResult CreateFailure(ResolutionResult result) => new(false, result);
}

/// <summary>
/// Helper class to store effect validation result.
/// </summary>
internal sealed class EffectValidationResult
{
    public bool Success { get; private init; }
    public ResolutionResult? Result { get; private init; }

    private EffectValidationResult(bool success, ResolutionResult? result = null)
    {
        Success = success;
        Result = result;
    }

    public static EffectValidationResult CreateSuccess() => new(true);
    public static EffectValidationResult CreateFailure(ResolutionResult result) => new(false, result);
}

/// <summary>
/// Helper class to store Slash setup result containing all necessary data for resolution.
/// </summary>
internal sealed class SlashSetupResult
{
    public DamageDescriptor Damage { get; init; } = null!;
    public Dictionary<string, object> IntermediateResults { get; init; } = null!;
    public ResolutionContext ResponseContext { get; init; } = null!;
    public ResolutionContext HandlerContext { get; init; } = null!;
    public bool Success => Damage is not null && IntermediateResults is not null && 
                          ResponseContext is not null && HandlerContext is not null;
}
