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
/// Twin Swords (雌雄双股剑) skill: Trigger skill that allows interaction with opposite gender targets.
/// When you use a Slash on an opposite gender target, you can make them choose:
/// 1) Discard 1 hand card, or
/// 2) Let you draw 1 card.
/// Attack Range: 2
/// </summary>
public sealed class TwinSwordsSkill : BaseSkill, IAttackDistanceModifyingSkill
{
    private Game? _game;
    private Player? _owner;
    private IEventBus? _eventBus;
    private ICardMoveService? _cardMoveService;
    private Func<ChoiceRequest, ChoiceResult>? _getPlayerChoice;

    /// <inheritdoc />
    public override string Id => "twin_swords";

    /// <inheritdoc />
    public override string Name => "雌雄";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Trigger;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.ModifiesRules;

    /// <summary>
    /// The attack range provided by Twin Swords.
    /// </summary>
    private const int AttackRange = 2;

    /// <summary>
    /// Sets the card move service for discarding cards and drawing cards.
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

        eventBus.Subscribe<AfterCardTargetsDeclaredEvent>(OnAfterCardTargetsDeclared);
    }

    /// <inheritdoc />
    public override void Detach(Game game, Player owner, IEventBus eventBus)
    {
        if (eventBus is null)
            return;

        eventBus.Unsubscribe<AfterCardTargetsDeclaredEvent>(OnAfterCardTargetsDeclared);

        _game = null;
        _owner = null;
        _eventBus = null;
        _cardMoveService = null;
        _getPlayerChoice = null;
    }

    /// <summary>
    /// Handles the AfterCardTargetsDeclaredEvent.
    /// </summary>
    private void OnAfterCardTargetsDeclared(AfterCardTargetsDeclaredEvent evt)
    {
        if (_game is null || _owner is null || _cardMoveService is null)
            return;

        // Only process if the owner is the source player
        if (evt.SourcePlayerSeat != _owner.Seat)
            return;

        // Check if skill is active
        if (!IsActive(_game, _owner))
            return;

        // Check if the card is a Slash
        if (evt.Card.CardSubType != CardSubType.Slash)
            return;

        // Process each target separately (for multi-target scenarios)
        foreach (var targetSeat in evt.TargetSeats)
        {
            var target = _game.Players.FirstOrDefault(p => p.Seat == targetSeat);
            if (target is null || !target.IsAlive)
                continue;

            // Check if target is opposite gender
            if (!IsOppositeGender(_owner.Gender, target.Gender))
                continue;

            // Process this target
            ProcessTarget(evt.Card, target);
        }
    }

    /// <summary>
    /// Checks if two genders are opposite.
    /// Neutral is considered non-opposite to any gender.
    /// </summary>
    private static bool IsOppositeGender(Gender sourceGender, Gender targetGender)
    {
        if (sourceGender == Gender.Neutral || targetGender == Gender.Neutral)
            return false;

        return sourceGender != targetGender;
    }

    /// <summary>
    /// Processes a single target for Twin Swords skill.
    /// </summary>
    private void ProcessTarget(Card slashCard, Player target)
    {
        if (_game is null || _owner is null || _cardMoveService is null)
            return;

        // Ask owner if they want to activate Twin Swords for this target
        if (_getPlayerChoice is null)
        {
            // Auto-trigger: automatically activate
            ActivateTwinSwords(target);
            return;
        }

        // Ask owner to confirm
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
                ActivateTwinSwords(target);
            }
        }
        catch
        {
            // If getting choice fails, silently ignore
        }
    }

    /// <summary>
    /// Activates Twin Swords: makes target choose between discarding a hand card or letting owner draw a card.
    /// </summary>
    private void ActivateTwinSwords(Player target)
    {
        if (_game is null || _owner is null || _cardMoveService is null)
            return;

        // Check if target has hand cards
        var targetHandCards = target.HandZone.Cards?.ToList() ?? new List<Card>();
        var hasHandCards = targetHandCards.Count > 0;

        // If target has no hand cards, they can only choose to let owner draw
        if (!hasHandCards)
        {
            // Target has no choice, automatically let owner draw
            DrawCardForOwner();
            return;
        }

        // Ask target to choose
        if (_getPlayerChoice is null)
        {
            // Auto-choose: prefer discarding hand card if available
            DiscardTargetHandCard(target, targetHandCards);
            return;
        }

        // Create choice request for target
        // Note: In a real game, this would be a special choice type for Twin Swords
        // For now, we'll use a simplified approach: ask target to select a card (discard) or pass (draw)
        var choiceRequest = new ChoiceRequest(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: target.Seat,
            ChoiceType: ChoiceType.SelectCards,
            TargetConstraints: null,
            AllowedCards: targetHandCards,
            ResponseWindowId: null,
            CanPass: true // Can pass to choose "let owner draw"
        );

        try
        {
            var choiceResult = _getPlayerChoice(choiceRequest);
            if (choiceResult?.SelectedCardIds is not null && choiceResult.SelectedCardIds.Count > 0)
            {
                // Target chose to discard a hand card
                var cardToDiscard = targetHandCards.FirstOrDefault(c => choiceResult.SelectedCardIds.Contains(c.Id));
                if (cardToDiscard is not null)
                {
                    DiscardTargetHandCard(target, new List<Card> { cardToDiscard });
                }
                else
                {
                    // Fallback: let owner draw
                    DrawCardForOwner();
                }
            }
            else
            {
                // Target chose to pass (let owner draw)
                DrawCardForOwner();
            }
        }
        catch
        {
            // If getting choice fails, fallback to letting owner draw
            DrawCardForOwner();
        }
    }

    /// <summary>
    /// Discards a hand card from target.
    /// </summary>
    private void DiscardTargetHandCard(Player target, List<Card> cardsToDiscard)
    {
        if (_cardMoveService is null || _game is null)
            return;

        try
        {
            _cardMoveService.DiscardFromHand(_game, target, cardsToDiscard);
        }
        catch
        {
            // If discarding fails, silently ignore
        }
    }

    /// <summary>
    /// Draws a card for the owner.
    /// </summary>
    private void DrawCardForOwner()
    {
        if (_cardMoveService is null || _game is null || _owner is null)
            return;

        try
        {
            // Draw 1 card from draw pile to owner's hand
            var drawPile = _game.DrawPile;
            if (drawPile.Cards is null || drawPile.Cards.Count == 0)
                return; // No cards to draw

            var cardToDraw = drawPile.Cards.First();
            var moveDescriptor = new CardMoveDescriptor(
                SourceZone: drawPile,
                TargetZone: _owner.HandZone,
                Cards: new[] { cardToDraw },
                Reason: CardMoveReason.Draw,
                Ordering: CardMoveOrdering.ToTop,
                Game: _game
            );
            _cardMoveService.MoveSingle(moveDescriptor);
        }
        catch
        {
            // If drawing fails, silently ignore
        }
    }

    /// <inheritdoc />
    public int? ModifyAttackDistance(int current, Game game, Player from, Player to)
    {
        // Twin Swords provides attack range of 2
        // If current distance is less than 2, set it to 2
        if (!IsActive(game, from))
            return null;

        // Set attack distance to 2 (weapon's fixed range)
        return AttackRange;
    }
}

/// <summary>
/// Factory for creating TwinSwordsSkill instances.
/// </summary>
public sealed class TwinSwordsSkillFactory : IEquipmentSkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new TwinSwordsSkill();
    }
}

