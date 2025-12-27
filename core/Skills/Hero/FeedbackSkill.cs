using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Feedback (反馈) skill: Trigger skill that obtains a card from damage source after taking damage.
/// Standard version: Can trigger at most once per damage event.
/// </summary>
public sealed class FeedbackSkill : BaseSkill, IAfterDamageSkill
{
    private Game? _game;
    private Player? _owner;
    private IEventBus? _eventBus;
    private ICardMoveService? _cardMoveService;
    private Func<ChoiceRequest, ChoiceResult>? _getPlayerChoice;

    /// <inheritdoc />
    public override string Id => "feedback";

    /// <inheritdoc />
    public override string Name => "反馈";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Trigger;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.None;

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
    public void OnAfterDamage(AfterDamageEvent evt)
    {
        if (_game is null || _owner is null || _cardMoveService is null)
            return;

        // Only process for the owner (target of damage)
        if (evt.Damage.TargetSeat != _owner.Seat)
            return;

        // Check if skill is active
        if (!IsActive(_game, _owner))
            return;

        // Check if damage has a valid source (not null or -1)
        if (evt.Damage.SourceSeat < 0)
            return;

        // Find damage source player
        var damageSource = _game.Players.FirstOrDefault(p => p.Seat == evt.Damage.SourceSeat);
        if (damageSource is null || !damageSource.IsAlive)
            return;

        // Check if already triggered for this damage event
        // Use a unique key based on damage descriptor to track per-damage-event triggering
        var damageKey = GetDamageEventKey(evt.Damage);
        var triggerKey = $"feedback_triggered_{damageKey}";
        if (_owner.Flags.ContainsKey(triggerKey))
        {
            return; // Already triggered for this damage event
        }

        // Get available cards from damage source (hand + equipment, excluding judgement zone)
        var availableCards = GetAvailableCardsFromSource(damageSource);
        if (availableCards.Count == 0)
        {
            return; // No cards available to obtain
        }

        // Mark as triggered for this damage event
        _owner.Flags[triggerKey] = true;

        // Ask player if they want to activate Feedback
        // For v1, we'll automatically trigger if getPlayerChoice is not available
        if (_getPlayerChoice is null)
        {
            // Auto-trigger: obtain a card automatically
            ObtainCardFromSource(_game, _owner, damageSource, availableCards);
            return;
        }

        // Ask player to confirm
        var choiceRequest = new ChoiceRequest(
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
            var choiceResult = _getPlayerChoice(choiceRequest);
            if (choiceResult?.Confirmed == true)
            {
                ObtainCardFromSource(_game, _owner, damageSource, availableCards);
            }
        }
        catch
        {
            // If getting choice fails, silently ignore
        }
    }

    /// <summary>
    /// Gets a unique key for a damage event to track per-damage-event triggering.
    /// Uses damage descriptor properties to create a unique identifier.
    /// </summary>
    private static string GetDamageEventKey(DamageDescriptor damage)
    {
        // Use a combination of source, target, amount, and reason to identify unique damage events
        // Note: This is a simple approach. In a more sophisticated system, we might use a DamageId.
        return $"source_{damage.SourceSeat}_target_{damage.TargetSeat}_amount_{damage.Amount}_reason_{damage.Reason ?? "none"}";
    }

    /// <summary>
    /// Gets available cards from damage source that can be obtained.
    /// Includes hand cards and equipment cards, but excludes judgement zone cards.
    /// </summary>
    private static List<Card> GetAvailableCardsFromSource(Player damageSource)
    {
        var availableCards = new List<Card>();

        // Add hand cards
        availableCards.AddRange(damageSource.HandZone.Cards);

        // Add equipment cards
        availableCards.AddRange(damageSource.EquipmentZone.Cards);

        // Exclude judgement zone cards (not obtainable)
        // Already excluded since we only add from HandZone and EquipmentZone

        return availableCards;
    }

