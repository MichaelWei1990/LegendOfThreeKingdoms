using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Skills.Equipment;

/// <summary>
/// Qinglong Yanyue Dao (青龙偃月刀) skill: Trigger skill that allows using another Slash after a Slash is negated by Jink.
/// When you use a Slash and the target negates it with Jink (闪), you can use another Slash against the same target (ignoring distance).
/// Attack Range: 3
/// </summary>
public sealed class QinglongYanyueDaoSkill : BaseSkill, ISlashNegatedByJinkSkill, IAttackDistanceModifyingSkill
{
    private Game? _game;
    private Player? _owner;
    private IEventBus? _eventBus;
    private ICardMoveService? _cardMoveService;
    private IRuleService? _ruleService;
    private Func<ChoiceRequest, ChoiceResult>? _getPlayerChoice;
    private SkillManager? _skillManager;

    /// <inheritdoc />
    public override string Id => "qinglong_yanyue_dao";

    /// <inheritdoc />
    public override string Name => "青龙偃月刀";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Trigger;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.ModifiesRules;

    /// <summary>
    /// The attack range provided by Qinglong Yanyue Dao.
    /// </summary>
    private const int AttackRange = 3;

    /// <summary>
    /// Sets the card move service for card operations.
    /// This is called by the skill manager during attachment.
    /// </summary>
    public void SetCardMoveService(ICardMoveService cardMoveService)
    {
        _cardMoveService = cardMoveService ?? throw new ArgumentNullException(nameof(cardMoveService));
    }

    /// <summary>
    /// Sets the rule service for validation.
    /// This is called by the skill manager during attachment.
    /// </summary>
    public void SetRuleService(IRuleService ruleService)
    {
        _ruleService = ruleService ?? throw new ArgumentNullException(nameof(ruleService));
    }

    /// <summary>
    /// Sets the function to get player choice.
    /// This is called by the skill manager during attachment.
    /// </summary>
    public void SetGetPlayerChoice(Func<ChoiceRequest, ChoiceResult> getPlayerChoice)
    {
        _getPlayerChoice = getPlayerChoice;
    }

    /// <summary>
    /// Sets the skill manager for accessing other skills.
    /// This is called by the skill manager during attachment.
    /// </summary>
    public void SetSkillManager(SkillManager skillManager)
    {
        _skillManager = skillManager;
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

        eventBus.Subscribe<SlashNegatedByJinkEvent>(OnSlashNegatedByJink);
    }

    /// <inheritdoc />
    public override void Detach(Game game, Player owner, IEventBus eventBus)
    {
        if (eventBus is null)
            return;

        eventBus.Unsubscribe<SlashNegatedByJinkEvent>(OnSlashNegatedByJink);

        _game = null;
        _owner = null;
        _eventBus = null;
        _cardMoveService = null;
        _ruleService = null;
        _getPlayerChoice = null;
        _skillManager = null;
    }

    /// <summary>
    /// Handles the SlashNegatedByJinkEvent.
    /// </summary>
    /// <inheritdoc />
    public void OnSlashNegatedByJink(SlashNegatedByJinkEvent evt)
    {
        if (_game is null || _owner is null || _cardMoveService is null || _ruleService is null)
            return;

        // Only process if the owner is the source (attacker)
        if (evt.Source.Seat != _owner.Seat)
            return;

        // Check if skill is active
        if (!IsActive(_game, _owner))
            return;

        // Check if target is still alive
        if (!evt.Target.IsAlive)
            return;

        // Get available Slash cards from hand
        var availableSlashCards = GetAvailableSlashCards(_owner);
        if (availableSlashCards.Count == 0)
        {
            // No Slash cards available, cannot activate
            return;
        }

        // Ask player if they want to use Qinglong Yanyue Dao to chase
        if (_getPlayerChoice is null)
        {
            // Auto-trigger: automatically activate if Slash cards available
            TriggerChaseSlash(_game, _owner, evt.Target, availableSlashCards);
            return;
        }

        // Ask player to confirm
        var confirmRequest = new ChoiceRequest(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: _owner.Seat,
            ChoiceType: ChoiceType.Confirm,
            TargetConstraints: null,
            AllowedCards: null,
            ResponseWindowId: null,
            CanPass: true
        );

        try
        {
            var confirmResult = _getPlayerChoice(confirmRequest);
            if (confirmResult?.Confirmed == true)
            {
                TriggerChaseSlash(_game, _owner, evt.Target, availableSlashCards);
            }
        }
        catch
        {
            // If getting choice fails, silently ignore
        }
    }

