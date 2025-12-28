using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Judgement;
using LegendOfThreeKingdoms.Core.Logging;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Extension methods for registering resolution handlers.
/// </summary>
public static class ResolutionExtensions
{
    /// <summary>
    /// Executes draw phase logic: draws cards for the current player.
    /// This method should be called when entering Draw Phase.
    /// </summary>
    /// <param name="stack">The resolution stack to use for execution.</param>
    /// <param name="context">The resolution context containing game state and dependencies.</param>
    public static void ExecuteDrawPhase(
        this IResolutionStack stack,
        ResolutionContext context)
    {
        if (stack is null)
            throw new ArgumentNullException(nameof(stack));
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        var resolver = new DrawPhaseResolver();
        stack.Push(resolver, context);

        // Execute immediately
        while (!stack.IsEmpty)
        {
            var result = stack.Pop();
            if (!result.Success)
            {
                throw new InvalidOperationException(
                    $"Draw phase failed: {result.MessageKey ?? result.ErrorCode?.ToString()}");
            }
        }
    }
    /// <summary>
    /// Prepares a ResolutionContext with card conversion logic applied.
    /// This method resolves card conversions before creating the context, allowing resolvers
    /// to focus on processing cards without knowing about conversion logic.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="sourcePlayer">The player using the card.</param>
    /// <param name="action">The action descriptor.</param>
    /// <param name="choice">The player's choice.</param>
    /// <param name="stack">The resolution stack.</param>
    /// <param name="cardMoveService">The card move service.</param>
    /// <param name="ruleService">The rule service.</param>
    /// <param name="skillManager">The skill manager for checking conversion skills.</param>
    /// <param name="getPlayerChoice">Function to get player choice for response windows.</param>
    /// <param name="eventBus">The event bus.</param>
    /// <param name="logCollector">The log collector.</param>
    /// <param name="equipmentSkillRegistry">The equipment skill registry.</param>
    /// <param name="judgementService">The judgement service.</param>
    /// <returns>A ResolutionContext with conversion information in IntermediateResults.</returns>
    public static ResolutionContext CreateResolutionContextWithCardConversion(
        Game game,
        Player sourcePlayer,
        ActionDescriptor action,
        ChoiceResult choice,
        IResolutionStack stack,
        ICardMoveService cardMoveService,
        IRuleService ruleService,
        SkillManager? skillManager = null,
        Func<ChoiceRequest, ChoiceResult>? getPlayerChoice = null,
        IEventBus? eventBus = null,
        ILogCollector? logCollector = null,
        EquipmentSkillRegistry? equipmentSkillRegistry = null,
        IJudgementService? judgementService = null)
    {
        // Extract the selected card
        var selectedCardIds = choice.SelectedCardIds;
        if (selectedCardIds is null || selectedCardIds.Count == 0)
        {
            throw new ArgumentException("Choice must contain at least one selected card ID.", nameof(choice));
        }

        var cardId = selectedCardIds[0];
        var selectedCard = sourcePlayer.HandZone.Cards.FirstOrDefault(c => c.Id == cardId);
        if (selectedCard is null)
        {
            throw new InvalidOperationException($"Card with ID {cardId} not found in player's hand.");
        }

        // Resolve card conversion
        var (actualCard, originalCard, conversionSkill) = CardConversionHelper.ResolveCardForAction(
            action,
            selectedCard,
            game,
            sourcePlayer,
            skillManager);

        // Prepare IntermediateResults with conversion information
        var intermediateResults = CardConversionHelper.PrepareIntermediateResults(
            actualCard,
            originalCard,
            conversionSkill);

        // Create and return the context
        return new ResolutionContext(
            game,
            sourcePlayer,
            action,
            choice,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: getPlayerChoice,
            IntermediateResults: intermediateResults,
            EventBus: eventBus,
            LogCollector: logCollector,
            SkillManager: skillManager,
            EquipmentSkillRegistry: equipmentSkillRegistry,
            JudgementService: judgementService);
    }

    /// <summary>
    /// Registers the UseSlash action handler that uses the resolution pipeline.
    /// </summary>
    /// <param name="mapper">The action resolution mapper to register with.</param>
    /// <param name="cardMoveService">The card move service for card operations.</param>
    /// <param name="ruleService">The rule service for validation.</param>
    /// <param name="getPlayerChoice">Function to get player choice for response windows. May be null if response windows are not supported.</param>
    public static void RegisterUseSlashHandler(
        this ActionResolutionMapper mapper,
        ICardMoveService cardMoveService,
        IRuleService ruleService,
        Func<ChoiceRequest, ChoiceResult>? getPlayerChoice = null)
    {
        if (mapper is null) throw new ArgumentNullException(nameof(mapper));
        if (cardMoveService is null) throw new ArgumentNullException(nameof(cardMoveService));
        if (ruleService is null) throw new ArgumentNullException(nameof(ruleService));

        mapper.Register("UseSlash", (context, action, originalRequest, playerChoice) =>
        {
            // Create resolution stack
            var stack = new BasicResolutionStack();

            // Create resolution context with card conversion logic applied
            var resolutionContext = CreateResolutionContextWithCardConversion(
                context.Game,
                context.CurrentPlayer,
                action,
                playerChoice,
                stack,
                cardMoveService,
                ruleService,
                skillManager: null, // TODO: Get SkillManager from context if available
                getPlayerChoice: getPlayerChoice,
                eventBus: null,
                logCollector: null,
                equipmentSkillRegistry: null,
                judgementService: null);

            // Create and push UseCardResolver
            var useCardResolver = new UseCardResolver();
            stack.Push(useCardResolver, resolutionContext);

            // Execute all resolvers in the stack
            while (!stack.IsEmpty)
            {
                var result = stack.Pop();
                if (!result.Success)
                {
                    // If any resolver fails, throw an exception with error details
                    throw new InvalidOperationException(
                        $"Resolution failed: {result.MessageKey ?? result.ErrorCode?.ToString()}");
                }
            }
        });
    }

