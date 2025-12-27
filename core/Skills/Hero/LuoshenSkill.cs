using System;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Judgement;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Luoshen (洛神) skill: During your prepare phase, you can perform a judgement.
/// If the result is black, you obtain the judgement card and can repeat this process.
/// If the result is red, the process stops.
/// </summary>
public sealed class LuoshenSkill : BaseSkill
{
    private Game? _game;
    private Player? _owner;
    private IEventBus? _eventBus;
    private ICardMoveService? _cardMoveService;
    private IJudgementService? _judgementService;
    private IResolutionStack? _resolutionStack;
    private Func<Rules.ChoiceRequest, Rules.ChoiceResult>? _getPlayerChoice;

    /// <inheritdoc />
    public override string Id => "luoshen";

    /// <inheritdoc />
    public override string Name => "洛神";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Trigger;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.None;

    /// <inheritdoc />
    public override void Attach(Game game, Player owner, IEventBus eventBus)
    {
        if (game is null)
            throw new ArgumentNullException(nameof(game));
        if (owner is null)
            throw new ArgumentNullException(nameof(owner));
        if (eventBus is null)
            throw new ArgumentNullException(nameof(eventBus));

        _game = game;
        _owner = owner;
        _eventBus = eventBus;

        eventBus.Subscribe<PhaseStartEvent>(OnPhaseStart);
    }

    /// <inheritdoc />
    public override void Detach(Game game, Player owner, IEventBus eventBus)
    {
        if (eventBus is not null)
        {
            eventBus.Unsubscribe<PhaseStartEvent>(OnPhaseStart);
        }

        _game = null;
        _owner = null;
        _eventBus = null;
        _cardMoveService = null;
        _judgementService = null;
        _resolutionStack = null;
        _getPlayerChoice = null;
    }

    /// <summary>
    /// Sets the card move service for this skill.
    /// </summary>
    public void SetCardMoveService(ICardMoveService cardMoveService)
    {
        _cardMoveService = cardMoveService;
    }

    /// <summary>
    /// Sets the judgement service for this skill.
    /// </summary>
    public void SetJudgementService(IJudgementService judgementService)
    {
        _judgementService = judgementService;
    }

    /// <summary>
    /// Sets the resolution stack for this skill.
    /// </summary>
    public void SetResolutionStack(IResolutionStack resolutionStack)
    {
        _resolutionStack = resolutionStack;
    }

    /// <summary>
    /// Sets the player choice function for this skill.
    /// </summary>
    public void SetGetPlayerChoice(Func<Rules.ChoiceRequest, Rules.ChoiceResult> getPlayerChoice)
    {
        _getPlayerChoice = getPlayerChoice;
    }

    private void OnPhaseStart(PhaseStartEvent evt)
    {
        // Only process for the owner's prepare phase (Phase.Start)
        if (_game is null || _owner is null || evt.PlayerSeat != _owner.Seat)
            return;

        if (evt.Phase != Phase.Start)
            return;

        // Check if skill is active
        if (!IsActive(_game, _owner))
            return;

        // Check if required services are available
        if (_cardMoveService is null || _judgementService is null || _resolutionStack is null || _getPlayerChoice is null)
            return;

        // Ask player if they want to activate Luoshen
        var confirmRequest = new Rules.ChoiceRequest(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: _owner.Seat,
            ChoiceType: Rules.ChoiceType.Confirm,
            TargetConstraints: null,
            AllowedCards: null,
            ResponseWindowId: null,
            CanPass: true // Player can choose not to activate
        );

        try
        {
            var confirmResult = _getPlayerChoice(confirmRequest);
            if (confirmResult?.Confirmed != true)
            {
                return; // Player chose not to activate
            }
        }
        catch
        {
            // If choice fails, skip activation
            return;
        }

        // Push LuoshenLoopResolver to handle the judgement loop
        var context = new ResolutionContext(
            _game,
            _owner,
            Action: null,
            Choice: null,
            _resolutionStack,
            _cardMoveService,
            RuleService: null,
            PendingDamage: null,
            LogSink: null,
            _getPlayerChoice,
            IntermediateResults: new System.Collections.Generic.Dictionary<string, object>(),
            EventBus: _eventBus,
            LogCollector: null,
            SkillManager: null,
            EquipmentSkillRegistry: null,
            JudgementService: _judgementService);

        _resolutionStack.Push(new LuoshenLoopResolver(), context);
    }
}

/// <summary>
/// Factory for creating LuoshenSkill instances.
/// </summary>
public sealed class LuoshenSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new LuoshenSkill();
    }
}

