using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Response;
using LegendOfThreeKingdoms.Core.Rules;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Helper class for nullification (无懈可击) operations.
/// </summary>
public static class NullificationHelper
{
    /// <summary>
    /// Opens a nullification window for a nullifiable effect.
    /// Supports chain nullification (无懈可以无懈无懈).
    /// </summary>
    /// <param name="context">The resolution context.</param>
    /// <param name="effect">The nullifiable effect instance.</param>
    /// <param name="resultKey">The key to store the nullification result in IntermediateResults.</param>
    /// <returns>True if the window was opened, false if the effect is not nullifiable.</returns>
    public static bool OpenNullificationWindow(
        ResolutionContext context,
        INullifiableEffect effect,
        string resultKey)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (effect is null) throw new ArgumentNullException(nameof(effect));
        if (string.IsNullOrWhiteSpace(resultKey)) throw new ArgumentException("Result key cannot be null or empty.", nameof(resultKey));

        if (!effect.IsNullifiable)
        {
            // Effect cannot be nullified, store result as not nullified
            if (context.IntermediateResults is not null)
            {
                context.IntermediateResults[resultKey] = new NullificationResult(IsNullified: false, NullificationCount: 0);
            }
            return false;
        }

        // If GetPlayerChoice is null, skip nullification window and store result as not nullified
        // This is useful for tests or scenarios where player interaction is not available
        if (context.GetPlayerChoice is null)
        {
            if (context.IntermediateResults is not null)
            {
                context.IntermediateResults[resultKey] = new NullificationResult(IsNullified: false, NullificationCount: 0);
            }
            return false;
        }

        // Create responder order: start from target player, then in turn order
        var game = context.Game;
        var targetPlayer = effect.TargetPlayer;
        var responderOrder = GetResponderOrder(game, targetPlayer);

        // Create response window context
        var intermediateResults = context.IntermediateResults ?? new Dictionary<string, object>();
        var responseRuleService = new ResponseRuleService(context.SkillManager);
        
        var responseContext = new ResponseWindowContext(
            Game: game,
            ResponseType: ResponseType.Nullification,
            ResponderOrder: responderOrder,
            SourceEvent: effect,
            RuleService: context.RuleService,
            ResponseRuleService: responseRuleService,
            ChoiceFactory: new ChoiceRequestFactory(),
            CardMoveService: context.CardMoveService,
            LogSink: context.LogSink,
            SkillManager: context.SkillManager,
            JudgementService: context.JudgementService,
            EventBus: context.EventBus,
            IntermediateResults: intermediateResults
        );

        // Create nullification window resolver
        var nullificationWindow = new NullificationWindowResolver(
            responseContext,
            effect,
            resultKey,
            context.GetPlayerChoice);

        // Push nullification window onto stack
        var windowContext = new ResolutionContext(
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
            context.EquipmentSkillRegistry,
            context.JudgementService
        );

        context.Stack.Push(nullificationWindow, windowContext);

        return true;
    }

    /// <summary>
    /// Determines if an effect is nullified based on the nullification count.
    /// Odd count = nullified, even count = not nullified.
    /// </summary>
    /// <param name="nullificationCount">The number of nullifications in the chain.</param>
    /// <returns>True if the effect is nullified, false otherwise.</returns>
    public static bool IsNullified(int nullificationCount)
    {
        // Odd count means nullified (1st nullification nullifies, 2nd cancels the 1st, etc.)
        return nullificationCount % 2 == 1;
    }

    /// <summary>
    /// Creates a nullifiable effect instance.
    /// </summary>
    public static INullifiableEffect CreateNullifiableEffect(
        string effectKey,
        Player targetPlayer,
        Card? causingCard,
        bool isNullifiable = true)
    {
        return new NullifiableEffect(effectKey, targetPlayer, causingCard, isNullifiable);
    }

    /// <summary>
    /// Gets the responder order for nullification, starting from the target player.
    /// </summary>
    private static IReadOnlyList<Player> GetResponderOrder(Game game, Player targetPlayer)
    {
        var players = game.Players;
        var total = players.Count;

        if (total == 0)
        {
            return Array.Empty<Player>();
        }

        // Find the index of the target player
        var targetIndex = -1;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].Seat == targetPlayer.Seat)
            {
                targetIndex = i;
                break;
            }
        }

        if (targetIndex < 0)
        {
            return Array.Empty<Player>();
        }

        // Collect alive players in turn order starting from target player
        var result = new List<Player>();
        for (int i = 0; i < total; i++)
        {
            var index = (targetIndex + i) % total;
            var player = players[index];
            if (player.IsAlive)
            {
                result.Add(player);
            }
        }

        return result;
    }

    /// <summary>
    /// Simple implementation of INullifiableEffect.
    /// </summary>
    private sealed record NullifiableEffect(
        string EffectKey,
        Player TargetPlayer,
        Card? CausingCard,
        bool IsNullifiable) : INullifiableEffect;
}

/// <summary>
/// Result of a nullification window execution.
/// </summary>
public sealed record NullificationResult(
    bool IsNullified,
    int NullificationCount);
