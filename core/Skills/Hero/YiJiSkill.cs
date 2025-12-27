using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// YiJi (遗计) skill: After you take 1 point of damage, you can look at the top 2 cards of the draw pile,
/// then give these 2 cards to one or two players (can include yourself).
/// </summary>
public sealed class YiJiSkill : BaseSkill, IAfterDamageSkill
{
    private Game? _game;
    private Player? _owner;
    private IEventBus? _eventBus;
    private ICardMoveService? _cardMoveService;
    private Func<ChoiceRequest, ChoiceResult>? _getPlayerChoice;
    private IResolutionStack? _resolutionStack;

    /// <inheritdoc />
    public override string Id => "yiji";

    /// <inheritdoc />
    public override string Name => "遗计";

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

        eventBus.Subscribe<AfterDamageEvent>(OnAfterDamage);
    }

    /// <inheritdoc />
    public override void Detach(Game game, Player owner, IEventBus eventBus)
    {
        if (eventBus is not null)
        {
            eventBus.Unsubscribe<AfterDamageEvent>(OnAfterDamage);
        }

        _game = null;
        _owner = null;
        _eventBus = null;
        _cardMoveService = null;
        _getPlayerChoice = null;
        _resolutionStack = null;
    }

    /// <summary>
    /// Sets the card move service for this skill.
    /// </summary>
    public void SetCardMoveService(ICardMoveService cardMoveService)
    {
        _cardMoveService = cardMoveService;
    }

    /// <summary>
    /// Sets the player choice function for this skill.
    /// </summary>
    public void SetGetPlayerChoice(Func<ChoiceRequest, ChoiceResult> getPlayerChoice)
    {
        _getPlayerChoice = getPlayerChoice;
    }

    /// <summary>
    /// Sets the resolution stack for this skill.
    /// </summary>
    public void SetResolutionStack(IResolutionStack resolutionStack)
    {
        _resolutionStack = resolutionStack;
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

        // Process each point of damage separately
        // If damage is 2, trigger YiJi twice (once for each point)
        var damageAmount = evt.Damage.Amount;
        for (int pointIndex = 0; pointIndex < damageAmount; pointIndex++)
        {
            ProcessYiJiForOneDamagePoint();
        }
    }

    private void ProcessYiJiForOneDamagePoint()
    {
        // These should never be null if skill is properly attached
        Debug.Assert(_game is not null, "_game should not be null when skill is attached.");
        Debug.Assert(_owner is not null, "_owner should not be null when skill is attached.");
        Debug.Assert(_cardMoveService is not null, "_cardMoveService should not be null when skill is attached.");
        if (_game is null || _owner is null || _cardMoveService is null)
            return;

        // Ask player if they want to activate YiJi
        if (_getPlayerChoice is not null)
        {
            var confirmRequest = new ChoiceRequest(
                RequestId: Guid.NewGuid().ToString(),
                PlayerSeat: _owner.Seat,
                ChoiceType: ChoiceType.Confirm,
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
                // This should rarely happen: _getPlayerChoice should not throw exceptions in normal operation.
                Debug.Assert(false, "An exception occurred while getting player choice for YiJi activation confirmation. This should not happen in normal operation.");
                // If getting choice fails, skip this trigger
                return;
            }
        }
        else
        {
            // If no getPlayerChoice, skip (YiJi requires player choice)
            return;
        }

        // Check if draw pile has at least 2 cards
        if (_game.DrawPile.Cards.Count < 2)
        {
            return; // Not enough cards in draw pile
        }

        // Create a temporary zone to hold the 2 cards (visible only to owner)
        var tempZoneId = $"YiJiPool_{_owner.Seat}_{Guid.NewGuid()}";
        var tempZone = new Zone(tempZoneId, ownerSeat: _owner.Seat, isPublic: false);

        // Move 2 cards from draw pile top to temporary zone
        if (!MoveTwoCardsToTempZone(_game, tempZone, _cardMoveService))
        {
            return; // Failed to move cards
        }

        var cardsInTempZone = tempZone.Cards.ToList();
        if (cardsInTempZone.Count != 2)
        {
            // This should never happen if MoveTwoCardsToTempZone returned true
            Debug.Assert(false, $"cardsInTempZone.Count should be 2 after MoveTwoCardsToTempZone returned true, but got {cardsInTempZone.Count}. This indicates a logic error in card movement.");
            // Return cards to draw pile if something went wrong
            ReturnCardsToDrawPile(_game, cardsInTempZone, _cardMoveService, tempZone);
            return;
        }

        // Ask player to select targets (1 or 2)
        // We'll allow 1-2 targets, and determine distribution based on selection
        var targetResult = AskForTargets();
        if (targetResult is null)
        {
            ReturnCardsToDrawPile(_game, cardsInTempZone, _cardMoveService, tempZone);
            return;
        }

        // Distribute cards based on number of targets
        if (targetResult.Count == 1)
        {
            DistributeToOneTarget(cardsInTempZone, targetResult[0], tempZone);
        }
        else if (targetResult.Count == 2)
        {
            DistributeToTwoTargets(cardsInTempZone, targetResult[0], targetResult[1], tempZone);
        }
        else
        {
            // This should never happen: AskForTargets() validates that Count is 1 or 2
            Debug.Assert(false, $"targetResult.Count should be 1 or 2, but got {targetResult.Count}. This indicates a logic error in AskForTargets().");
            ReturnCardsToDrawPile(_game, cardsInTempZone, _cardMoveService, tempZone);
        }
    }

    private bool MoveTwoCardsToTempZone(Game game, Zone tempZone, ICardMoveService cardMoveService)
    {
        if (game.DrawPile is not Zone drawZone)
        {
            // This should never happen: DrawPile should always be Zone type in the model.
            Debug.Assert(false, $"game.DrawPile should be Zone type, but got {game.DrawPile?.GetType().Name}. This indicates a model error.");
            return false;
        }

        var drawCards = drawZone.Cards;
        if (drawCards.Count < 2)
            return false;

        // Get top 2 cards from draw pile
        var cardsToMove = drawCards.Take(2).ToList();

        // Move 2 cards from top of draw pile to temp zone using CardMoveService
        var moveDescriptor = new CardMoveDescriptor(
            SourceZone: drawZone,
            TargetZone: tempZone,
            Cards: cardsToMove,
            Reason: CardMoveReason.Draw,
            Ordering: CardMoveOrdering.ToTop,
            Game: game
        );

        try
        {
            cardMoveService.MoveMany(moveDescriptor);
            return true;
        }
        catch
        {
            // This should rarely happen: CardMoveService.MoveMany should not throw exceptions in normal operation.
            Debug.Assert(false, "An exception occurred while moving cards from draw pile to temporary zone. This should not happen in normal operation.");
            return false;
        }
    }

    private void ReturnCardsToDrawPile(Game game, List<Card> cards, ICardMoveService cardMoveService, Zone? sourceZone = null)
    {
        if (game.DrawPile is not Zone drawZone)
        {
            // This should never happen: DrawPile should always be Zone type in the model.
            Debug.Assert(false, $"game.DrawPile should be Zone type, but got {game.DrawPile?.GetType().Name}. This indicates a model error.");
            return;
        }
        
        if (cards.Count == 0)
            return;

        // If source zone is provided, use CardMoveService
        if (sourceZone is not null)
        {
            var moveDescriptor = new CardMoveDescriptor(
                SourceZone: sourceZone,
                TargetZone: drawZone,
                Cards: cards,
                Reason: CardMoveReason.Draw,
                Ordering: CardMoveOrdering.ToBottom, // Return to bottom
                Game: game
            );

            try
            {
                cardMoveService.MoveMany(moveDescriptor);
                return;
            }
            catch
            {
                // This should rarely happen: CardMoveService.MoveMany should not throw exceptions in normal operation.
                Debug.Assert(false, "An exception occurred while returning cards to draw pile via CardMoveService. Falling back to direct manipulation.");
                // If moving fails, fall back to direct manipulation
            }
        }

        // Fallback: directly add cards to bottom of draw pile
        // This is used when source zone is not available or CardMoveService fails
        var drawCards = drawZone.MutableCards;
        foreach (var card in cards)
        {
            drawCards.Add(card);
        }
    }

    private List<int>? AskForTargets()
    {
        if (_getPlayerChoice is null || _owner is null || _game is null)
            return null;

        // Ask player to select 1 or 2 targets
        var targetConstraints = new TargetConstraints(
            MinTargets: 1,
            MaxTargets: 2,
            FilterType: TargetFilterType.Any
        );

        var targetRequest = new ChoiceRequest(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: _owner.Seat,
            ChoiceType: ChoiceType.SelectTargets,
            TargetConstraints: targetConstraints,
            AllowedCards: null,
            ResponseWindowId: null,
            CanPass: false
        );

        try
        {
            var targetResult = _getPlayerChoice(targetRequest);
            if (targetResult?.SelectedTargetSeats is null || targetResult.SelectedTargetSeats.Count < 1 || targetResult.SelectedTargetSeats.Count > 2)
            {
                return null;
            }

            // Validate targets are alive
            var validTargets = targetResult.SelectedTargetSeats
                .Where(seat => _game.Players.FirstOrDefault(p => p.Seat == seat)?.IsAlive == true)
                .ToList();

            if (validTargets.Count != targetResult.SelectedTargetSeats.Count)
            {
                return null; // Some targets are invalid
            }

            return validTargets;
        }
        catch
        {
            // This should rarely happen: _getPlayerChoice should not throw exceptions in normal operation.
            Debug.Assert(false, "An exception occurred while getting player choice for target selection. This should not happen in normal operation.");
            return null;
        }
    }

    private void DistributeToOneTarget(List<Card> cards, int targetSeat, Zone sourceZone)
    {
        // These should never be null if skill is properly attached
        Debug.Assert(_game is not null, "_game should not be null when skill is attached.");
        Debug.Assert(_owner is not null, "_owner should not be null when skill is attached.");
        Debug.Assert(_cardMoveService is not null, "_cardMoveService should not be null when skill is attached.");
        if (_game is null || _owner is null || _cardMoveService is null)
            return;

        var target = _game.Players.FirstOrDefault(p => p.Seat == targetSeat);
        if (target is null || !target.IsAlive || target.HandZone is not Zone targetHandZone)
        {
            // This should rarely happen: AskForTargets() validates that targets are alive.
            // However, game state may change between selection and distribution (e.g., other skills).
            // If target is null or HandZone is not Zone, this may indicate a model error.
            Debug.Assert(target is not null, $"Target with seat {targetSeat} should exist since it was validated in AskForTargets().");
            Debug.Assert(target?.HandZone is Zone, $"Target's HandZone should be Zone type, but got {target?.HandZone?.GetType().Name}.");
            ReturnCardsToDrawPile(_game, cards, _cardMoveService);
            return;
        }

        // Move both cards to target's hand
        foreach (var card in cards)
        {
            var moveDescriptor = new CardMoveDescriptor(
                SourceZone: sourceZone,
                TargetZone: targetHandZone,
                Cards: new[] { card },
                Reason: CardMoveReason.Draw,
                Ordering: CardMoveOrdering.ToTop,
                Game: _game
            );
            _cardMoveService.MoveSingle(moveDescriptor);
        }
    }

    private void DistributeToTwoTargets(List<Card> cards, int target1Seat, int target2Seat, Zone sourceZone)
    {
        // These should never be null if skill is properly attached
        Debug.Assert(_game is not null, "_game should not be null when skill is attached.");
        Debug.Assert(_owner is not null, "_owner should not be null when skill is attached.");
        Debug.Assert(_cardMoveService is not null, "_cardMoveService should not be null when skill is attached.");
        Debug.Assert(_getPlayerChoice is not null, "_getPlayerChoice should not be null when distributing to two targets.");
        if (_game is null || _owner is null || _cardMoveService is null || _getPlayerChoice is null)
            return;

        var target1 = _game.Players.FirstOrDefault(p => p.Seat == target1Seat);
        var target2 = _game.Players.FirstOrDefault(p => p.Seat == target2Seat);

        if (target1 is null || !target1.IsAlive || target2 is null || !target2.IsAlive)
        {
            // This should rarely happen: AskForTargets() validates that targets are alive.
            // However, game state may change between selection and distribution (e.g., other skills).
            // If targets are null, this may indicate a logic error.
            Debug.Assert(target1 is not null, $"Target1 with seat {target1Seat} should exist since it was validated in AskForTargets().");
            Debug.Assert(target2 is not null, $"Target2 with seat {target2Seat} should exist since it was validated in AskForTargets().");
            ReturnCardsToDrawPile(_game, cards, _cardMoveService);
            return;
        }

        if (target1.HandZone is not Zone target1HandZone || target2.HandZone is not Zone target2HandZone)
        {
            // This should never happen: HandZone should always be Zone type in the model.
            Debug.Assert(false, $"Target's HandZone should be Zone type. target1.HandZone: {target1.HandZone?.GetType().Name}, target2.HandZone: {target2.HandZone?.GetType().Name}. This indicates a model error.");
            ReturnCardsToDrawPile(_game, cards, _cardMoveService);
            return;
        }

        // Ask player to assign which card goes to which target
        // We'll use SelectCards to let player choose which card goes to target1
        // The other card will go to target2
        var cardAssignmentRequest = new ChoiceRequest(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: _owner.Seat,
            ChoiceType: ChoiceType.SelectCards,
            TargetConstraints: null,
            AllowedCards: cards, // Player can select one of the two cards
            ResponseWindowId: null,
            CanPass: false
        );

        try
        {
            var cardAssignmentResult = _getPlayerChoice(cardAssignmentRequest);
            if (cardAssignmentResult?.SelectedCardIds is null || cardAssignmentResult.SelectedCardIds.Count != 1)
            {
                // This should rarely happen: CanPass is false, so player must select exactly 1 card.
                // However, _getPlayerChoice implementation may return invalid result in edge cases.
                Debug.Assert(false, $"cardAssignmentResult should have exactly 1 SelectedCardId, but got: SelectedCardIds is null: {cardAssignmentResult?.SelectedCardIds is null}, Count: {cardAssignmentResult?.SelectedCardIds?.Count ?? 0}. This may indicate an issue with _getPlayerChoice implementation.");
                // Invalid selection, return cards to draw pile
                ReturnCardsToDrawPile(_game, cards, _cardMoveService);
                return;
            }

            var selectedCardId = cardAssignmentResult.SelectedCardIds[0];
            var cardForTarget1 = cards.FirstOrDefault(c => c.Id == selectedCardId);
            var cardForTarget2 = cards.FirstOrDefault(c => c.Id != selectedCardId);

            if (cardForTarget1 is null || cardForTarget2 is null)
            {
                // This should never happen if cards list has 2 cards and selectedCardId is from that list
                Debug.Assert(false, $"Could not find cards in the list. cardForTarget1 is null: {cardForTarget1 is null}, cardForTarget2 is null: {cardForTarget2 is null}, selectedCardId: {selectedCardId}, cards count: {cards.Count}. This indicates a logic error in card selection.");
                ReturnCardsToDrawPile(_game, cards, _cardMoveService);
                return;
            }

            // Move cardForTarget1 to target1's hand
            var moveDescriptor1 = new CardMoveDescriptor(
                SourceZone: sourceZone,
                TargetZone: target1HandZone,
                Cards: new[] { cardForTarget1 },
                Reason: CardMoveReason.Draw,
                Ordering: CardMoveOrdering.ToTop,
                Game: _game
            );
            _cardMoveService.MoveSingle(moveDescriptor1);

            // Move cardForTarget2 to target2's hand
            var moveDescriptor2 = new CardMoveDescriptor(
                SourceZone: sourceZone,
                TargetZone: target2HandZone,
                Cards: new[] { cardForTarget2 },
                Reason: CardMoveReason.Draw,
                Ordering: CardMoveOrdering.ToTop,
                Game: _game
            );
            _cardMoveService.MoveSingle(moveDescriptor2);
        }
        catch
        {
            // This should rarely happen: _getPlayerChoice should not throw exceptions in normal operation.
            // If it does, it may indicate an implementation error or unexpected game state.
            Debug.Assert(false, "An exception occurred while getting player choice for card assignment. This should not happen in normal operation.");
            ReturnCardsToDrawPile(_game, cards, _cardMoveService);
        }
    }
}

/// <summary>
/// Factory for creating YiJiSkill instances.
/// </summary>
public sealed class YiJiSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new YiJiSkill();
    }
}

