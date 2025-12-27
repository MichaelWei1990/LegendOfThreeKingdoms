using System;
using System.Linq;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Jianxiong (奸雄) skill: Trigger skill that obtains the card that caused damage after taking damage.
/// </summary>
public sealed class JianxiongSkill : BaseSkill, IDamageResolvedSkill
{
    private Game? _game;
    private Player? _owner;
    private IEventBus? _eventBus;
    private ICardMoveService? _cardMoveService;

    /// <inheritdoc />
    public override string Id => "jianxiong";

    /// <inheritdoc />
    public override string Name => "奸雄";

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

        eventBus.Subscribe<DamageResolvedEvent>(OnDamageResolved);
    }

    /// <inheritdoc />
    public override void Detach(Game game, Player owner, IEventBus eventBus)
    {
        if (eventBus is null)
            return;

        eventBus.Unsubscribe<DamageResolvedEvent>(OnDamageResolved);

        _game = null;
        _owner = null;
        _eventBus = null;
        _cardMoveService = null;
    }

    /// <inheritdoc />
    public void OnDamageResolved(DamageResolvedEvent evt)
    {
        if (_game is null || _owner is null || _cardMoveService is null)
            return;

        // Only process for the owner (target of damage)
        if (evt.Damage.TargetSeat != _owner.Seat)
            return;

        // Check if skill is active
        if (!IsActive(_game, _owner))
            return;

        // Check if there is a causing card
        var causingCard = evt.Damage.CausingCard;
        if (causingCard is null)
            return;

        // Core v1 convention: automatically trigger (no player choice)
        // Try to obtain the card if it's in an obtainable zone
        if (IsCardObtainable(_game, causingCard))
        {
            try
            {
                MoveCardToHand(_game, _owner, causingCard, _cardMoveService);
            }
            catch (Exception)
            {
                // If moving fails (e.g., card no longer in expected zone), silently ignore
                // This matches the behavior of other trigger skills
            }
        }
    }

    /// <summary>
    /// Checks if a card is in an obtainable zone.
    /// Obtainable zones: DiscardPile only (v1 implementation).
    /// According to requirements, cards in other players' zones or draw pile are not obtainable.
    /// Cards in resolution/in-play zones are theoretically obtainable, but we use a conservative
    /// approach: only obtain from discard pile to avoid edge cases.
    /// </summary>
    private static bool IsCardObtainable(Game game, Card card)
    {
        // Only obtainable from discard pile (v1 implementation)
        // This is the most common case and avoids complexity with resolution zones
        if (game.DiscardPile.Cards.Contains(card))
            return true;

        // Check if card is in a non-obtainable zone
        // (hand, equipment, or judgement zones of any player are not obtainable)
        foreach (var player in game.Players)
        {
            if (player.HandZone.Cards.Contains(card))
                return false; // Card is in a player's hand
            if (player.EquipmentZone.Cards.Contains(card))
                return false; // Card is in a player's equipment zone
            if (player.JudgementZone.Cards.Contains(card))
                return false; // Card is in a player's judgement zone
        }

        // Check if it's in draw pile (not obtainable for Jianxiong)
        if (game.DrawPile.Cards.Contains(card))
            return false; // Draw pile is not considered obtainable for Jianxiong

        // Card is not in any known zone - might be in resolution/in-play zone
        // For v1, we use conservative approach: only obtain from discard pile
        // This ensures we don't try to obtain cards that have been moved to unknown locations
        return false;
    }

    /// <summary>
    /// Moves a card to the owner's hand zone.
    /// </summary>
    private static void MoveCardToHand(Game game, Player owner, Card card, ICardMoveService cardMoveService)
    {
        // Find the source zone containing the card
        IZone? sourceZone = null;

        // Check discard pile first (most common case)
        if (game.DiscardPile.Cards.Contains(card))
        {
            sourceZone = game.DiscardPile;
        }
        else
        {
            // Search all zones for the card
            // This handles cases where the card might be in a resolution/in-play zone
            foreach (var player in game.Players)
            {
                if (player.HandZone.Cards.Contains(card))
                {
                    sourceZone = player.HandZone;
                    break;
                }
                if (player.EquipmentZone.Cards.Contains(card))
                {
                    sourceZone = player.EquipmentZone;
                    break;
                }
                if (player.JudgementZone.Cards.Contains(card))
                {
                    sourceZone = player.JudgementZone;
                    break;
                }
            }
        }

        // If source zone not found, cannot move
        if (sourceZone is null)
            return;

        // Ensure target hand zone is valid
        if (owner.HandZone is not Zone targetHandZone)
            return;

        // Move the card to owner's hand
        var moveDescriptor = new CardMoveDescriptor(
            SourceZone: sourceZone,
            TargetZone: targetHandZone,
            Cards: new[] { card },
            Reason: CardMoveReason.Draw, // Using Draw reason for obtaining cards
            Ordering: CardMoveOrdering.ToTop,
            Game: game
        );

        cardMoveService.MoveMany(moveDescriptor);
    }
}

/// <summary>
/// Factory for creating JianxiongSkill instances.
/// </summary>
public sealed class JianxiongSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new JianxiongSkill();
    }
}
