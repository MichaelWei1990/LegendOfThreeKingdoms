using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Kurou (苦肉) skill: Active skill that allows losing 1 HP to draw 2 cards during play phase.
/// Can be used multiple times per play phase, but may cause the player to enter dying state.
/// </summary>
public sealed class KurouSkill : BaseSkill, IActionProvidingSkill, IActiveHpLossSkill
{
    /// <inheritdoc />
    public override string Id => "kurou";

    /// <inheritdoc />
    public override string Name => "苦肉";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Active;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.InitiatesChoices;

    /// <inheritdoc />
    public override void Attach(Game game, Player owner, IEventBus eventBus)
    {
        // No event subscriptions needed - logic is handled in KurouResolver
    }

    /// <inheritdoc />
    public override void Detach(Game game, Player owner, IEventBus eventBus)
    {
        // No cleanup needed
    }

    /// <inheritdoc />
    public int GetHpLossAmount(Game game, Player owner)
    {
        // Kurou skill loses 1 HP
        return 1;
    }

    /// <inheritdoc />
    public void OnAfterHpLost(AfterHpLostEvent evt)
    {
        // Kurou skill does not respond to HP loss events directly.
        // The card drawing is handled by KurouDrawHandlerResolver after HP loss is resolved.
        // This method is implemented to satisfy IActiveHpLossSkill interface,
        // but the actual logic is in the resolver.
    }

    /// <summary>
    /// Creates the main resolver for Kurou skill execution flow.
    /// This method centralizes all resolver creation and orchestration logic in the skill class.
    /// </summary>
    /// <param name="owner">The player who owns the Kurou skill.</param>
    /// <returns>A resolver that orchestrates the entire Kurou skill execution flow.</returns>
    public static IResolver CreateMainResolver(Player owner)
    {
        if (owner is null)
            throw new ArgumentNullException(nameof(owner));

        return new KurouMainResolver(owner);
    }

    /// <summary>
    /// Creates a resolver that handles drawing cards after HP loss in Kurou skill.
    /// This method centralizes the resolver creation logic in the skill class.
    /// </summary>
    /// <param name="owner">The player who owns the Kurou skill.</param>
    /// <returns>A resolver that will draw 2 cards if the player is still alive.</returns>
    public static IResolver CreateDrawHandlerResolver(Player owner)
    {
        if (owner is null)
            throw new ArgumentNullException(nameof(owner));

        return new KurouDrawHandlerResolver(owner);
    }

    /// <summary>
    /// Main resolver for Kurou skill that orchestrates the execution flow.
    /// This class is nested in KurouSkill to keep all logic centralized.
    /// </summary>
    private sealed class KurouMainResolver : IResolver
    {
        private readonly Player _owner;

        public KurouMainResolver(Player owner)
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
            var drawHandlerResolver = CreateDrawHandlerResolver(_owner);
            context.Stack.Push(drawHandlerResolver, context);

            // Push LoseHpResolver to handle HP loss (and dying if needed)
            context.Stack.Push(new Resolution.LoseHpResolver(_owner.Seat, 1, new { Type = "Skill", SkillId = "kurou", SkillName = "苦肉" }), loseHpContext);

            return ResolutionResult.SuccessResult;
        }
    }

    /// <summary>
    /// Resolver that handles drawing cards after HP loss in Kurou skill.
    /// Only draws if the player is still alive.
    /// This class is nested in KurouSkill to keep the logic centralized.
    /// </summary>
    private sealed class KurouDrawHandlerResolver : IResolver
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

    /// <inheritdoc />
    public ActionDescriptor? GenerateAction(Game game, Player owner)
    {
        // Check conditions:
        // 1. Must be in play phase
        if (game.CurrentPhase != Phase.Play)
            return null;

        // 2. Must be owner's turn
        if (game.CurrentPlayerSeat != owner.Seat)
            return null;

        // 3. Owner must be alive
        if (!owner.IsAlive)
            return null;

        // 4. No other conditions - can be used even at 1 HP (will enter dying)
        // Note: The skill can be used multiple times per play phase (no usage limit)

        // Create action (no targets or cards required)
        return new ActionDescriptor(
            ActionId: "UseKurou",
            DisplayKey: "action.useKurou",
            RequiresTargets: false,
            TargetConstraints: null,
            CardCandidates: null);
    }
}

/// <summary>
/// Factory for creating KurouSkill instances.
/// </summary>
public sealed class KurouSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new KurouSkill();
    }
}

