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
/// Kirin Bow (麒麟弓) skill: Trigger skill that allows discarding target's horse equipment after dealing Slash damage.
/// When you use a Slash to deal damage to a target, after the damage is fully resolved,
/// you can discard one horse equipment card (OffensiveHorse or DefensiveHorse) from the target's equipment zone.
/// Attack Range: 5
/// </summary>
public sealed class KirinBowSkill : BaseSkill, IAttackDistanceModifyingSkill, IAfterDamageSkill
{
    private Game? _game;
    private Player? _owner;
    private IEventBus? _eventBus;
    private ICardMoveService? _cardMoveService;
    private Func<ChoiceRequest, ChoiceResult>? _getPlayerChoice;

    /// <inheritdoc />
    public override string Id => "kirin_bow";

    /// <inheritdoc />
    public override string Name => "麒麟";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Trigger;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.ModifiesRules;

    /// <summary>
    /// The attack range provided by Kirin Bow.
    /// </summary>
    private const int AttackRange = 5;

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

        eventBus.Subscribe<AfterDamageEvent>(OnAfterDamage);
    }

    /// <inheritdoc />
    public override void Detach(Game game, Player owner, IEventBus eventBus)
    {
        if (eventBus is null)
            return;

        eventBus.Unsubscribe<AfterDamageEvent>(OnAfterDamage);

        _game = null;
        _owner = null;
        _eventBus = null;
        _cardMoveService = null;
        _getPlayerChoice = null;
    }

    /// <inheritdoc />
    public int? ModifyAttackDistance(int current, Game game, Player from, Player to)
    {
        // Kirin Bow provides attack range of 5
        // If current distance is less than 5, set it to 5
        if (!IsActive(game, from))
            return null;

        // Set attack distance to 5 (weapon's fixed range)
        return AttackRange;
    }

    /// <inheritdoc />
    public void OnAfterDamage(AfterDamageEvent evt)
    {
        if (_game is null || _owner is null || _cardMoveService is null)
            return;

        // Only process if the owner is the damage source
        if (evt.Damage.SourceSeat != _owner.Seat)
            return;

        // Check if skill is active
        if (!IsActive(_game, _owner))
            return;

        // Check if damage is from Slash
        if (!IsSlashDamage(evt.Damage))
            return;

        // Check if target is still alive
        var target = _game.Players.FirstOrDefault(p => p.Seat == evt.Damage.TargetSeat);
        if (target is null || !target.IsAlive)
            return;

        // Get horse equipment cards from target's equipment zone
        var horseCards = GetHorseEquipmentCards(target);
        if (horseCards.Count == 0)
        {
            // No horse equipment, cannot activate
            return;
        }

        // Ask player if they want to activate Kirin Bow
        if (_getPlayerChoice is null)
        {
            // Auto-trigger: automatically activate if horse cards available
            ActivateKirinBow(target, horseCards);
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
                ActivateKirinBow(target, horseCards);
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
    /// Gets horse equipment cards (OffensiveHorse or DefensiveHorse) from target's equipment zone.
    /// </summary>
    private static List<Card> GetHorseEquipmentCards(Player target)
    {
        var horseCards = new List<Card>();

        if (target.EquipmentZone?.Cards is null)
            return horseCards;

        foreach (var card in target.EquipmentZone.Cards)
        {
            if (card.CardSubType == CardSubType.OffensiveHorse || 
                card.CardSubType == CardSubType.DefensiveHorse)
            {
                horseCards.Add(card);
            }
        }

        return horseCards;
    }

    /// <summary>
    /// Activates Kirin Bow: discards one horse equipment card from target.
    /// </summary>
    private void ActivateKirinBow(Player target, List<Card> horseCards)
    {
        if (_cardMoveService is null || _game is null)
            return;

        Card? cardToDiscard = null;

        // If only one horse card, use it directly
        if (horseCards.Count == 1)
        {
            cardToDiscard = horseCards[0];
        }
        else
        {
            // Multiple horse cards - ask player to select one
            if (_getPlayerChoice is not null)
            {
                var selectRequest = new ChoiceRequest(
                    RequestId: Guid.NewGuid().ToString(),
                    PlayerSeat: _owner!.Seat,
                    ChoiceType: ChoiceType.SelectCards,
                    TargetConstraints: null,
                    AllowedCards: horseCards,
                    ResponseWindowId: null,
                    CanPass: false // Must select one card
                );

                try
                {
                    var selectResult = _getPlayerChoice(selectRequest);
                    if (selectResult?.SelectedCardIds is not null && selectResult.SelectedCardIds.Count > 0)
                    {
                        cardToDiscard = horseCards.FirstOrDefault(c => selectResult.SelectedCardIds.Contains(c.Id));
                    }
                }
                catch
                {
                    // If getting choice fails, fall back to auto-select
                }
            }

            // If no card selected or getPlayerChoice not available, auto-select first card
            if (cardToDiscard is null)
            {
                cardToDiscard = horseCards.FirstOrDefault();
            }
        }

        // Discard the selected horse card
        if (cardToDiscard is null)
            return;

        try
        {
            var moveDescriptor = new CardMoveDescriptor(
                SourceZone: target.EquipmentZone,
                TargetZone: _game.DiscardPile,
                Cards: new[] { cardToDiscard },
                Reason: CardMoveReason.Discard,
                Ordering: CardMoveOrdering.ToTop,
                Game: _game
            );
            _cardMoveService.MoveSingle(moveDescriptor);
        }
        catch
        {
            // If discarding fails, silently ignore
        }
    }
}

/// <summary>
/// Factory for creating KirinBowSkill instances.
/// </summary>
public sealed class KirinBowSkillFactory : IEquipmentSkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new KirinBowSkill();
    }
}

