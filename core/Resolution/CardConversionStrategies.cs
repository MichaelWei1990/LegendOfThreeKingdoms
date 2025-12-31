using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Result of a card conversion operation.
/// </summary>
public sealed record CardConversionResult(
    Card ActualCard,
    Card? OriginalCard,
    IReadOnlyList<Card>? OriginalCards,
    bool IsConversion,
    bool IsMultiCardConversion,
    Dictionary<string, object>? UpdatedIntermediateResults
);

/// <summary>
/// Strategy interface for card conversion operations.
/// </summary>
public interface ICardConversionStrategy
{
    /// <summary>
    /// Determines whether this strategy can handle the given selected cards and action.
    /// </summary>
    bool CanHandle(IReadOnlyList<Card> selectedCards, ActionDescriptor action);

    /// <summary>
    /// Attempts to convert the selected cards according to this strategy.
    /// </summary>
    /// <param name="context">The resolution context.</param>
    /// <param name="selectedCards">The cards selected by the player.</param>
    /// <param name="action">The action descriptor.</param>
    /// <returns>The conversion result, or null if conversion is not applicable.</returns>
    CardConversionResult? Convert(ResolutionContext context, IReadOnlyList<Card> selectedCards, ActionDescriptor action);
}

/// <summary>
/// Strategy for handling card conversion from IntermediateResults (already resolved).
/// </summary>
public sealed class PreResolvedConversionStrategy : ICardConversionStrategy
{
    /// <inheritdoc />
    public bool CanHandle(IReadOnlyList<Card> selectedCards, ActionDescriptor action)
    {
        // This strategy handles cases where conversion was already resolved
        return true; // Always check IntermediateResults first
    }

    /// <inheritdoc />
    public CardConversionResult? Convert(ResolutionContext context, IReadOnlyList<Card> selectedCards, ActionDescriptor action)
    {
        if (context.IntermediateResults is null)
            return null;

        // Check if conversion was already resolved by CardConversionHelper
        if (!context.IntermediateResults.TryGetValue("ActualCard", out var actualCardObj) ||
            actualCardObj is not Card resolvedCard)
        {
            return null;
        }

        // Check if single-card conversion occurred
        if (context.IntermediateResults.TryGetValue("ConversionOriginalCard", out var originalCardObj) &&
            originalCardObj is Card original)
        {
            return new CardConversionResult(
                ActualCard: resolvedCard,
                OriginalCard: original,
                OriginalCards: null,
                IsConversion: true,
                IsMultiCardConversion: false,
                UpdatedIntermediateResults: null // No update needed
            );
        }

        // Check if multi-card conversion occurred
        if (context.IntermediateResults.TryGetValue("ConversionOriginalCards", out var originalCardsObj) &&
            originalCardsObj is IReadOnlyList<Card> originals)
        {
            return new CardConversionResult(
                ActualCard: resolvedCard,
                OriginalCard: null,
                OriginalCards: originals,
                IsConversion: true,
                IsMultiCardConversion: true,
                UpdatedIntermediateResults: null // No update needed
            );
        }

        // No conversion found in IntermediateResults
        return null;
    }
}

/// <summary>
/// Strategy for multi-card conversion (e.g., Serpent Spear).
/// </summary>
public sealed class MultiCardConversionStrategy : ICardConversionStrategy
{
    /// <inheritdoc />
    public bool CanHandle(IReadOnlyList<Card> selectedCards, ActionDescriptor action)
    {
        return selectedCards.Count > 1;
    }

    /// <inheritdoc />
    public CardConversionResult? Convert(ResolutionContext context, IReadOnlyList<Card> selectedCards, ActionDescriptor action)
    {
        if (context.SkillManager is null)
            return null;

        var game = context.Game;
        var sourcePlayer = context.SourcePlayer;

        // Determine the expected card type from the action
        var expectedCardSubType = GetExpectedCardSubType(action.ActionId);
        if (!expectedCardSubType.HasValue)
            return null;

        // Try all multi-card conversion skills
        var multiConversionSkills = context.SkillManager.GetActiveSkills(game, sourcePlayer)
            .OfType<Skills.IMultiCardConversionSkill>()
            .ToList();

        foreach (var skill in multiConversionSkills)
        {
            if (selectedCards.Count != skill.RequiredCardCount)
                continue;

            if (skill.TargetCardSubType != expectedCardSubType.Value)
                continue;

            var virtualCard = skill.CreateVirtualCardFromMultiple(selectedCards, game, sourcePlayer);
            if (virtualCard is not null && virtualCard.CardSubType == expectedCardSubType.Value)
            {
                // Store in IntermediateResults for cleanup
                var intermediateResults = context.IntermediateResults ?? new Dictionary<string, object>();
                intermediateResults["ConversionOriginalCards"] = selectedCards;
                intermediateResults["ConversionSkill"] = skill;

                return new CardConversionResult(
                    ActualCard: virtualCard,
                    OriginalCard: null,
                    OriginalCards: selectedCards,
                    IsConversion: true,
                    IsMultiCardConversion: true,
                    UpdatedIntermediateResults: intermediateResults
                );
            }
        }

        return null;
    }

