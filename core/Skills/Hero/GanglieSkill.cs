using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Judgement;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Ganglie (刚烈) skill: Trigger skill that performs judgement after taking damage.
/// If judgement is not Heart, the damage source must choose: discard 2 hand cards or take 1 damage.
/// </summary>
public sealed class GanglieSkill : BaseSkill, IDamageResolvedSkill
{
    private Game? _game;
    private Player? _owner;
    private IEventBus? _eventBus;
    private IJudgementService? _judgementService;
    private ICardMoveService? _cardMoveService;
    private Func<ChoiceRequest, ChoiceResult>? _getPlayerChoice;

    /// <inheritdoc />
    public override string Id => "ganglie";

    /// <inheritdoc />
    public override string Name => "刚烈";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Trigger;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.None;

    /// <summary>
    /// Sets the judgement service for performing judgements.
    /// This is called by the skill manager during attachment.
    /// </summary>
    public void SetJudgementService(IJudgementService judgementService)
    {
        _judgementService = judgementService ?? throw new ArgumentNullException(nameof(judgementService));
    }

    /// <summary>
    /// Sets the card move service for moving cards.
    /// This is called by the skill manager during attachment.
    /// </summary>
    public void SetCardMoveService(ICardMoveService cardMoveService)
    {
        _cardMoveService = cardMoveService ?? throw new ArgumentNullException(nameof(cardMoveService));
    }

    /// <summary>
    /// Sets the function to get player choice.
    /// This is called by the skill manager during attachment.
    /// </summary>
    public void SetGetPlayerChoice(Func<ChoiceRequest, ChoiceResult> getPlayerChoice)
    {
        _getPlayerChoice = getPlayerChoice;
    }

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

