using System;
using LegendOfThreeKingdoms.Core.Rules;
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

            // Create resolution context
            var resolutionContext = new ResolutionContext(
                context.Game,
                context.CurrentPlayer,
                action,
                playerChoice,
                stack,
                cardMoveService,
                ruleService,
                GetPlayerChoice: getPlayerChoice,
                EventBus: null
            );

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
}
