using System;
using System.Linq;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Judgement;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Tricks;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Phases;

/// <summary>
/// Service that handles judge phase logic by listening to PhaseStartEvent.
/// When Judge Phase starts, it automatically checks for delayed tricks in the player's judgement zone
/// and triggers judgement for each delayed trick.
/// </summary>
public sealed class JudgePhaseService
{
    private readonly ICardMoveService _cardMoveService;
    private readonly IRuleService _ruleService;
    private readonly IEventBus _eventBus;
    private readonly IResolutionStack _resolutionStack;
    private readonly IJudgementService? _judgementService;

    /// <summary>
    /// Creates a new JudgePhaseService.
    /// </summary>
    /// <param name="cardMoveService">The card move service for moving cards.</param>
    /// <param name="ruleService">The rule service for rule queries.</param>
    /// <param name="eventBus">The event bus to subscribe to phase start events.</param>
    /// <param name="resolutionStack">The resolution stack for executing judgement resolvers.</param>
    /// <param name="judgementService">Optional judgement service. If null, a default BasicJudgementService will be used.</param>
    /// <exception cref="ArgumentNullException">Thrown if any required parameter is null.</exception>
    public JudgePhaseService(
        ICardMoveService cardMoveService,
        IRuleService ruleService,
        IEventBus eventBus,
        IResolutionStack resolutionStack,
        IJudgementService? judgementService = null)
    {
        _cardMoveService = cardMoveService ?? throw new ArgumentNullException(nameof(cardMoveService));
        _ruleService = ruleService ?? throw new ArgumentNullException(nameof(ruleService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _resolutionStack = resolutionStack ?? throw new ArgumentNullException(nameof(resolutionStack));
        _judgementService = judgementService;

        _eventBus.Subscribe<PhaseStartEvent>(OnPhaseStart);
    }

    private void OnPhaseStart(PhaseStartEvent evt)
    {
        if (evt.Phase != Phase.Judge)
            return;

        var game = evt.Game;
        var player = game.Players.FirstOrDefault(p => p.Seat == evt.PlayerSeat);
        if (player is null || !player.IsAlive)
            return;

        // Get all delayed tricks in the player's judgement zone
        var delayedTrickManager = new DelayedTrickManager();
        var delayedTricks = delayedTrickManager.GetDelayedTricks(player);

        if (delayedTricks.Count == 0)
        {
            // No delayed tricks to judge
            return;
        }

        // Create resolution context
        var context = new ResolutionContext(
            game,
            player,
            Action: null,
            Choice: null,
            _resolutionStack,
            _cardMoveService,
            _ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: null,
            IntermediateResults: new System.Collections.Generic.Dictionary<string, object>(),
            EventBus: _eventBus,
            LogCollector: null,
            SkillManager: null,
            EquipmentSkillRegistry: null,
            JudgementService: _judgementService ?? new BasicJudgementService(_eventBus)
        );

        // Push resolvers for each delayed trick (in reverse order so first one executes last)
        // This ensures judgements happen in the order cards were placed
        for (int i = delayedTricks.Count - 1; i >= 0; i--)
        {
            var delayedTrick = delayedTricks[i];
            context.Stack.Push(new DelayedTrickJudgementResolver(delayedTrick), context);
        }
    }
}
