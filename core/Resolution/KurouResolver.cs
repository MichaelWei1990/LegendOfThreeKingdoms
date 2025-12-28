using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Resolver for Kurou (苦肉) skill.
/// Handles the resolution flow: lose 1 HP, then draw 2 cards (if still alive).
/// </summary>
public sealed class KurouResolver : IResolver
{
    private readonly Player _owner;

    /// <summary>
    /// Initializes a new instance of the <see cref="KurouResolver"/> class.
    /// </summary>
    /// <param name="owner">The player who owns the Kurou skill.</param>
    public KurouResolver(Player owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var game = context.Game;
        var cardMoveService = context.CardMoveService;

        if (cardMoveService is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.kurou.missingCardMoveService");
        }

        // Step 1: Lose 1 HP
        // Store the owner's seat in IntermediateResults for the draw step
        var intermediateResults = context.IntermediateResults ?? new Dictionary<string, object>();
        intermediateResults["KurouOwnerSeat"] = _owner.Seat;

        // Create context for LoseHpResolver
        var loseHpContext = new ResolutionContext(
            game,
            _owner,
            context.Action,
            context.Choice,
            context.Stack,
            cardMoveService,
            context.RuleService,
            PendingDamage: null,
            context.LogSink,
            context.GetPlayerChoice,
            intermediateResults,
            context.EventBus,
            context.LogCollector,
            context.SkillManager,
            context.EquipmentSkillRegistry,
            context.JudgementService);

        // Push KurouDrawHandlerResolver onto stack first (will execute after HP loss and dying process due to LIFO)
        context.Stack.Push(new KurouDrawHandlerResolver(_owner), context);

        // Push LoseHpResolver to handle HP loss (and dying if needed)
        context.Stack.Push(new LoseHpResolver(_owner.Seat, 1, new { Type = "Skill", SkillId = "kurou", SkillName = "苦肉" }), loseHpContext);

        return ResolutionResult.SuccessResult;
    }
}

/// <summary>
/// Resolver that handles drawing cards after HP loss in Kurou skill.
/// Only draws if the player is still alive.
/// </summary>
internal sealed class KurouDrawHandlerResolver : IResolver
{
    private readonly Player _owner;

    public KurouDrawHandlerResolver(Player owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var game = context.Game;
        var cardMoveService = context.CardMoveService;

        if (cardMoveService is null)
        {
            return ResolutionResult.SuccessResult; // Skip if service is missing
        }

        // Check if owner is still alive
        var owner = game.Players.FirstOrDefault(p => p.Seat == _owner.Seat);
        if (owner is null || !owner.IsAlive)
        {
            // Player is dead - do not draw cards
            return ResolutionResult.SuccessResult;
        }

        // Player is alive - draw 2 cards
        try
        {
            cardMoveService.DrawCards(game, owner, 2);
        }
        catch (Exception)
        {
            // If drawing fails (e.g., draw pile empty), silently ignore
            // This matches the behavior of other skills
        }

        return ResolutionResult.SuccessResult;
    }
}