    /// <summary>
    /// Registers the UseQingnang action handler that uses the resolution pipeline.
    /// </summary>
    /// <param name="mapper">The action resolution mapper to register with.</param>
    /// <param name="cardMoveService">The card move service for card operations.</param>
    /// <param name="ruleService">The rule service for validation.</param>
    /// <param name="getPlayerChoice">Function to get player choice for response windows. May be null if response windows are not supported.</param>
    public static void RegisterUseQingnangHandler(
        this ActionResolutionMapper mapper,
        ICardMoveService cardMoveService,
        IRuleService ruleService,
        Func<ChoiceRequest, ChoiceResult>? getPlayerChoice = null)
    {
        if (mapper is null) throw new ArgumentNullException(nameof(mapper));
        if (cardMoveService is null) throw new ArgumentNullException(nameof(cardMoveService));
        if (ruleService is null) throw new ArgumentNullException(nameof(ruleService));

        mapper.Register("UseQingnang", (context, action, originalRequest, playerChoice) =>
        {
            if (playerChoice is null)
            {
                throw new InvalidOperationException("UseQingnang requires a player choice");
            }

            // Create resolution stack
            var stack = new BasicResolutionStack();

            // Create resolution context for skill usage (no card conversion needed)
            var resolutionContext = new ResolutionContext(
                context.Game,
                context.CurrentPlayer,
                action,
                playerChoice,
                stack,
                cardMoveService,
                ruleService,
                PendingDamage: null,
                LogSink: null,
                GetPlayerChoice: getPlayerChoice,
                IntermediateResults: null,
                EventBus: null,
                LogCollector: null,
                SkillManager: null,
                EquipmentSkillRegistry: null,
                JudgementService: null);

            // Create and push QingnangResolver
            var qingnangResolver = new QingnangResolver();
            stack.Push(qingnangResolver, resolutionContext);

            // Execute all resolvers in the stack
            while (!stack.IsEmpty)
            {
                var result = stack.Pop();
                if (!result.Success)
                {
                    // If any resolver fails, throw an exception with error details
                    throw new InvalidOperationException(
                        $"Resolution failed: {result.MessageKey ?? result.ErrorCode?.ToString()}");
                }
            }
        });
    }

    /// <summary>
    /// Registers the UseKurou action handler that uses the resolution pipeline.
    /// </summary>
    /// <param name="mapper">The action resolution mapper to register with.</param>
    /// <param name="cardMoveService">The card move service for card operations.</param>
    /// <param name="ruleService">The rule service for validation.</param>
    /// <param name="getPlayerChoice">Function to get player choice for response windows. May be null if response windows are not supported.</param>
    public static void RegisterUseKurouHandler(
        this ActionResolutionMapper mapper,
        ICardMoveService cardMoveService,
        IRuleService ruleService,
        Func<ChoiceRequest, ChoiceResult>? getPlayerChoice = null)
    {
        if (mapper is null) throw new ArgumentNullException(nameof(mapper));
        if (cardMoveService is null) throw new ArgumentNullException(nameof(cardMoveService));
        if (ruleService is null) throw new ArgumentNullException(nameof(ruleService));

        mapper.Register("UseKurou", (context, action, originalRequest, playerChoice) =>
        {
            // Create resolution stack
            var stack = new BasicResolutionStack();

            // Create resolution context for skill usage
            var resolutionContext = new ResolutionContext(
                context.Game,
                context.CurrentPlayer,
                action,
                playerChoice,
                stack,
                cardMoveService,
                ruleService,
                PendingDamage: null,
                LogSink: null,
                GetPlayerChoice: getPlayerChoice,
                IntermediateResults: null,
                EventBus: null,
                LogCollector: null,
                SkillManager: null,
                EquipmentSkillRegistry: null,
                JudgementService: null);

            // Create and push Kurou main resolver using the skill class method
            // This keeps all logic centralized in the skill class
            var kurouResolver = Skills.Hero.KurouSkill.CreateMainResolver(context.CurrentPlayer);
            stack.Push(kurouResolver, resolutionContext);

            // Execute all resolvers in the stack
            while (!stack.IsEmpty)
            {
                var result = stack.Pop();
                if (!result.Success)
                {
                    // If any resolver fails, throw an exception with error details
                    throw new InvalidOperationException(
                        $"Resolution failed: {result.MessageKey ?? result.ErrorCode?.ToString()}");
                }
            }
        });
    }
}
