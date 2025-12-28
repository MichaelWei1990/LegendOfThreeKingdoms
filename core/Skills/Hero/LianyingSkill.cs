using System;
using System.Linq;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Lianying (连营) skill: Trigger skill (optional) that allows you to draw 1 card
/// when you lose your last hand card.
/// </summary>
public sealed class LianyingSkill : BaseSkill, ICardMovedSkill
{
    private Game? _game;
    private Player? _owner;
    private IEventBus? _eventBus;
    private ICardMoveService? _cardMoveService;
    private Func<ChoiceRequest, ChoiceResult>? _getPlayerChoice;

    // Track hand count before move to detect transition from >0 to 0
    // Use CardIds as operation identifier since Before and After events have the same CardIds
    private int? _handCountBeforeMove;
    private HashSet<int>? _pendingMoveCardIds; // Track card IDs of the current move operation

    /// <inheritdoc />
    public override string Id => "lianying";

    /// <inheritdoc />
    public override string Name => "连营";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Trigger;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.None;

    /// <summary>
    /// Sets the card move service for drawing cards.
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
    public void SetPlayerChoice(Func<ChoiceRequest, ChoiceResult> getPlayerChoice)
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

        eventBus.Subscribe<CardMovedEvent>(OnCardMoved);
    }

    /// <inheritdoc />
    public override void Detach(Game game, Player owner, IEventBus eventBus)
    {
        if (eventBus is null)
            return;

        eventBus.Unsubscribe<CardMovedEvent>(OnCardMoved);

        _game = null;
        _owner = null;
        _eventBus = null;
        _cardMoveService = null;
        _getPlayerChoice = null;
        _handCountBeforeMove = null;
        _pendingMoveCardIds = null;
    }

    /// <inheritdoc />
    public void OnCardMoved(CardMovedEvent evt)
    {
        if (_game is null || _owner is null || _cardMoveService is null)
            return;

        var moveEvent = evt.CardMoveEvent;

        // Only process moves from hand zone of the owner
        if (!IsHandZone(moveEvent.SourceZoneId, _owner.Seat))
            return;

        // Check if skill is active
        if (!IsActive(_game, _owner))
            return;

        // Handle Before timing: record hand count and card IDs
        if (moveEvent.Timing == CardMoveEventTiming.Before)
        {
            _handCountBeforeMove = _owner.HandZone.Cards.Count;
            // Store card IDs to match with After event
            _pendingMoveCardIds = new HashSet<int>(moveEvent.CardIds);
            return;
        }

        // Handle After timing: check if trigger condition is met
        if (moveEvent.Timing == CardMoveEventTiming.After)
        {
            // Verify this is the same move operation by checking card IDs
            if (_pendingMoveCardIds is null || !_pendingMoveCardIds.SetEquals(moveEvent.CardIds))
            {
                // Different move operation or no pending move, reset state
                _handCountBeforeMove = null;
                _pendingMoveCardIds = null;
                return;
            }

            // Check trigger condition:
            // 1. Hand count before move was > 0
            // 2. Hand count after move is 0
            if (_handCountBeforeMove.HasValue && _handCountBeforeMove.Value > 0)
            {
                var handCountAfter = _owner.HandZone.Cards.Count;
                if (handCountAfter == 0)
                {
                    // Trigger condition met: lost last hand card
                    // Reset state to avoid duplicate triggers for the same move
                    _handCountBeforeMove = null;
                    _pendingMoveCardIds = null;

                    // Ask player if they want to use Lianying (optional trigger)
                    if (_getPlayerChoice is not null)
                    {
                        var request = new ChoiceRequest(
                            RequestId: Guid.NewGuid().ToString(),
                            PlayerSeat: _owner.Seat,
                            ChoiceType: ChoiceType.Confirm,
                            TargetConstraints: null,
                            AllowedCards: null,
                            ResponseWindowId: null,
                            CanPass: true); // Player can choose not to use the skill

                        try
                        {
                            var result = _getPlayerChoice(request);
                            if (result?.Confirmed != true)
                            {
                                // Player chose not to use the skill
                                return;
                            }
                        }
                        catch
                        {
                            // If choice fails, don't trigger
                            return;
                        }
                    }

                    // Draw 1 card
                    try
                    {
                        _cardMoveService.DrawCards(_game, _owner, 1);
                    }
                    catch (Exception)
                    {
                        // If drawing fails (e.g., draw pile empty), silently ignore
                        // This matches the behavior of other trigger skills
                    }
                }
            }
        }
    }

    /// <summary>
    /// Checks if a zone ID represents a hand zone for the given player seat.
    /// </summary>
    private static bool IsHandZone(string zoneId, int playerSeat)
    {
        return zoneId == $"Hand_{playerSeat}";
    }
}

/// <summary>
/// Factory for creating LianyingSkill instances.
/// </summary>
public sealed class LianyingSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new LianyingSkill();
    }
}

