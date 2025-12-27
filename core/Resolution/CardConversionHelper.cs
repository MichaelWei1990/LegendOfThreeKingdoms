using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Helper class for resolving card conversions before passing to resolvers.
/// This allows resolvers to focus on processing cards without knowing about conversion logic.
/// </summary>
internal static class CardConversionHelper
{
    /// <summary>
    /// Resolves the actual card to use based on the action and selected card.
    /// If the selected card is in the action's CardCandidates, it can be used directly.
    /// Otherwise, tries all conversion skills to see if the card can be converted to match the action.
    /// </summary>
    /// <param name="action">The action descriptor.</param>
    /// <param name="selectedCard">The card selected by the player.</param>
    /// <param name="game">The current game state.</param>
    /// <param name="sourcePlayer">The player using the card.</param>
    /// <param name="skillManager">The skill manager for checking conversion skills.</param>
    /// <returns>
    /// A tuple containing:
    /// - actualCard: The card to use for resolution (may be virtual)
    /// - originalCard: The original physical card (null if no conversion)
    /// - conversionSkill: The skill that performed the conversion (null if no conversion)
    /// </returns>
    public static (Card actualCard, Card? originalCard, ICardConversionSkill? conversionSkill) ResolveCardForAction(
        ActionDescriptor action,
        Card selectedCard,
        Game game,
        Player sourcePlayer,
        SkillManager? skillManager)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));
        if (selectedCard is null) throw new ArgumentNullException(nameof(selectedCard));
        if (game is null) throw new ArgumentNullException(nameof(game));
        if (sourcePlayer is null) throw new ArgumentNullException(nameof(sourcePlayer));

        // Determine the expected card type from the action ID
        // This allows us to check if conversion is applicable even when CardCandidates is empty
        CardSubType? expectedCardSubType = GetExpectedCardSubTypeFromActionId(action.ActionId);

        // Try all conversion skills to see if the selected card can be converted to match the action
        if (skillManager is not null)
        {
            var conversionSkills = skillManager.GetActiveSkills(game, sourcePlayer)
                .OfType<ICardConversionSkill>()
                .ToList();

            foreach (var skill in conversionSkills)
            {
                var virtualCard = skill.CreateVirtualCard(selectedCard, game, sourcePlayer);
                if (virtualCard is null)
                    continue;

                // Check if conversion is applicable:
                // 1. If we have an expected card type, check if virtual card matches it
                // 2. Otherwise, check if virtual card's type matches any candidate in the action
                bool isConversionApplicable = false;

                if (expectedCardSubType.HasValue)
                {
                    // Check if virtual card matches the expected type
                    isConversionApplicable = virtualCard.CardSubType == expectedCardSubType.Value &&
                                            selectedCard.CardSubType != expectedCardSubType.Value;
                }
                else if (action.CardCandidates is not null)
                {
                    // Fallback: check if virtual card matches any candidate's type
                    isConversionApplicable = action.CardCandidates.Any(c => 
                        c.CardSubType == virtualCard.CardSubType && 
                        c.Id != selectedCard.Id);
                }

                if (isConversionApplicable)
                {
                    // Conversion successful - the virtual card matches the action's expected type
                    return (virtualCard, selectedCard, skill);
                }
            }
        }

        // If selected card is already in candidates and no conversion was needed, use it directly
        if (action.CardCandidates is not null &&
            action.CardCandidates.Any(c => c.Id == selectedCard.Id))
        {
            return (selectedCard, null, null);
        }

        // No conversion possible - return original card (this may result in an error later)
        return (selectedCard, null, null);
    }

    /// <summary>
    /// Gets the expected card subtype from an action ID.
    /// This is used to determine if a card conversion is applicable for the action.
    /// </summary>
    /// <param name="actionId">The action ID.</param>
    /// <returns>The expected card subtype, or null if not determinable from the action ID.</returns>
    private static CardSubType? GetExpectedCardSubTypeFromActionId(string actionId)
    {
        return actionId switch
        {
            "UseSlash" => CardSubType.Slash,
            "UsePeach" => CardSubType.Peach,
            "UseGuoheChaiqiao" => CardSubType.GuoheChaiqiao,
            "UseWuzhongShengyou" => CardSubType.WuzhongShengyou,
            "UseTaoyuanJieyi" => CardSubType.TaoyuanJieyi,
            "UseShunshouQianyang" => CardSubType.ShunshouQianyang,
            "UseWanjianQifa" => CardSubType.WanjianQifa,
            "UseNanmanRushin" => CardSubType.NanmanRushin,
            "UseDuel" => CardSubType.Duel,
            "UseLebusishu" => CardSubType.Lebusishu,
            "UseShandian" => CardSubType.Shandian,
            // Add more action-to-card mappings as needed
            _ => null
        };
    }

    /// <summary>
    /// Prepares the IntermediateResults dictionary with card conversion information.
    /// </summary>
    /// <param name="actualCard">The card to use for resolution.</param>
    /// <param name="originalCard">The original physical card (null if no conversion).</param>
    /// <param name="conversionSkill">The skill that performed the conversion (null if no conversion).</param>
    /// <param name="existingResults">Existing IntermediateResults dictionary, or null to create a new one.</param>
    /// <returns>The IntermediateResults dictionary with conversion information added.</returns>
    public static Dictionary<string, object> PrepareIntermediateResults(
        Card actualCard,
        Card? originalCard,
        ICardConversionSkill? conversionSkill,
        Dictionary<string, object>? existingResults = null)
    {
        var results = existingResults ?? new Dictionary<string, object>();

        // Store the actual card to use
        results["ActualCard"] = actualCard;

        // Store conversion information if conversion occurred
        if (originalCard is not null && conversionSkill is not null)
        {
            results["ConversionOriginalCard"] = originalCard;
            results["ConversionSkill"] = conversionSkill;
        }

        return results;
    }
}
