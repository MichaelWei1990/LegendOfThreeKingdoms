using System;
using System.Linq;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Tricks;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Jizhi (集智) skill: Trigger skill that draws 1 card when using a non-delayed trick card.
/// </summary>
public sealed class JizhiSkill : BaseSkill
{
    private Game? _game;
    private Player? _owner;
    private IEventBus? _eventBus;
    private ICardMoveService? _cardMoveService;

    /// <inheritdoc />
    public override string Id => "jizhi";

    /// <inheritdoc />
    public override string Name => "集智";

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

        eventBus.Subscribe<CardUsedEvent>(OnCardUsed);
    }

    /// <inheritdoc />
    public override void Detach(Game game, Player owner, IEventBus eventBus)
    {
        if (eventBus is null)
            return;

        eventBus.Unsubscribe<CardUsedEvent>(OnCardUsed);

        _game = null;
        _owner = null;
        _eventBus = null;
        _cardMoveService = null;
    }

    private void OnCardUsed(CardUsedEvent evt)
    {
        if (_game is null || _owner is null || _cardMoveService is null)
            return;

        // Only process for the owner (user of the card)
        if (evt.SourcePlayerSeat != _owner.Seat)
            return;

        // Check if skill is active
        if (!IsActive(_game, _owner))
            return;

        // Check conditions using CardSubType from event:
        // 1. Card must be an immediate trick card (non-delayed trick)
        // 2. Card must NOT be a delayed trick
        
        // First check if it's a delayed trick - if so, don't trigger
        bool isDelayedTrick = IsDelayedTrickSubType(evt.CardSubType);
        if (isDelayedTrick)
            return;
        
        // Then check if it's an immediate trick card
        bool isImmediateTrick = IsImmediateTrickSubType(evt.CardSubType);
        if (!isImmediateTrick)
            return;

        // Core v1 convention: automatically trigger (no player choice)
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

    /// <summary>
    /// Checks if a CardSubType represents an immediate trick card (non-delayed trick).
    /// Uses the same logic as DelayedTrickManager.IsSpecificImmediateTrickSubType.
    /// </summary>
    private static bool IsImmediateTrickSubType(CardSubType subType)
    {
        return subType switch
        {
            CardSubType.ImmediateTrick => true,
            CardSubType.WuzhongShengyou => true,
            CardSubType.TaoyuanJieyi => true,
            CardSubType.ShunshouQianyang => true,
            CardSubType.GuoheChaiqiao => true,
            CardSubType.WanjianQifa => true,
            CardSubType.NanmanRushin => true,
            CardSubType.Duel => true,
            _ => false
        };
    }

    /// <summary>
    /// Checks if a CardSubType represents a delayed trick card.
    /// Uses the same logic as DelayedTrickManager.IsSpecificDelayedTrickSubType.
    /// </summary>
    private static bool IsDelayedTrickSubType(CardSubType subType)
    {
        return subType switch
        {
            CardSubType.DelayedTrick => true,
            CardSubType.Lebusishu => true,
            CardSubType.Shandian => true,
            _ => false
        };
    }
}

/// <summary>
/// Factory for creating JizhiSkill instances.
/// </summary>
public sealed class JizhiSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new JizhiSkill();
    }
}