    private static CardSubType? GetExpectedCardSubType(string actionId)
    {
        return actionId switch
        {
            "UseSlash" => CardSubType.Slash,
            "UsePeach" => CardSubType.Peach,
            "UseGuoheChaiqiao" => CardSubType.GuoheChaiqiao,
            // Add more action-to-card mappings here as needed
            _ => null
        };
    }
}

/// <summary>
/// Strategy for single-card conversion (e.g., Qixi).
/// </summary>
public sealed class SingleCardConversionStrategy : ICardConversionStrategy
{
    /// <inheritdoc />
    public bool CanHandle(IReadOnlyList<Card> selectedCards, ActionDescriptor action)
    {
        return selectedCards.Count == 1;
    }

    /// <inheritdoc />
    public CardConversionResult? Convert(ResolutionContext context, IReadOnlyList<Card> selectedCards, ActionDescriptor action)
    {
        if (selectedCards.Count != 1)
            return null;

        var game = context.Game;
        var sourcePlayer = context.SourcePlayer;
        var card = selectedCards[0];

        // Determine the expected card type from the action
        var expectedCardSubType = GetExpectedCardSubType(action.ActionId);
        if (!expectedCardSubType.HasValue)
            return null;

        // If action expects a specific card type and the selected card is not that type, try conversion
        if (card.CardSubType == expectedCardSubType.Value || context.SkillManager is null)
        {
            // No conversion needed
            return new CardConversionResult(
                ActualCard: card,
                OriginalCard: null,
                OriginalCards: null,
                IsConversion: false,
                IsMultiCardConversion: false,
                UpdatedIntermediateResults: null
            );
        }

        // Try single-card conversion skills
        var skills = context.SkillManager.GetActiveSkills(game, sourcePlayer)
            .OfType<Skills.ICardConversionSkill>()
            .ToList();

        foreach (var skill in skills)
        {
            var virtualCard = skill.CreateVirtualCard(card, game, sourcePlayer);
            if (virtualCard is not null && virtualCard.CardSubType == expectedCardSubType.Value)
            {
                // For Wusheng skill with equipment cards, dependency checking will happen during actual conversion
                // For now, we skip dependency check during candidate discovery to avoid complexity
                // Full dependency validation will happen during actual conversion if needed

                // Store original card and conversion skill in IntermediateResults for cleanup
                var intermediateResults = context.IntermediateResults ?? new Dictionary<string, object>();
                intermediateResults["ConversionOriginalCard"] = card;
                intermediateResults["ConversionSkill"] = skill;
                // Mark if card is from equipment zone for cleanup
                var cardIsFromEquipment = sourcePlayer.EquipmentZone.Cards.Any(c => c.Id == card.Id);
                if (cardIsFromEquipment)
                {
                    intermediateResults["ConversionFromEquipment"] = true;
                }

                return new CardConversionResult(
                    ActualCard: virtualCard,
                    OriginalCard: card,
                    OriginalCards: null,
                    IsConversion: true,
                    IsMultiCardConversion: false,
                    UpdatedIntermediateResults: intermediateResults
                );
            }
        }

        // No conversion possible, use original card
        return new CardConversionResult(
            ActualCard: card,
            OriginalCard: null,
            OriginalCards: null,
            IsConversion: false,
            IsMultiCardConversion: false,
            UpdatedIntermediateResults: null
        );
    }

    private static CardSubType? GetExpectedCardSubType(string actionId)
    {
        return actionId switch
        {
            "UseGuoheChaiqiao" => CardSubType.GuoheChaiqiao,
            // Add more action-to-card mappings here as needed
            _ => null
        };
    }
}

/// <summary>
/// Factory for selecting and executing card conversion strategies.
/// </summary>
public sealed class CardConversionStrategyExecutor
{
    private readonly IReadOnlyList<ICardConversionStrategy> _strategies;

    /// <summary>
    /// Creates a new instance with default strategies.
    /// </summary>
    public CardConversionStrategyExecutor()
        : this(new ICardConversionStrategy[]
        {
            new PreResolvedConversionStrategy(), // Check IntermediateResults first
            new MultiCardConversionStrategy(),    // Try multi-card conversion
            new SingleCardConversionStrategy()    // Fall back to single-card conversion
        })
    {
    }

    /// <summary>
    /// Creates a new instance with custom strategies.
    /// </summary>
    public CardConversionStrategyExecutor(IReadOnlyList<ICardConversionStrategy> strategies)
    {
        _strategies = strategies ?? throw new ArgumentNullException(nameof(strategies));
    }

    /// <summary>
    /// Executes card conversion using the appropriate strategy.
    /// </summary>
    public CardConversionResult Execute(ResolutionContext context, IReadOnlyList<Card> selectedCards, ActionDescriptor action)
    {
        if (selectedCards is null || selectedCards.Count == 0)
            throw new ArgumentException("At least one card must be selected.", nameof(selectedCards));

        // Try each strategy in order until one succeeds
        foreach (var strategy in _strategies)
        {
            if (!strategy.CanHandle(selectedCards, action))
                continue;

            var result = strategy.Convert(context, selectedCards, action);
            if (result is not null)
                return result;
        }

        // No conversion possible, use first card as default
        return new CardConversionResult(
            ActualCard: selectedCards[0],
            OriginalCard: null,
            OriginalCards: null,
            IsConversion: false,
            IsMultiCardConversion: false,
            UpdatedIntermediateResults: null
        );
    }
}

