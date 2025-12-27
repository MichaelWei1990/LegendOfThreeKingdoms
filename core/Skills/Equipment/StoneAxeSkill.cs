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
/// Stone Axe (贯石斧) skill: Trigger skill that allows forcing damage after a Slash is dodged.
/// When you use a Slash and the target dodges it with Dodge (闪), you can discard 2 cards
/// to force the Slash to deal 1 damage anyway.
/// </summary>
public sealed class StoneAxeSkill : BaseSkill
{
    private Game? _game;
    private Player? _owner;
    private IEventBus? _eventBus;
    private ICardMoveService? _cardMoveService;
    private Func<ChoiceRequest, ChoiceResult>? _getPlayerChoice;

    /// <inheritdoc />
    public override string Id => "stone_axe";

    /// <inheritdoc />
    public override string Name => "贯石";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Trigger;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.None;

    /// <summary>
    /// Sets the card move service for discarding cards.
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

        eventBus.Subscribe<AfterSlashDodgedEvent>(OnAfterSlashDodged);
    }

    /// <inheritdoc />
    public override void Detach(Game game, Player owner, IEventBus eventBus)
    {
        if (eventBus is null)
            return;

        eventBus.Unsubscribe<AfterSlashDodgedEvent>(OnAfterSlashDodged);

        _game = null;
        _owner = null;
        _eventBus = null;
        _cardMoveService = null;
        _getPlayerChoice = null;
    }

    /// <summary>
    /// Handles the AfterSlashDodgedEvent.
    /// </summary>
    private void OnAfterSlashDodged(AfterSlashDodgedEvent evt)
    {
        if (_game is null || _owner is null || _cardMoveService is null)
            return;

        // Only process if the owner is the attacker
        if (evt.AttackerSeat != _owner.Seat)
            return;

        // Check if skill is active
        if (!IsActive(_game, _owner))
            return;

        // Check if target is still alive
        var target = _game.Players.FirstOrDefault(p => p.Seat == evt.TargetSeat);
        if (target is null || !target.IsAlive)
            return;

        // Get available cards for discarding (hand + equipment, excluding judgement zone)
        var availableCards = GetAvailableCardsForDiscard(_owner);
        if (availableCards.Count < 2)
        {
            // Not enough cards to discard, cannot activate
            return;
        }

        // Ask player if they want to activate Stone Axe
        if (_getPlayerChoice is null)
        {
            // Auto-trigger: automatically activate if enough cards
            ActivateStoneAxe(_game, _owner, target, evt.SlashCard, evt.OriginalDamage, availableCards);
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
                ActivateStoneAxe(_game, _owner, target, evt.SlashCard, evt.OriginalDamage, availableCards);
            }
        }
        catch
        {
            // If getting choice fails, silently ignore
        }
    }

    /// <summary>
    /// Gets available cards that can be discarded (hand + equipment zones).
    /// </summary>
    private static List<Card> GetAvailableCardsForDiscard(Player player)
    {
        var availableCards = new List<Card>();

        // Add hand cards
        if (player.HandZone.Cards is not null)
        {
            availableCards.AddRange(player.HandZone.Cards);
        }

        // Add equipment cards
        if (player.EquipmentZone.Cards is not null)
        {
            availableCards.AddRange(player.EquipmentZone.Cards);
        }

        // Exclude judgement zone cards (not discardable for Stone Axe)

        return availableCards;
    }

    /// <summary>
    /// Activates Stone Axe: discards 2 cards and forces 1 damage.
    /// </summary>
    private void ActivateStoneAxe(
        Game game,
        Player owner,
        Player target,
        Card slashCard,
        DamageDescriptor originalDamage,
        List<Card> availableCards)
    {
        if (_cardMoveService is null)
            return;

        List<Card>? cardsToDiscard = null;

        // Ask player to select 2 cards to discard (if getPlayerChoice is available)
        if (_getPlayerChoice is not null)
        {
            var selectRequest = new ChoiceRequest(
                RequestId: Guid.NewGuid().ToString(),
                PlayerSeat: owner.Seat,
                ChoiceType: ChoiceType.SelectCards,
                TargetConstraints: null,
                AllowedCards: availableCards,
                ResponseWindowId: null,
                CanPass: false // Must select 2 cards
            );

            try
            {
                var selectResult = _getPlayerChoice(selectRequest);
                if (selectResult?.SelectedCardIds is not null && selectResult.SelectedCardIds.Count == 2)
                {
                    cardsToDiscard = availableCards
                        .Where(c => selectResult.SelectedCardIds.Contains(c.Id))
                        .ToList();
                }
            }
            catch
            {
                // If getting choice fails, fall back to auto-select
            }
        }

        // If no cards selected or getPlayerChoice not available, auto-select first 2 cards
        if (cardsToDiscard is null || cardsToDiscard.Count != 2)
        {
            cardsToDiscard = availableCards.Take(2).ToList();
            if (cardsToDiscard.Count != 2)
                return; // Not enough cards
        }

        // Discard the selected cards
        try
        {
            // Separate cards by zone
            var handCards = cardsToDiscard.Where(c => owner.HandZone.Cards.Contains(c)).ToList();
            var equipmentCards = cardsToDiscard.Where(c => owner.EquipmentZone.Cards.Contains(c)).ToList();

            // Discard hand cards
            if (handCards.Count > 0)
            {
                _cardMoveService.DiscardFromHand(game, owner, handCards);
            }

            // Discard equipment cards
            foreach (var card in equipmentCards)
            {
                var moveDescriptor = new CardMoveDescriptor(
                    SourceZone: owner.EquipmentZone,
                    TargetZone: game.DiscardPile,
                    Cards: new[] { card },
                    Reason: CardMoveReason.Discard,
                    Ordering: CardMoveOrdering.ToTop,
                    Game: game
                );
                _cardMoveService.MoveSingle(moveDescriptor);
            }
        }
        catch
        {
            // If discarding fails, cannot proceed
            return;
        }

        // Force damage: create damage descriptor and push to resolution stack
        var forcedDamage = new DamageDescriptor(
            SourceSeat: owner.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "StoneAxe",
            CausingCard: slashCard, // The original Slash card
            IsPreventable: false, // Force damage, cannot be prevented
            TransferredToSeat: null,
            TriggersDying: true
        );

        // Create resolution stack and context for damage
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
            PendingDamage: forcedDamage,
            LogSink: null,
            GetPlayerChoice: _getPlayerChoice,
            IntermediateResults: null,
            EventBus: _eventBus,
            LogCollector: null,
            SkillManager: null,
            EquipmentSkillRegistry: null,
            JudgementService: null
        );

        // Push DamageResolver to apply the forced damage
        stack.Push(new DamageResolver(), damageContext);

        // Execute all resolvers in the stack
        while (!stack.IsEmpty)
        {
            var result = stack.Pop();
            // Ignore failures for now (resolver should handle errors internally)
        }
    }
}

/// <summary>
/// Factory for creating StoneAxeSkill instances.
/// </summary>
public sealed class StoneAxeSkillFactory : IEquipmentSkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new StoneAxeSkill();
    }
}

