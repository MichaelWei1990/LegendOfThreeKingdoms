using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Extension methods for registering resolution handlers.
/// </summary>
public static class ResolutionExtensions
{
    /// <summary>
    /// Registers the UseSlash action handler that uses the resolution pipeline.
    /// </summary>
    /// <param name="mapper">The action resolution mapper to register with.</param>
    /// <param name="cardMoveService">The card move service for card operations.</param>
    /// <param name="ruleService">The rule service for validation.</param>
    public static void RegisterUseSlashHandler(
        this ActionResolutionMapper mapper,
        ICardMoveService cardMoveService,
        IRuleService ruleService)
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
                ruleService
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