    /// <summary>
    /// Gets available Slash cards from the player's hand.
    /// </summary>
    private static List<Card> GetAvailableSlashCards(Player player)
    {
        var availableCards = new List<Card>();

        // Add Slash cards from hand
        if (player.HandZone.Cards is not null)
        {
            availableCards.AddRange(player.HandZone.Cards.Where(c => c.CardSubType == CardSubType.Slash));
        }

        return availableCards;
    }

    /// <summary>
    /// Triggers the chase Slash: asks player to select a Slash card and uses it against the target (ignoring distance).
    /// </summary>
    private void TriggerChaseSlash(
        Game game,
        Player owner,
        Player target,
        List<Card> availableSlashCards)
    {
        if (_cardMoveService is null || _ruleService is null || _getPlayerChoice is null)
            return;

        Card? selectedSlash = null;

        // Ask player to select a Slash card
        var selectRequest = new ChoiceRequest(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: owner.Seat,
            ChoiceType: ChoiceType.SelectCards,
            TargetConstraints: null,
            AllowedCards: availableSlashCards,
            ResponseWindowId: null,
            CanPass: false // Must select a Slash card
        );

        try
        {
            var selectResult = _getPlayerChoice(selectRequest);
            if (selectResult?.SelectedCardIds is not null && selectResult.SelectedCardIds.Count > 0)
            {
                selectedSlash = availableSlashCards.FirstOrDefault(c => c.Id == selectResult.SelectedCardIds[0]);
            }
        }
        catch
        {
            // If getting choice fails, fall back to auto-select
        }

        // If no card selected or getPlayerChoice not available, auto-select first Slash card
        if (selectedSlash is null)
        {
            selectedSlash = availableSlashCards.FirstOrDefault();
            if (selectedSlash is null)
                return; // No Slash card available
        }

        // Create Action and Choice for using the Slash
        var action = new ActionDescriptor(
            ActionId: Guid.NewGuid().ToString(),
            DisplayKey: "UseSlash",
            RequiresTargets: true,
            TargetConstraints: new TargetConstraints(
                MinTargets: 1,
                MaxTargets: 1,
                FilterType: TargetFilterType.Enemies
            ),
            CardCandidates: new[] { selectedSlash }
        );

        var choice = new ChoiceResult(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: owner.Seat,
            SelectedTargetSeats: new[] { target.Seat },
            SelectedCardIds: new[] { selectedSlash.Id },
            SelectedOptionId: null,
            Confirmed: null
        );

        // Mark this as a chase Slash in the owner's flags to enable distance bypass
        owner.Flags["QinglongYanyueDao_ChaseSlash"] = true;

        try
        {
            // Create resolution stack and context
            var stack = new BasicResolutionStack();
            var intermediateResults = new Dictionary<string, object>
            {
                { "IgnoreDistanceCheck", true } // Mark to ignore distance check for this chase Slash
            };

            var resolutionContext = new ResolutionContext(
                game,
                owner,
                action,
                choice,
                stack,
                _cardMoveService,
                _ruleService,
                PendingDamage: null,
                LogSink: null,
                GetPlayerChoice: _getPlayerChoice,
                IntermediateResults: intermediateResults,
                EventBus: _eventBus,
                LogCollector: null,
                SkillManager: _skillManager,
                EquipmentSkillRegistry: null,
                JudgementService: null
            );

            // Push SlashResolver to execute the chase Slash
            stack.Push(new SlashResolver(), resolutionContext);

            // Execute all resolvers in the stack
            while (!stack.IsEmpty)
            {
                var result = stack.Pop();
                // Ignore failures for now (resolver should handle errors internally)
            }
        }
        finally
        {
            // Clear the chase flag after resolution
            owner.Flags.Remove("QinglongYanyueDao_ChaseSlash");
        }
    }

    /// <inheritdoc />
    public int? ModifyAttackDistance(int current, Game game, Player from, Player to)
    {
        if (!IsActive(game, from))
            return null;

        // Check if this is a chase Slash (ignore distance check)
        // We check if there's a flag in the game state or use a different approach
        // For now, we'll use a thread-local or game-state flag to track chase Slash
        // Since we can't access IntermediateResults here, we'll use a different mechanism:
        // Store a flag in the owner's Flags dictionary when triggering chase
        if (_owner is not null && _owner.Flags.TryGetValue("QinglongYanyueDao_ChaseSlash", out var chaseFlag) && chaseFlag is bool isChase && isChase)
        {
            // For chase Slash, return a very large attack distance to effectively ignore distance check
            return int.MaxValue;
        }

        // Qinglong Yanyue Dao provides attack range of 3
        // If current distance is less than 3, set it to 3
        return AttackRange;
    }
}

/// <summary>
/// Factory for creating QinglongYanyueDaoSkill instances.
/// </summary>
public sealed class QinglongYanyueDaoSkillFactory : IEquipmentSkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new QinglongYanyueDaoSkill();
    }
}