        eventBus.Subscribe<DamageResolvedEvent>(OnDamageResolved);
    }

    /// <inheritdoc />
    public override void Detach(Game game, Player owner, IEventBus eventBus)
    {
        if (eventBus is null)
            return;

        eventBus.Unsubscribe<DamageResolvedEvent>(OnDamageResolved);

        _game = null;
        _owner = null;
        _eventBus = null;
        _judgementService = null;
        _cardMoveService = null;
        _getPlayerChoice = null;
    }

    /// <inheritdoc />
    public void OnDamageResolved(DamageResolvedEvent evt)
    {
        if (_game is null || _owner is null || _judgementService is null || _cardMoveService is null)
            return;

        // Only process for the owner (target of damage)
        if (evt.Damage.TargetSeat != _owner.Seat)
            return;

        // Check if skill is active
        if (!IsActive(_game, _owner))
            return;

        // Check if damage has a source (not null or -1)
        if (evt.Damage.SourceSeat < 0)
            return;

        // Find damage source player
        var damageSource = _game.Players.FirstOrDefault(p => p.Seat == evt.Damage.SourceSeat);
        if (damageSource is null || !damageSource.IsAlive)
            return;

        // Core v1 convention: automatically trigger (no player choice)
        // Perform judgement
        var judgementRequest = new JudgementRequest(
            JudgementId: Guid.NewGuid(),
            JudgeOwnerSeat: _owner.Seat,
            Reason: JudgementReason.Skill,
            Source: new GanglieEffectSource(),
            Rule: new NegatedJudgementRule(new SuitJudgementRule(Suit.Heart)), // Not Heart = success
            Tags: null,
            AllowModify: false // Ganglie does not allow modification
        );

        try
        {
            var judgementResult = _judgementService.ExecuteJudgement(
                _game,
                _owner,
                judgementRequest,
                _cardMoveService);

            // Complete the judgement (move card from JudgementZone to discard pile)
            _judgementService.CompleteJudgement(_game, _owner, judgementResult.FinalCard, _cardMoveService);

            // If judgement is successful (not Heart), trigger choice for damage source
            if (judgementResult.IsSuccess)
            {
                TriggerGanglieChoice(_game, _owner, damageSource, evt.Damage);
            }
        }
        catch (Exception)
        {
            // Judgement failed (e.g., draw pile empty), silently ignore
            // This matches the behavior of other trigger skills
        }
    }

    /// <summary>
    /// Triggers the choice for damage source: discard 2 hand cards or take 1 damage.
    /// Uses resolution stack to handle the choice and execution.
    /// </summary>
    private void TriggerGanglieChoice(Game game, Player owner, Player damageSource, DamageDescriptor originalDamage)
    {
        if (_cardMoveService is null)
            return;

        // Check if damage source has less than 2 hand cards
        // If so, automatically choose "take damage" option
        if (damageSource.HandZone.Cards.Count < 2)
        {
            // Automatically execute damage option
            ExecuteGanglieDamageDirect(game, owner, damageSource);
            return;
        }

        // Create resolution stack to handle the choice
        var stack = new BasicResolutionStack();
        var ruleService = new RuleService();

        // Create choice request for damage source
        // Use SelectCards to let player choose 2 cards to discard
        // If they pass or don't select 2 cards, it means they choose damage option
        var choiceRequest = new ChoiceRequest(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: damageSource.Seat,
            ChoiceType: ChoiceType.SelectCards,
            TargetConstraints: null,
            AllowedCards: damageSource.HandZone.Cards.ToList(),
            ResponseWindowId: null,
            CanPass: true // Can pass to choose damage option
        );

        // Get player choice if available
        ChoiceResult? choiceResult = null;
        if (_getPlayerChoice is not null)
        {
            try
            {
                choiceResult = _getPlayerChoice(choiceRequest);
            }
            catch
            {
                // If getting choice fails, default to damage
                choiceResult = null;
            }
        }

        // Create context for GanglieChoiceResolver
        var choiceContext = new ResolutionContext(
            game,
            owner,
            null,
            choiceResult,
            stack,
            _cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: _getPlayerChoice,
            IntermediateResults: null,
            EventBus: _eventBus,
            LogCollector: null,
            SkillManager: null,
            EquipmentSkillRegistry: null,
            JudgementService: _judgementService
        );

        // Push GanglieChoiceResolver to handle the choice
        stack.Push(new GanglieChoiceResolver(owner.Seat, damageSource.Seat), choiceContext);

        // Execute all resolvers in the stack
        while (!stack.IsEmpty)
        {
            var result = stack.Pop();
            // Ignore failures for now (resolver should handle errors internally)
        }
    }

    /// <summary>
    /// Executes the "take 1 damage" option directly using resolution stack.
    /// </summary>
    private void ExecuteGanglieDamageDirect(Game game, Player owner, Player damageSource)
    {
        if (_cardMoveService is null)
            return;

        // Create damage descriptor for 1 damage from owner to damageSource
        var damage = new DamageDescriptor(
            SourceSeat: owner.Seat,
            TargetSeat: damageSource.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Ganglie",
            CausingCard: null, // Skill damage, no causing card
            IsPreventable: true,
            TransferredToSeat: null,
            TriggersDying: true
        );

        // Create resolution stack and context
        var stack = new BasicResolutionStack();
        var ruleService = new RuleService();

        var damageContext = new ResolutionContext(
            game,
            owner,
            null,
            null,
            stack,
            _cardMoveService,
            ruleService,
            PendingDamage: damage,
            LogSink: null,
            GetPlayerChoice: _getPlayerChoice,
            IntermediateResults: null,
            EventBus: _eventBus,
            LogCollector: null,
            SkillManager: null,
            EquipmentSkillRegistry: null,
            JudgementService: _judgementService
        );

        // Push and execute DamageResolver
        stack.Push(new DamageResolver(), damageContext);

        // Execute all resolvers in the stack
        while (!stack.IsEmpty)
        {
            var result = stack.Pop();
            // Ignore failures for now (damage resolution should handle errors internally)
        }
    }
}

/// <summary>
/// Effect source for Ganglie skill judgements.
/// </summary>
internal sealed class GanglieEffectSource : IEffectSource
{
    public string SourceId => "ganglie";
    public string SourceType => "Skill";
    public string? DisplayName => "刚烈";
}

/// <summary>
/// Factory for creating GanglieSkill instances.
/// </summary>
public sealed class GanglieSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new GanglieSkill();
    }
}
