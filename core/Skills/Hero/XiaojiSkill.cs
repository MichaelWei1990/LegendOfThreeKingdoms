using System;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Xiaoji (枭姬) skill: Trigger skill that draws 2 cards when equipment is removed from equipment zone.
/// </summary>
public sealed class XiaojiSkill : BaseSkill
{
    private Game? _game;
    private Player? _owner;
    private IEventBus? _eventBus;
    private ICardMoveService? _cardMoveService;

    /// <inheritdoc />
    public override string Id => "xiaoji";

    /// <inheritdoc />
    public override string Name => "枭姬";

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
    }

    private void OnCardMoved(CardMovedEvent evt)
    {
        if (_game is null || _owner is null || _cardMoveService is null)
            return;

        // Only process for the owner
        if (evt.CardMoveEvent.SourceOwnerSeat != _owner.Seat)
            return;

        // Only process After move events (equipment has actually been removed)
        if (evt.CardMoveEvent.Timing != CardMoveEventTiming.After)
            return;

        // Check if the source zone is an equipment zone
        // Equipment zone ID format: "Equip_{seat}"
        var sourceZoneId = evt.CardMoveEvent.SourceZoneId;
        if (!sourceZoneId.StartsWith("Equip_", StringComparison.Ordinal))
            return;

        // Check if skill is active
        if (!IsActive(_game, _owner))
            return;

        // Draw 2 cards
        // Core v1 convention: automatically trigger (no player choice)
        try
        {
            _cardMoveService.DrawCards(_game, _owner, 2);
        }
        catch (Exception)
        {
            // If drawing fails (e.g., draw pile empty), silently ignore
            // This matches the behavior of other trigger skills
        }
    }
}

/// <summary>
/// Factory for creating XiaojiSkill instances.
/// </summary>
public sealed class XiaojiSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new XiaojiSkill();
    }
}
