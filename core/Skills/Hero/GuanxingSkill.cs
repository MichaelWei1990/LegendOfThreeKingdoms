using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Guanxing (观星) skill: Optional trigger skill that allows viewing and rearranging top cards of draw pile during Prepare phase.
/// During Prepare phase, you can view the top X cards (X = min(alive players count, 5)) and arrange them on top or bottom of draw pile.
/// </summary>
public sealed class GuanxingSkill : BaseSkill
{
    private Game? _game;
    private Player? _owner;
    private IEventBus? _eventBus;
    private ICardMoveService? _cardMoveService;
    private Func<ChoiceRequest, ChoiceResult>? _getPlayerChoice;

    /// <inheritdoc />
    public override string Id => "guanxing";

    /// <inheritdoc />
    public override string Name => "观星";

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
    /// Sets the player choice function for optional trigger.
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

        eventBus.Subscribe<PhaseStartEvent>(OnPhaseStart);
    }

    /// <inheritdoc />
    public override void Detach(Game game, Player owner, IEventBus eventBus)
    {
        if (eventBus is null)
            return;

        eventBus.Unsubscribe<PhaseStartEvent>(OnPhaseStart);

        _game = null;
        _owner = null;
        _eventBus = null;
        _cardMoveService = null;
        _getPlayerChoice = null;
    }

    private void OnPhaseStart(PhaseStartEvent evt)
    {
        if (_game is null || _owner is null || _cardMoveService is null)
            return;

        // Only trigger during Start phase (Prepare phase)
        if (evt.Phase != Phase.Start)
            return;

        // Only trigger for the owner's own Start phase
        if (evt.PlayerSeat != _owner.Seat)
            return;

        // Check if skill is active
        if (!IsActive(_game, _owner))
            return;

        // Calculate X = min(alive players count, 5)
        var aliveCount = _game.Players.Count(p => p.IsAlive);
        var x = Math.Min(aliveCount, 5);

        // Check if draw pile has enough cards
        if (_game.DrawPile.Cards.Count < x)
        {
            x = _game.DrawPile.Cards.Count;
        }

        // If X is 0, cannot activate
        if (x == 0)
            return;

        // Ask player if they want to activate Guanxing
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
                // If getting choice fails, skip activation
                return;
            }
        }
        else
        {
            // If no getPlayerChoice, skip (Guanxing requires player choice)
            return;
        }

        // Execute Guanxing: view and rearrange top X cards
        try
        {
            ExecuteGuanxing(_game, _owner, x, _cardMoveService, _getPlayerChoice);
        }
        catch (Exception)
        {
            // If execution fails, silently ignore
            // This matches the behavior of other trigger skills
        }
    }

    /// <summary>
    /// Executes the Guanxing skill: views top X cards and allows player to rearrange them.
    /// </summary>
    private static void ExecuteGuanxing(
        Game game,
        Player owner,
        int x,
        ICardMoveService cardMoveService,
        Func<ChoiceRequest, ChoiceResult>? getPlayerChoice)
    {
        if (game.DrawPile is not Zone drawZone)
        {
            throw new InvalidOperationException("Game.DrawPile must be a mutable Zone instance.");
        }

        // Step 1: Get top X cards from draw pile
        var drawCards = drawZone.Cards;
        if (drawCards.Count < x)
        {
            x = drawCards.Count;
        }

        if (x == 0)
            return;

        var topCards = drawCards.Take(x).ToList();

        // Step 2: Move cards to a temporary zone for viewing
        var tempZone = new Zone($"Temp_Guanxing_{owner.Seat}", owner.Seat, isPublic: false);
        var moveToTempDescriptor = new CardMoveDescriptor(
            SourceZone: drawZone,
            TargetZone: tempZone,
            Cards: topCards,
            Reason: CardMoveReason.Draw,
            Ordering: CardMoveOrdering.ToTop,
            Game: game
        );

        cardMoveService.MoveMany(moveToTempDescriptor);

        // Step 3: Ask player to arrange cards
        // First, ask which cards go to top and which go to bottom
        // Then ask for the order within each group
        var arrangement = GetCardArrangement(topCards, owner, getPlayerChoice);
        if (arrangement is null)
        {
            // Player cancelled or error occurred, return cards to draw pile in original order
            ReturnCardsToDrawPile(game, topCards, cardMoveService, tempZone, drawZone);
            return;
        }

        // Step 4: Place cards back to draw pile
        // First place bottom group (so they are at the bottom)
        // Then place top group (so they are at the top)
        if (arrangement.BottomInOrder.Count > 0)
        {
            var bottomCards = arrangement.BottomInOrder
                .Select(id => topCards.First(c => c.Id == id))
                .ToList();

            var moveBottomDescriptor = new CardMoveDescriptor(
                SourceZone: tempZone,
                TargetZone: drawZone,
                Cards: bottomCards,
                Reason: CardMoveReason.ReturnToDeckBottom,
                Ordering: CardMoveOrdering.ToBottom,
                Game: game
            );

            cardMoveService.MoveMany(moveBottomDescriptor);
        }

        if (arrangement.TopInOrder.Count > 0)
        {
            var topCardsArranged = arrangement.TopInOrder
                .Select(id => topCards.First(c => c.Id == id))
                .ToList();

            var moveTopDescriptor = new CardMoveDescriptor(
                SourceZone: tempZone,
                TargetZone: drawZone,
                Cards: topCardsArranged,
                Reason: CardMoveReason.ReturnToDeckTop,
                Ordering: CardMoveOrdering.ToTop,
                Game: game
            );

            cardMoveService.MoveMany(moveTopDescriptor);
        }
    }

    /// <summary>
    /// Gets the card arrangement from the player.
    /// Returns null if player cancels or an error occurs.
    /// </summary>
    private static GuanxingArrangement? GetCardArrangement(
        IReadOnlyList<Card> cards,
        Player owner,
        Func<ChoiceRequest, ChoiceResult>? getPlayerChoice)
    {
        if (getPlayerChoice is null)
            return null;

        // Step 1: Ask player to select which cards go to top (rest go to bottom)
        var selectTopRequest = new ChoiceRequest(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: owner.Seat,
            ChoiceType: ChoiceType.SelectCards,
            TargetConstraints: null,
            AllowedCards: cards,
            ResponseWindowId: null,
            CanPass: false // Must make a selection (can select all, none, or some)
        );

        try
        {
            var topSelection = getPlayerChoice(selectTopRequest);
            if (topSelection?.SelectedCardIds is null)
                return null;

            var topCardIds = topSelection.SelectedCardIds.ToHashSet();
            var bottomCardIds = cards.Select(c => c.Id).Where(id => !topCardIds.Contains(id)).ToList();

            // Step 2: Ask for order of top cards
            var topInOrder = GetCardOrder(cards.Where(c => topCardIds.Contains(c.Id)).ToList(), owner, getPlayerChoice, "top");
            if (topInOrder is null)
                return null;

            // Step 3: Ask for order of bottom cards
            var bottomInOrder = GetCardOrder(cards.Where(c => bottomCardIds.Contains(c.Id)).ToList(), owner, getPlayerChoice, "bottom");
            if (bottomInOrder is null)
                return null;

            return new GuanxingArrangement(topInOrder, bottomInOrder);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the order of cards by asking player to select them in order.
    /// For simplicity, we ask player to select cards one by one in the desired order.
    /// </summary>
    private static IReadOnlyList<int>? GetCardOrder(
        IReadOnlyList<Card> cards,
        Player owner,
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice,
        string groupName)
    {
        if (cards.Count == 0)
            return Array.Empty<int>();

        if (cards.Count == 1)
            return new[] { cards[0].Id };

        // For multiple cards, ask player to select them one by one in order
        // The order of selection determines the final order
        var orderedCardIds = new List<int>();
        var remainingCards = cards.ToList();

        while (remainingCards.Count > 0)
        {
            var orderRequest = new ChoiceRequest(
                RequestId: Guid.NewGuid().ToString(),
                PlayerSeat: owner.Seat,
                ChoiceType: ChoiceType.SelectCards,
                TargetConstraints: null,
                AllowedCards: remainingCards,
                ResponseWindowId: null,
                CanPass: false // Must select one card
            );

            try
            {
                var orderResult = getPlayerChoice(orderRequest);
                if (orderResult?.SelectedCardIds is null || orderResult.SelectedCardIds.Count == 0)
                    return null;

                var selectedId = orderResult.SelectedCardIds[0];
                orderedCardIds.Add(selectedId);
                remainingCards.RemoveAll(c => c.Id == selectedId);
            }
            catch
            {
                return null;
            }
        }

        return orderedCardIds;
    }

    /// <summary>
    /// Returns cards to draw pile in original order (fallback when arrangement is cancelled).
    /// </summary>
    private static void ReturnCardsToDrawPile(
        Game game,
        IReadOnlyList<Card> cards,
        ICardMoveService cardMoveService,
        Zone sourceZone,
        Zone drawZone)
    {
        if (cards.Count == 0)
            return;

        // Return cards to top in original order
        var moveDescriptor = new CardMoveDescriptor(
            SourceZone: sourceZone,
            TargetZone: drawZone,
            Cards: cards.ToList(),
            Reason: CardMoveReason.ReturnToDeckTop,
            Ordering: CardMoveOrdering.ToTop,
            Game: game
        );

        try
        {
            cardMoveService.MoveMany(moveDescriptor);
        }
        catch
        {
            // If moving fails, silently ignore
        }
    }

    /// <summary>
    /// Represents the arrangement decision for Guanxing cards.
    /// </summary>
    private sealed record GuanxingArrangement(
        IReadOnlyList<int> TopInOrder,      // Card IDs in order from top to bottom (index 0 is the new deck top)
        IReadOnlyList<int> BottomInOrder    // Card IDs in order from top to bottom of bottom section
    );
}

/// <summary>
/// Factory for creating GuanxingSkill instances.
/// </summary>
public sealed class GuanxingSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new GuanxingSkill();
    }
}

