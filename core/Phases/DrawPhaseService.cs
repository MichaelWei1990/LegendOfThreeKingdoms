using System;
using System.Linq;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Phases;

/// <summary>
/// Service that handles draw phase logic by listening to PhaseStartEvent.
/// When Draw Phase starts, it automatically executes the draw phase resolver to draw cards for the current player.
/// </summary>
public sealed class DrawPhaseService
{
    private readonly ICardMoveService _cardMoveService;
    private readonly IRuleService _ruleService;
    private readonly IEventBus _eventBus;
    private readonly IResolutionStack _resolutionStack;
    private readonly SkillManager? _skillManager;
    private readonly Func<Rules.ChoiceRequest, Rules.ChoiceResult>? _getPlayerChoice;

    /// <summary>
    /// Creates a new DrawPhaseService.
    /// </summary>
    /// <param name="cardMoveService">The card move service for drawing cards.</param>
    /// <param name="ruleService">The rule service for applying rule modifiers.</param>
    /// <param name="eventBus">The event bus to subscribe to phase start events.</param>
    /// <param name="resolutionStack">The resolution stack for executing the draw phase resolver.</param>
    /// <param name="skillManager">Optional skill manager for checking draw phase replacement skills.</param>
    /// <param name="getPlayerChoice">Optional function to get player choices for skill activations.</param>
    /// <exception cref="ArgumentNullException">Thrown if any required parameter is null.</exception>
    public DrawPhaseService(
        ICardMoveService cardMoveService,
        IRuleService ruleService,
        IEventBus eventBus,
        IResolutionStack resolutionStack,
        SkillManager? skillManager = null,
        Func<Rules.ChoiceRequest, Rules.ChoiceResult>? getPlayerChoice = null)
    {
        _cardMoveService = cardMoveService ?? throw new ArgumentNullException(nameof(cardMoveService));
        _ruleService = ruleService ?? throw new ArgumentNullException(nameof(ruleService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _resolutionStack = resolutionStack ?? throw new ArgumentNullException(nameof(resolutionStack));
        _skillManager = skillManager;
        _getPlayerChoice = getPlayerChoice;

        _eventBus.Subscribe<PhaseStartEvent>(OnPhaseStart);
    }

    private void OnPhaseStart(PhaseStartEvent evt)
    {
        if (evt.Phase != Phase.Draw)
            return;

        var game = evt.Game;
        var player = game.Players.FirstOrDefault(p => p.Seat == evt.PlayerSeat);
        if (player is null || !player.IsAlive)
            return;

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
            GetPlayerChoice: _getPlayerChoice,
            IntermediateResults: null,
            EventBus: _eventBus,
            LogCollector: null,
            SkillManager: _skillManager,
            EquipmentSkillRegistry: null,
            JudgementService: null
        );

        _resolutionStack.ExecuteDrawPhase(context);
    }
}
