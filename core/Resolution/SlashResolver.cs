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
            // Check if enough response units were provided
            // Calculate required count based on source player's skills (e.g., Wushuang)
            int requiredCount = 1;
            if (_pendingDamage.CausingCard is not null && context.SkillManager is not null)
            {
                var sourcePlayer = context.Game.Players.FirstOrDefault(p => p.Seat == _pendingDamage.SourceSeat);
                var targetPlayer = context.Game.Players.FirstOrDefault(p => p.Seat == _pendingDamage.TargetSeat);
                if (sourcePlayer is not null && targetPlayer is not null)
                {
                    requiredCount = ResponseRequirementCalculator.CalculateJinkRequirementForSlash(
                        context.Game,
                        sourcePlayer,
                        targetPlayer,
                        _pendingDamage.CausingCard,
                        context.SkillManager);
                }
            }

            // Only consider response successful if enough units were provided
            if (responseResult.ResponseUnitsProvided >= requiredCount)
            {
                // Response successful - slash dodged, no damage
                // Publish AfterSlashDodgedEvent for skills like Stone Axe (贯石斧)
                if (context.EventBus is not null && _pendingDamage.CausingCard is not null)
                {
                    var sourcePlayer = context.Game.Players.FirstOrDefault(p => p.Seat == _pendingDamage.SourceSeat);
                    var targetPlayer = context.Game.Players.FirstOrDefault(p => p.Seat == _pendingDamage.TargetSeat);
                    
                    var afterSlashDodgedEvent = new AfterSlashDodgedEvent(
                        Game: context.Game,
                        AttackerSeat: _pendingDamage.SourceSeat,
                        TargetSeat: _pendingDamage.TargetSeat,
                        SlashCard: _pendingDamage.CausingCard,
                        OriginalDamage: _pendingDamage
                    );
                    context.EventBus.Publish(afterSlashDodgedEvent);
                    
                    // Publish SlashNegatedByJinkEvent for skills like Qinglong Yanyue Dao (青龙偃月刀)
                    if (sourcePlayer is not null && targetPlayer is not null)
                    {
                        var slashNegatedEvent = new SlashNegatedByJinkEvent(
                            Game: context.Game,
                            Source: sourcePlayer,
                            Target: targetPlayer,
                            SlashCard: _pendingDamage.CausingCard,
                            DistanceWasChecked: true // Original slash had distance check
                        );
                        context.EventBus.Publish(slashNegatedEvent);
                    }
                }
            }
            else
            {
                // Insufficient response units - treat as no response and trigger damage
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
                    context.EquipmentSkillRegistry,
                    context.JudgementService
                );

                context.Stack.Push(new DamageResolver(), damageContext);
            }
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

        // Step 2.5: Try to use Jijiang (激将) for use assistance if no card is selected
        var choice = context.Choice!;
        var sourcePlayer = context.SourcePlayer;
        if ((choice.SelectedCardIds is null || choice.SelectedCardIds.Count == 0) &&
            context.SkillManager is not null && context.GetPlayerChoice is not null)
        {
            var jijiangResult = TryUseJijiangForUse(context, sourcePlayer);
            if (jijiangResult.ShouldContinue)
            {
                // Jijiang was activated, wait for it to complete
                return ResolutionResult.SuccessResult;
            }
        }

        // Step 3: Get Slash card
        var cardResult = GetSlashCard(context);
        if (!cardResult.Success)
            return cardResult.Result!;

        var slashCard = cardResult.Card!;

        // Step 4: Validate card effect on target
        var effectValidationResult = ValidateAndHandleCardEffect(context, slashCard, target);
        if (!effectValidationResult.Success)
            return effectValidationResult.Result!;

        // Step 4.5: Publish AfterCardTargetsDeclaredEvent (after targets are finalized, before response window)
        // This allows skills like Twin Swords (雌雄双股剑) to interact with targets before they respond
        if (context.EventBus is not null)
        {
            var afterTargetsDeclaredEvent = new AfterCardTargetsDeclaredEvent(
                Game: context.Game,
                SourcePlayerSeat: context.SourcePlayer.Seat,
                Card: slashCard,
                TargetSeats: new[] { target.Seat }
            );
            context.EventBus.Publish(afterTargetsDeclaredEvent);
        }

        // Step 5: Setup Slash resolution (damage, modifiers, etc.)
        var setupResult = SetupSlashResolution(context, slashCard, target);

        // Step 5.5: Check for target modifying skills (e.g., Liuli)
        // This happens after damage descriptor is created but before response window
        if (context.SkillManager is not null && context.RuleService is not null)
        {
            var targetModifyingSkill = FindSlashTargetModifyingSkill(
                context.SkillManager,
                context.Game,
                target,
                context.SourcePlayer,
                slashCard,
                context.RuleService);
            
            if (targetModifyingSkill is not null)
            {
                // Check if skill can modify target
                if (targetModifyingSkill.CanModifyTarget(
                    context.Game,
                    target,
                    context.SourcePlayer,
                    slashCard,
                    context.RuleService!))
                {
                    // Create target modification resolver
                    var modificationResolver = targetModifyingSkill.CreateTargetModificationResolver(
                        target,
                        context.SourcePlayer,
                        slashCard,
                        setupResult.Damage);
                    
                    if (modificationResolver is not null)
                    {
                        // Push a wrapper resolver that will:
                        // 1. Execute the target modification resolver
                        // 2. Read the modified target from IntermediateResults
                        // 3. Setup the response window with the correct target
                        var wrapperContext = new ResolutionContext(
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
                            setupResult.IntermediateResults,
                            context.EventBus,
                            context.LogCollector,
                            context.SkillManager,
                            context.EquipmentSkillRegistry,
                            context.JudgementService);
                        
                        var wrapperResolver = new SlashTargetModificationWrapperResolver(
                            setupResult,
                            target,
                            slashCard,
                            modificationResolver);
                        
                        context.Stack.Push(wrapperResolver, wrapperContext);
                        return ResolutionResult.SuccessResult;
                    }
                }
            }
        }

        // No target modification - setup resolution stack normally
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
    /// If no card is selected and the source player has Jijiang skill, tries to use Jijiang.
    /// </summary>
    private static CardExtractionResult GetSlashCard(ResolutionContext context)
    {
        var choice = context.Choice!;
        var sourcePlayer = context.SourcePlayer;
        var game = context.Game;

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
            // If still not found, try to find from source player's equipment zone
            if (slashCard is null && sourcePlayer.EquipmentZone.Cards is not null)
            {
                slashCard = sourcePlayer.EquipmentZone.Cards.FirstOrDefault(c => c.Id == cardId);
            }
        }

        // If no card selected, check if Jijiang (激将) was used and get virtual Slash card from intermediate results
        if (slashCard is null && context.IntermediateResults is not null)
        {
            if (context.IntermediateResults.TryGetValue("JijiangUseSlashCard", out var jijiangCardObj) &&
                jijiangCardObj is Card jijiangCard)
            {
                return CardExtractionResult.CreateSuccess(jijiangCard);
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
    /// Tries to use Jijiang (激将) skill for active Slash use.
    /// If successful, pushes resolvers to handle the assistance flow.
    /// </summary>
    private static JijiangUseResult TryUseJijiangForUse(ResolutionContext context, Player sourcePlayer)
    {
        var game = context.Game;
        var skillManager = context.SkillManager!;
        var getPlayerChoice = context.GetPlayerChoice!;

        // Find Jijiang skill
        var sourcePlayerSkills = skillManager.GetActiveSkills(game, sourcePlayer);
        var jijiangSkill = sourcePlayerSkills.OfType<Skills.Hero.JijiangSkill>().FirstOrDefault();

        if (jijiangSkill is null)
        {
            return JijiangUseResult.DoNotContinue();
        }

        // Check if Jijiang can provide assistance for use
        if (!jijiangSkill.CanProvideAssistanceForUse(game, sourcePlayer))
        {
            return JijiangUseResult.DoNotContinue();
        }

        // Ask source player if they want to use Jijiang
        if (!jijiangSkill.ShouldActivate(game, sourcePlayer, getPlayerChoice))
        {
            return JijiangUseResult.DoNotContinue();
        }

        // Source player wants to use Jijiang - push JijiangUseAssistanceResolver
        var assistanceContext = new ResolutionContext(
            game,
            sourcePlayer,
            context.Action,
            context.Choice,
            context.Stack,
            context.CardMoveService,
            context.RuleService,
            context.PendingDamage,
            context.LogSink,
            getPlayerChoice,
            context.IntermediateResults,
            context.EventBus,
            context.LogCollector,
            skillManager,
            context.EquipmentSkillRegistry,
            context.JudgementService);

        var assistanceResolver = new JijiangUseAssistanceResolver(
            beneficiary: sourcePlayer,
            assistanceSkill: jijiangSkill);

        // Push handler resolver that will re-enter SlashResolver after Jijiang completes
        var handlerContext = new ResolutionContext(
            game,
            sourcePlayer,
            context.Action,
            context.Choice,
            context.Stack,
            context.CardMoveService,
            context.RuleService,
            context.PendingDamage,
            context.LogSink,
            getPlayerChoice,
            context.IntermediateResults,
            context.EventBus,
            context.LogCollector,
            skillManager,
            context.EquipmentSkillRegistry,
            context.JudgementService);

        // Push SlashResolver again to continue with the virtual Slash card
        context.Stack.Push(new SlashResolver(), handlerContext);

        // Push use assistance resolver (will execute first due to LIFO)
        context.Stack.Push(assistanceResolver, assistanceContext);

        return JijiangUseResult.Continue();
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

        // Initialize IntermediateResults dictionary if not present
        // This dictionary will be shared across all resolvers in this resolution chain
        var intermediateResults = context.IntermediateResults;
        if (intermediateResults is null)
        {
            intermediateResults = new Dictionary<string, object>();
        }

        // Extract original cards from conversion if available (for multi-card conversion like Serpent Spear)
        IReadOnlyList<Card>? causingCards = null;
        if (intermediateResults.TryGetValue("ConversionOriginalCards", out var originalCardsObj) &&
            originalCardsObj is IReadOnlyList<Card> originalCards)
        {
            causingCards = originalCards;
        }

        // Create damage descriptor (will be used if no response is made)
        // For multi-card conversion, pass the original cards so skills like Jianxiong can obtain them
        var damage = new DamageDescriptor(
            SourceSeat: sourcePlayer.Seat,
            TargetSeat: target.Seat,
            Amount: 1,  // Basic Slash deals 1 damage
            Type: DamageType.Normal,
            Reason: "Slash",
            CausingCard: slashCard,  // The virtual Slash card that causes the damage
            CausingCards: causingCards  // The original cards used for conversion (e.g., two hand cards for Serpent Spear)
        );

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
    /// Uses DodgeProviderChainResolver to handle priority: Response Assistance > Bagua Array > Manual Dodge.
    /// </summary>
    internal static void SetupResolutionStack(
        ResolutionContext context,
        SlashSetupResult setupResult,
        Player target,
        Card slashCard)
    {
        var sourcePlayer = context.SourcePlayer;

        // Push SlashResponseHandlerResolver onto stack first (will execute after response window due to LIFO)
        context.Stack.Push(new SlashResponseHandlerResolver(setupResult.Damage), setupResult.HandlerContext);

        // Create Dodge request context
        var sourceEvent = new { Type = "Slash", SourceSeat = sourcePlayer.Seat, TargetSeat = target.Seat, SlashCard = slashCard };
        var dodgeRequestContext = new Response.DodgeRequestContext(
            defender: target,
            attacker: sourcePlayer,
            sourceEvent: sourceEvent);

        // Create and push DodgeProviderChainResolver
        // This will try providers in priority order: Response Assistance (Hujia) > Bagua Array > Manual Dodge
        var dodgeChainResolver = new Response.DodgeProviderChainResolver(dodgeRequestContext);
        context.Stack.Push(dodgeChainResolver, setupResult.ResponseContext);
    }

    /// <summary>
    /// Finds a Slash target modifying skill for the target player.
    /// </summary>
    private static ISlashTargetModifyingSkill? FindSlashTargetModifyingSkill(
        SkillManager skillManager,
        Game game,
        Player target,
        Player attacker,
        Card slashCard,
        IRuleService ruleService)
    {
        if (skillManager is null || !target.IsAlive)
            return null;

        var targetSkills = skillManager.GetActiveSkills(game, target);
        foreach (var skill in targetSkills)
        {
            if (skill is ISlashTargetModifyingSkill modifyingSkill)
            {
                // Check if this skill can modify the target
                if (modifyingSkill.CanModifyTarget(game, target, attacker, slashCard, ruleService))
                {
                    return modifyingSkill;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if target has a response assistance skill and wants to use it.
    /// If yes, pushes ResponseAssistanceResolver onto the stack.
    /// </summary>
    /// <returns>True if response assistance was activated, false otherwise.</returns>
    private static bool TryUseResponseAssistance(
        ResolutionContext context,
        SlashSetupResult setupResult,
        Player target,
        Card slashCard,
        Player sourcePlayer)
    {
        // Check if SkillManager is available
        if (context.SkillManager is null || context.GetPlayerChoice is null)
            return false;

        // Find any response assistance skill for the target
        var targetSkills = context.SkillManager.GetActiveSkills(context.Game, target);
        var assistanceSkill = targetSkills.OfType<Skills.IResponseAssistanceSkill>()
            .FirstOrDefault();

        if (assistanceSkill is null)
            return false;

        // Check if the skill can provide assistance for this response type
        var sourceEvent = new { Type = "Slash", SourceSeat = sourcePlayer.Seat, TargetSeat = target.Seat, SlashCard = slashCard };
        if (!assistanceSkill.CanProvideAssistance(context.Game, target, ResponseType.JinkAgainstSlash, sourceEvent))
            return false;

        // Ask target if they want to use the assistance skill
        if (!assistanceSkill.ShouldActivate(context.Game, target, context.GetPlayerChoice))
            return false;

        // Target wants to use response assistance - push ResponseAssistanceResolver
        var assistanceContext = new ResolutionContext(
            context.Game,
            target, // Beneficiary is the target
            Action: null,
            Choice: null,
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
            context.JudgementService);

        var assistanceResolver = new ResponseAssistanceResolver(
            beneficiary: target,
            responseType: ResponseType.JinkAgainstSlash,
            sourceEvent: sourceEvent,
            assistanceSkill: assistanceSkill);

        // Push ResponseAssistanceResolver onto stack (will execute first due to LIFO)
        context.Stack.Push(assistanceResolver, assistanceContext);

        return true;
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
    public bool ShouldContinue { get; private init; }

    private CardExtractionResult(bool success, ResolutionResult? result = null, Card? card = null, bool shouldContinue = false)
    {
        Success = success;
        Result = result;
        Card = card;
        ShouldContinue = shouldContinue;
    }

    public static CardExtractionResult CreateSuccess(Card card) => new(true, card: card);
    public static CardExtractionResult CreateFailure(ResolutionResult result) => new(false, result);
    public static CardExtractionResult CreateContinue() => new(false, shouldContinue: true);
}

/// <summary>
/// Helper class to store Jijiang use attempt result.
/// </summary>
internal sealed class JijiangUseResult
{
    public bool ShouldContinue { get; private init; }

    private JijiangUseResult(bool shouldContinue)
    {
        ShouldContinue = shouldContinue;
    }

    public static JijiangUseResult Continue() => new(true);
    public static JijiangUseResult DoNotContinue() => new(false);
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

/// <summary>
/// Wrapper resolver that handles target modification for Slash.
/// This resolver:
/// 1. Executes the target modification resolver (e.g., Liuli)
/// 2. Reads the modified target from IntermediateResults
/// 3. Sets up the response window with the correct target
/// </summary>
internal sealed class SlashTargetModificationWrapperResolver : IResolver
{
    private readonly SlashSetupResult _setupResult;
    private readonly Player _originalTarget;
    private readonly Card _slashCard;
    private readonly IResolver _modificationResolver;

    public SlashTargetModificationWrapperResolver(
        SlashSetupResult setupResult,
        Player originalTarget,
        Card slashCard,
        IResolver modificationResolver)
    {
        _setupResult = setupResult ?? throw new ArgumentNullException(nameof(setupResult));
        _originalTarget = originalTarget ?? throw new ArgumentNullException(nameof(originalTarget));
        _slashCard = slashCard ?? throw new ArgumentNullException(nameof(slashCard));
        _modificationResolver = modificationResolver ?? throw new ArgumentNullException(nameof(modificationResolver));
    }

    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        // Step 1: Execute the target modification resolver
        var modificationContext = new ResolutionContext(
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
            _setupResult.IntermediateResults,
            context.EventBus,
            context.LogCollector,
            context.SkillManager,
            context.EquipmentSkillRegistry,
            context.JudgementService);

        var modificationResult = _modificationResolver.Resolve(modificationContext);
        if (!modificationResult.Success)
        {
            // Modification failed, use original target
            SlashResolver.SetupResolutionStack(context, _setupResult, _originalTarget, _slashCard);
            return ResolutionResult.SuccessResult;
        }

        // Step 2: Read the modified target from IntermediateResults
        var intermediateResults = _setupResult.IntermediateResults;
        Player? actualTarget = _originalTarget;
        DamageDescriptor actualDamage = _setupResult.Damage;

        if (intermediateResults is not null && intermediateResults.TryGetValue("LiuliNewTargetSeat", out var newTargetSeatObj))
        {
            if (newTargetSeatObj is int newTargetSeat)
            {
                var newTarget = context.Game.Players.FirstOrDefault(p => p.Seat == newTargetSeat);
                if (newTarget is not null && newTarget.IsAlive)
                {
                    actualTarget = newTarget;

                    // Update damage descriptor if it was modified
                    if (intermediateResults.TryGetValue("SlashPendingDamage", out var modifiedDamageObj) &&
                        modifiedDamageObj is DamageDescriptor modifiedDamage)
                    {
                        actualDamage = modifiedDamage;
                    }
                    else
                    {
                        // Create new damage descriptor with new target
                        actualDamage = new DamageDescriptor(
                            SourceSeat: _setupResult.Damage.SourceSeat,
                            TargetSeat: newTargetSeat,
                            Amount: _setupResult.Damage.Amount,
                            Type: _setupResult.Damage.Type,
                            Reason: _setupResult.Damage.Reason,
                            CausingCard: _setupResult.Damage.CausingCard,
                            CausingCards: _setupResult.Damage.CausingCards
                        );
                    }

                    // Update setupResult with modified damage
                    var updatedSetupResult = new SlashSetupResult
                    {
                        Damage = actualDamage,
                        IntermediateResults = _setupResult.IntermediateResults,
                        ResponseContext = _setupResult.ResponseContext,
                        HandlerContext = _setupResult.HandlerContext
                    };

                    // Step 3: Setup resolution stack with modified target
                    SlashResolver.SetupResolutionStack(context, updatedSetupResult, actualTarget, _slashCard);
                    return ResolutionResult.SuccessResult;
                }
            }
        }

        // No target modification occurred, use original target
        SlashResolver.SetupResolutionStack(context, _setupResult, _originalTarget, _slashCard);
        return ResolutionResult.SuccessResult;
    }
}
