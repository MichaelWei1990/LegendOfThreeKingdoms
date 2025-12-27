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
/// Ice Sword (寒冰剑) skill: Trigger skill that allows preventing Slash damage and discarding target's cards instead.
/// When you use a Slash that would deal damage to a target, you can prevent the damage
/// and instead discard 2 cards from the target (hand + equipment zones).
/// Attack Range: 2
/// </summary>
public sealed class IceSwordSkill : BaseSkill, IBeforeDamageSkill, IAttackDistanceModifyingSkill
{
    private Game? _game;
    private Player? _owner;
    private IEventBus? _eventBus;
    private ICardMoveService? _cardMoveService;
    private Func<ChoiceRequest, ChoiceResult>? _getPlayerChoice;

    /// <inheritdoc />
    public override string Id => "ice_sword";

    /// <inheritdoc />
    public override string Name => "寒冰";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Trigger;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.ModifiesRules;

    /// <summary>
    /// The attack range provided by Ice Sword.
    /// </summary>
    private const int AttackRange = 2;

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

        eventBus.Subscribe<BeforeDamageEvent>(OnBeforeDamage);
    }

    /// <inheritdoc />
    public override void Detach(Game game, Player owner, IEventBus eventBus)
    {
        if (eventBus is null)
            return;

        eventBus.Unsubscribe<BeforeDamageEvent>(OnBeforeDamage);

        _game = null;
        _owner = null;
        _eventBus = null;
        _cardMoveService = null;
        _getPlayerChoice = null;
    }

    /// <summary>
    /// Handles the BeforeDamageEvent.
    /// </summary>
    /// <inheritdoc />
    public void OnBeforeDamage(BeforeDamageEvent evt)
    {
        if (_game is null || _owner is null || _cardMoveService is null)
            return;

        // Only process if the owner is the damage source
        if (evt.Damage.SourceSeat != _owner.Seat)
            return;

        // Check if skill is active
        if (!IsActive(_game, _owner))
            return;

        // Check if damage has already been prevented (avoid duplicate prevention)
        if (evt.IsPrevented)
            return;

        // Check if damage is from Slash
        if (!IsSlashDamage(evt.Damage))
            return;

        // Check if target is still alive
        var target = _game.Players.FirstOrDefault(p => p.Seat == evt.Damage.TargetSeat);
        if (target is null || !target.IsAlive)
            return;

        // Get available cards for discarding from target (hand + equipment, excluding judgement zone)
        var availableCards = GetAvailableCardsForDiscard(target);
        if (availableCards.Count == 0)
        {
            // No cards to discard, cannot activate
            return;
        }

        // Ask player if they want to activate Ice Sword
        if (_getPlayerChoice is null)
        {
            // Auto-trigger: automatically activate if cards available
            ActivateIceSword(evt, target, availableCards);
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
                ActivateIceSword(evt, target, availableCards);
            }
        }
        catch
        {
            // If getting choice fails, silently ignore
        }
    }

    /// <summary>
    /// Checks if the damage is from a Slash card.
    /// </summary>
    private static bool IsSlashDamage(DamageDescriptor damage)
    {
        // Check by CausingCard
        if (damage.CausingCard is not null)
        {
            return damage.CausingCard.CardSubType == CardSubType.Slash;
        }

        // Check by Reason
        if (damage.Reason == "Slash")
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets available cards that can be discarded from target (hand + equipment zones).
    /// </summary>
    private static List<Card> GetAvailableCardsForDiscard(Player target)
    {
        var availableCards = new List<Card>();

        // Add hand cards
        if (target.HandZone.Cards is not null)
        {
            availableCards.AddRange(target.HandZone.Cards);
        }

        // Add equipment cards
        if (target.EquipmentZone.Cards is not null)
        {
            availableCards.AddRange(target.EquipmentZone.Cards);
        }

        // Exclude judgement zone cards (not discardable for Ice Sword)

        return availableCards;
    }

    /// <summary>
    /// Activates Ice Sword: prevents damage and discards target's cards.
    /// </summary>
    private void ActivateIceSword(
        BeforeDamageEvent evt,
        Player target,
        List<Card> availableCards)
    {
        if (_cardMoveService is null || _game is null)
            return;

        // Determine how many cards to discard (up to 2, or all available if less than 2)
        var cardsToDiscardCount = Math.Min(2, availableCards.Count);
        if (cardsToDiscardCount == 0)
            return;

        List<Card>? cardsToDiscard = null;

        // Ask player to select cards to discard (if getPlayerChoice is available)
        if (_getPlayerChoice is not null && cardsToDiscardCount > 0)
        {
            var selectRequest = new ChoiceRequest(
                RequestId: Guid.NewGuid().ToString(),
                PlayerSeat: _owner!.Seat,
                ChoiceType: ChoiceType.SelectCards,
                TargetConstraints: null,
                AllowedCards: availableCards,
                ResponseWindowId: null,
                CanPass: false // Must select cards
            );

            try
            {
                var selectResult = _getPlayerChoice(selectRequest);
                if (selectResult?.SelectedCardIds is not null && selectResult.SelectedCardIds.Count > 0)
                {
                    // Select up to cardsToDiscardCount cards
                    var selectedIds = selectResult.SelectedCardIds.Take(cardsToDiscardCount).ToList();
                    cardsToDiscard = availableCards
                        .Where(c => selectedIds.Contains(c.Id))
                        .Take(cardsToDiscardCount)
                        .ToList();
                }
            }
            catch
            {
                // If getting choice fails, fall back to auto-select
            }
        }

        // If no cards selected or getPlayerChoice not available, auto-select first N cards
        if (cardsToDiscard is null || cardsToDiscard.Count == 0)
        {
            cardsToDiscard = availableCards.Take(cardsToDiscardCount).ToList();
        }

        // Discard the selected cards
        try
        {
            // Separate cards by zone
            var handCards = cardsToDiscard.Where(c => target.HandZone.Cards.Contains(c)).ToList();
            var equipmentCards = cardsToDiscard.Where(c => target.EquipmentZone.Cards.Contains(c)).ToList();

            // Discard hand cards
            if (handCards.Count > 0)
            {
                _cardMoveService.DiscardFromHand(_game, target, handCards);
            }

            // Discard equipment cards
            foreach (var card in equipmentCards)
            {
                var moveDescriptor = new CardMoveDescriptor(
                    SourceZone: target.EquipmentZone,
                    TargetZone: _game.DiscardPile,
                    Cards: new[] { card },
                    Reason: CardMoveReason.Discard,
                    Ordering: CardMoveOrdering.ToTop,
                    Game: _game
                );
                _cardMoveService.MoveSingle(moveDescriptor);
            }
        }
        catch
        {
            // If discarding fails, cannot prevent damage
            return;
        }

        // Prevent the damage
        evt.IsPrevented = true;
    }

    /// <inheritdoc />
    public int? ModifyAttackDistance(int current, Game game, Player from, Player to)
    {
        // Ice Sword provides attack range of 2
        // If current distance is less than 2, set it to 2
        if (!IsActive(game, from))
            return null;

        // Set attack distance to 2 (weapon's fixed range)
        return AttackRange;
    }
}

/// <summary>
/// Factory for creating IceSwordSkill instances.
/// </summary>
public sealed class IceSwordSkillFactory : IEquipmentSkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new IceSwordSkill();
    }
}