    /// <summary>
    /// Obtains a card from damage source.
    /// If equipment cards are available, asks player to choose one (if getPlayerChoice is available).
    /// If only hand cards are available, asks player to choose one by index (player selects by specifying card ID,
    /// which corresponds to a position/index in the hand card collection, even though cards are not visible).
    /// </summary>
    private void ObtainCardFromSource(Game game, Player owner, Player damageSource, List<Card> availableCards)
    {
        if (_cardMoveService is null)
            return;

        // Separate equipment cards and hand cards
        var equipmentCards = damageSource.EquipmentZone.Cards
            .Where(c => availableCards.Contains(c))
            .ToList();
        var handCards = damageSource.HandZone.Cards
            .Where(c => availableCards.Contains(c))
            .ToList();

        Card? cardToObtain = null;

        // If equipment cards are available, ask player to choose one (if getPlayerChoice is available)
        if (equipmentCards.Count > 0)
        {
            if (_getPlayerChoice is not null)
            {
                // If both equipment and hand cards are available, prefer equipment (visible)
                // Ask player to choose from equipment cards
                var choiceRequest = new ChoiceRequest(
                    RequestId: Guid.NewGuid().ToString(),
                    PlayerSeat: owner.Seat,
                    ChoiceType: ChoiceType.SelectCards,
                    TargetConstraints: null,
                    AllowedCards: equipmentCards,
                    ResponseWindowId: null,
                    CanPass: handCards.Count > 0 // Can pass if hand cards are available
                );

                try
                {
                    var choiceResult = _getPlayerChoice(choiceRequest);
                    if (choiceResult?.SelectedCardIds is not null && choiceResult.SelectedCardIds.Count > 0)
                    {
                        cardToObtain = equipmentCards.FirstOrDefault(c => choiceResult.SelectedCardIds.Contains(c.Id));
                    }
                }
                catch
                {
                    // If getting choice fails, fall back to selecting from hand cards
                }
            }
            else
            {
                // If no getPlayerChoice, automatically select first equipment card
                cardToObtain = equipmentCards[0];
            }
        }

        // If no equipment card was selected and hand cards are available, ask player to choose one by index
        if (cardToObtain is null && handCards.Count > 0)
        {
            cardToObtain = SelectHandCard(owner, handCards);
        }

        // If we have a card to obtain, move it to owner's hand
        if (cardToObtain is not null)
        {
            try
            {
                game.MoveCardToHand(owner, cardToObtain, _cardMoveService);
            }
            catch
            {
                // If moving fails (e.g., card no longer in expected zone), silently ignore
            }
        }
    }

    /// <summary>
    /// Selects a hand card from the available hand cards.
    /// If getPlayerChoice is available, asks player to choose one by index (player selects by specifying card ID,
    /// which corresponds to a position/index in the hand card collection, even though cards are not visible).
    /// Otherwise, automatically selects the first hand card.
    /// </summary>
    /// <param name="owner">The player who owns the Feedback skill.</param>
    /// <param name="handCards">The list of hand cards available for selection.</param>
    /// <returns>The selected hand card, or null if selection failed or no cards available.</returns>
    private Card? SelectHandCard(Player owner, List<Card> handCards)
    {
        if (handCards.Count == 0)
            return null;

        if (_getPlayerChoice is not null)
        {
            // Ask player to choose a hand card by index (even though cards are not visible)
            // Player can select by specifying the card ID, which corresponds to a position in the hand
            var choiceRequest = new ChoiceRequest(
                RequestId: Guid.NewGuid().ToString(),
                PlayerSeat: owner.Seat,
                ChoiceType: ChoiceType.SelectCards,
                TargetConstraints: null,
                AllowedCards: handCards,
                ResponseWindowId: null,
                CanPass: false // Must select one hand card
            );

            try
            {
                var choiceResult = _getPlayerChoice(choiceRequest);
                if (choiceResult?.SelectedCardIds is not null && choiceResult.SelectedCardIds.Count > 0)
                {
                    return handCards.FirstOrDefault(c => choiceResult.SelectedCardIds.Contains(c.Id));
                }
            }
            catch
            {
                // If getting choice fails, cannot obtain card
            }

            return null;
        }
        else
        {
            // If no getPlayerChoice, automatically select first hand card
            return handCards[0];
        }
    }
}

/// <summary>
/// Factory for creating FeedbackSkill instances.
/// </summary>
public sealed class FeedbackSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new FeedbackSkill();
    }
}
