using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Logging;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Service for handling cleanup after card conversion.
/// Handles moving original cards to discard pile after resolution.
/// </summary>
public sealed class CardConversionCleanupService
{
    /// <summary>
    /// Determines if cleanup is needed for the given conversion result and card type.
    /// </summary>
    public bool NeedsCleanup(CardConversionResult conversionResult, CardType actualCardType)
    {
        if (!conversionResult.IsConversion)
            return false;

        // Equipment cards need cleanup after EquipResolver moves them to equipment zone
        if (actualCardType == CardType.Equip)
        {
            // For equipment, we need to clean up original cards after equipping
            return conversionResult.OriginalCard is not null || conversionResult.OriginalCards is not null;
        }

        // For other card types, cards should have been moved before resolution
        // Only need cleanup if something went wrong
        return false;
    }

    /// <summary>
    /// Creates a cleanup resolver for the given conversion result.
    /// </summary>
    public IResolver? CreateCleanupResolver(CardConversionResult conversionResult)
    {
        if (!conversionResult.IsConversion)
            return null;

        // Single-card conversion cleanup
        if (conversionResult.OriginalCard is not null)
        {
            return new CardConversionCleanupResolver(conversionResult.OriginalCard);
        }

        // Multi-card conversion cleanup
        if (conversionResult.OriginalCards is not null && conversionResult.OriginalCards.Count > 0)
        {
            return new MultiCardConversionCleanupResolver(conversionResult.OriginalCards);
        }

        return null;
    }
}

/// <summary>
/// Resolver for cleaning up multi-card conversion (moving original cards to discard pile).
/// </summary>
internal sealed class MultiCardConversionCleanupResolver : IResolver
{
    private readonly IReadOnlyList<Card> _originalCards;

    /// <summary>
    /// Creates a new MultiCardConversionCleanupResolver.
    /// </summary>
    public MultiCardConversionCleanupResolver(IReadOnlyList<Card> originalCards)
    {
        _originalCards = originalCards ?? throw new ArgumentNullException(nameof(originalCards));
        if (originalCards.Count == 0)
            throw new ArgumentException("Original cards list cannot be empty.", nameof(originalCards));
    }

    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var game = context.Game;
        var sourcePlayer = context.SourcePlayer;

        // Find original cards in the source player's hand or equipment zone
        var cardsToDiscard = new List<Card>();
        foreach (var originalCard in _originalCards)
        {
            // Check hand zone first
            var cardInHand = sourcePlayer.HandZone.Cards?.FirstOrDefault(c => c.Id == originalCard.Id);
            if (cardInHand is not null)
            {
                cardsToDiscard.Add(cardInHand);
                continue;
            }

            // Check equipment zone (for equipment conversions)
            var cardInEquipment = sourcePlayer.EquipmentZone?.Cards?.FirstOrDefault(c => c.Id == originalCard.Id);
            if (cardInEquipment is not null)
            {
                cardsToDiscard.Add(cardInEquipment);
            }
        }

        if (cardsToDiscard.Count == 0)
        {
            // Cards might have already been moved (shouldn't happen, but handle gracefully)
            return ResolutionResult.SuccessResult;
        }

        try
        {
            // Move cards from hand to discard pile
            var handCards = cardsToDiscard.Where(c => 
                sourcePlayer.HandZone.Cards?.Any(h => h.Id == c.Id) == true).ToList();
            if (handCards.Count > 0)
            {
                context.CardMoveService.DiscardFromHand(game, sourcePlayer, handCards);
            }

            // Move cards from equipment zone to discard pile
            var equipmentCards = cardsToDiscard.Where(c => 
                sourcePlayer.EquipmentZone?.Cards?.Any(e => e.Id == c.Id) == true).ToList();
            if (equipmentCards.Count > 0 && sourcePlayer.EquipmentZone is Zone equipmentZone)
            {
                var moveDescriptor = new CardMoveDescriptor(
                    SourceZone: equipmentZone,
                    TargetZone: game.DiscardPile,
                    Cards: equipmentCards,
                    Reason: CardMoveReason.Discard,
                    Ordering: CardMoveOrdering.ToTop,
                    Game: game
                );
                context.CardMoveService.MoveMany(moveDescriptor);
            }

            // Log the cleanup if log sink is available
            if (context.LogSink is not null)
            {
                var logEntry = new LogEntry
                {
                    EventType = "MultiCardConversionCleanup",
                    Level = "Info",
                    Message = $"Player {sourcePlayer.Seat} discarded {cardsToDiscard.Count} original cards after multi-card conversion",
                    Data = new
                    {
                        SourcePlayerSeat = sourcePlayer.Seat,
                        CardIds = cardsToDiscard.Select(c => c.Id).ToList(),
                        CardCount = cardsToDiscard.Count
                    }
                };
                context.LogSink.Log(logEntry);
            }
        }
        catch (Exception ex)
        {
            // Log error but don't fail resolution
            if (context.LogSink is not null)
            {
                var logEntry = new LogEntry
                {
                    EventType = "MultiCardConversionCleanupFailed",
                    Level = "Error",
                    Message = $"Failed to cleanup multi-card conversion: {ex.Message}",
                    Data = new
                    {
                        SourcePlayerSeat = sourcePlayer.Seat,
                        Exception = ex.Message
                    }
                };
                context.LogSink.Log(logEntry);
            }
        }

        return ResolutionResult.SuccessResult;
    }
}

