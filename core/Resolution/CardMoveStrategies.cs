using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Result of a card move operation.
/// </summary>
public sealed record CardMoveResult(
    bool ShouldMove,
    IReadOnlyList<Card>? CardsToMove,
    bool MoveBeforeResolution
);

/// <summary>
/// Strategy interface for card movement operations.
/// </summary>
public interface ICardMoveStrategy
{
    /// <summary>
    /// Determines whether this strategy can handle the given scenario.
    /// </summary>
    bool CanHandle(CardConversionResult conversionResult, Card actualCard, bool isDelayedTrick);

    /// <summary>
    /// Determines what cards should be moved and when.
    /// </summary>
    CardMoveResult DetermineMove(CardConversionResult conversionResult, IReadOnlyList<Card> selectedCards);
}

/// <summary>
/// Strategy for non-conversion card movement (normal card usage).
/// </summary>
public sealed class NoConversionCardMoveStrategy : ICardMoveStrategy
{
    /// <inheritdoc />
    public bool CanHandle(CardConversionResult conversionResult, Card actualCard, bool isDelayedTrick)
    {
        return !conversionResult.IsConversion && 
               actualCard.CardType != CardType.Equip && 
               !isDelayedTrick;
    }

    /// <inheritdoc />
    public CardMoveResult DetermineMove(CardConversionResult conversionResult, IReadOnlyList<Card> selectedCards)
    {
        if (selectedCards.Count == 0)
            return new CardMoveResult(ShouldMove: false, CardsToMove: null, MoveBeforeResolution: false);

        return new CardMoveResult(
            ShouldMove: true,
            CardsToMove: new[] { selectedCards[0] },
            MoveBeforeResolution: true
        );
    }
}

/// <summary>
/// Strategy for single-card conversion movement.
/// </summary>
public sealed class SingleCardConversionMoveStrategy : ICardMoveStrategy
{
    /// <inheritdoc />
    public bool CanHandle(CardConversionResult conversionResult, Card actualCard, bool isDelayedTrick)
    {
        return conversionResult.IsConversion && 
               !conversionResult.IsMultiCardConversion && 
               conversionResult.OriginalCard is not null &&
               actualCard.CardType != CardType.Equip && 
               !isDelayedTrick;
    }

    /// <inheritdoc />
    public CardMoveResult DetermineMove(CardConversionResult conversionResult, IReadOnlyList<Card> selectedCards)
    {
        if (conversionResult.OriginalCard is null)
            return new CardMoveResult(ShouldMove: false, CardsToMove: null, MoveBeforeResolution: false);

        return new CardMoveResult(
            ShouldMove: true,
            CardsToMove: new[] { conversionResult.OriginalCard },
            MoveBeforeResolution: true
        );
    }
}

/// <summary>
/// Strategy for multi-card conversion movement.
/// </summary>
public sealed class MultiCardConversionMoveStrategy : ICardMoveStrategy
{
    /// <inheritdoc />
    public bool CanHandle(CardConversionResult conversionResult, Card actualCard, bool isDelayedTrick)
    {
        return conversionResult.IsMultiCardConversion && 
               conversionResult.OriginalCards is not null &&
               actualCard.CardType != CardType.Equip && 
               !isDelayedTrick;
    }

    /// <inheritdoc />
    public CardMoveResult DetermineMove(CardConversionResult conversionResult, IReadOnlyList<Card> selectedCards)
    {
        if (conversionResult.OriginalCards is null)
            return new CardMoveResult(ShouldMove: false, CardsToMove: null, MoveBeforeResolution: false);

        return new CardMoveResult(
            ShouldMove: true,
            CardsToMove: conversionResult.OriginalCards,
            MoveBeforeResolution: true
        );
    }
}

/// <summary>
/// Strategy for equipment card movement (handled by EquipResolver, not moved here).
/// </summary>
public sealed class EquipmentCardMoveStrategy : ICardMoveStrategy
{
    /// <inheritdoc />
    public bool CanHandle(CardConversionResult conversionResult, Card actualCard, bool isDelayedTrick)
    {
        return actualCard.CardType == CardType.Equip;
    }

    /// <inheritdoc />
    public CardMoveResult DetermineMove(CardConversionResult conversionResult, IReadOnlyList<Card> selectedCards)
    {
        // Equipment cards are moved by EquipResolver, not here
        // For conversions, we need to ensure original cards are still in hand for EquipResolver
        return new CardMoveResult(
            ShouldMove: false,
            CardsToMove: null,
            MoveBeforeResolution: false
        );
    }
}

/// <summary>
/// Strategy for delayed trick card movement (handled by DelayedTrickResolver).
/// </summary>
public sealed class DelayedTrickCardMoveStrategy : ICardMoveStrategy
{
    /// <inheritdoc />
    public bool CanHandle(CardConversionResult conversionResult, Card actualCard, bool isDelayedTrick)
    {
        return isDelayedTrick;
    }

    /// <inheritdoc />
    public CardMoveResult DetermineMove(CardConversionResult conversionResult, IReadOnlyList<Card> selectedCards)
    {
        // Delayed tricks are moved to judgement zone by DelayedTrickResolver, not here
        return new CardMoveResult(
            ShouldMove: false,
            CardsToMove: null,
            MoveBeforeResolution: false
        );
    }
}

/// <summary>
/// Executor for card move strategies.
/// </summary>
public sealed class CardMoveStrategyExecutor
{
    private readonly IReadOnlyList<ICardMoveStrategy> _strategies;

    /// <summary>
    /// Creates a new instance with default strategies.
    /// </summary>
    public CardMoveStrategyExecutor()
        : this(new ICardMoveStrategy[]
        {
            new EquipmentCardMoveStrategy(),      // Check equipment first (highest priority)
            new DelayedTrickCardMoveStrategy(),    // Check delayed tricks second
            new MultiCardConversionMoveStrategy(), // Check multi-card conversion
            new SingleCardConversionMoveStrategy(), // Check single-card conversion
            new NoConversionCardMoveStrategy()     // Default fallback
        })
    {
    }

    /// <summary>
    /// Creates a new instance with custom strategies.
    /// </summary>
    public CardMoveStrategyExecutor(IReadOnlyList<ICardMoveStrategy> strategies)
    {
        _strategies = strategies ?? throw new ArgumentNullException(nameof(strategies));
    }

    /// <summary>
    /// Determines what cards should be moved and when.
    /// </summary>
    public CardMoveResult Execute(CardConversionResult conversionResult, Card actualCard, bool isDelayedTrick, IReadOnlyList<Card> selectedCards)
    {
        foreach (var strategy in _strategies)
        {
            if (strategy.CanHandle(conversionResult, actualCard, isDelayedTrick))
            {
                return strategy.DetermineMove(conversionResult, selectedCards);
            }
        }

        // Default: don't move
        return new CardMoveResult(ShouldMove: false, CardsToMove: null, MoveBeforeResolution: false);
    }
}

