using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Skills;

namespace LegendOfThreeKingdoms.Core.Rules;

/// <summary>
/// Service for discovering and managing card conversions via conversion skills.
/// This service provides unified card conversion logic used by both ActionQueryService and ResponseRuleService.
/// </summary>
public sealed class CardConversionService
{
    private readonly SkillManager? _skillManager;
    private readonly ICardUsageRuleService _cardUsageRules;

    /// <summary>
    /// Creates a new CardConversionService.
    /// </summary>
    /// <param name="skillManager">Optional skill manager for accessing conversion skills.</param>
    /// <param name="cardUsageRules">The card usage rule service for validating converted cards.</param>
    public CardConversionService(SkillManager? skillManager, ICardUsageRuleService cardUsageRules)
    {
        _skillManager = skillManager;
        _cardUsageRules = cardUsageRules ?? throw new ArgumentNullException(nameof(cardUsageRules));
    }

    /// <summary>
    /// Discovers all card types that can be converted to via conversion skills.
    /// Returns a dictionary mapping target card subtypes to lists of source cards that can be converted.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="player">The player whose cards are being checked.</param>
    /// <returns>A dictionary mapping target card subtypes to lists of convertible source cards.</returns>
    public Dictionary<CardSubType, List<Card>> DiscoverConversionTargets(Game game, Player player)
    {
        var conversionTargets = new Dictionary<CardSubType, List<Card>>();
        
        var conversionSkills = GetConversionSkills(game, player);
        if (conversionSkills.Count == 0)
            return conversionTargets;
        
        // Discover conversions from hand zone
        DiscoverConversionsFromZone(
            game, 
            player, 
            player.HandZone.Cards, 
            conversionSkills, 
            conversionTargets);
        
        // Discover conversions from equipment zone (for skills like Wusheng)
        DiscoverConversionsFromZone(
            game, 
            player, 
            player.EquipmentZone.Cards, 
            conversionSkills, 
            conversionTargets);
        
        return conversionTargets;
    }

    /// <summary>
    /// Gets cards that can be converted to the target card subtype via conversion skills.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="player">The player whose cards are being checked.</param>
    /// <param name="targetSubType">The target card subtype to convert to.</param>
    /// <returns>A list of cards that can be converted to the target subtype.</returns>
    public List<Card> GetConvertibleCards(Game game, Player player, CardSubType targetSubType)
    {
        var convertibleCards = new List<Card>();
        
        if (_skillManager is null)
            return convertibleCards;

        var conversionSkills = GetConversionSkills(game, player);
        if (conversionSkills.Count == 0)
            return convertibleCards;

        // Try to convert each hand card
        foreach (var card in player.HandZone.Cards)
        {
            // Skip if card is already of the target type
            if (card.CardSubType == targetSubType)
                continue;

            // Try each conversion skill
            foreach (var conversionSkill in conversionSkills)
            {
                var virtualCard = conversionSkill.CreateVirtualCard(card, game, player);
                if (virtualCard is not null && virtualCard.CardSubType == targetSubType)
                {
                    // This card can be converted to the target type
                    if (!convertibleCards.Any(c => c.Id == card.Id))
                    {
                        convertibleCards.Add(card);
                    }
                    // Only need one successful conversion per card
                    break;
                }
            }
        }

        // Also try to convert equipment cards (for skills like Wusheng)
        foreach (var card in player.EquipmentZone.Cards)
        {
            // Skip if card is already of the target type
            if (card.CardSubType == targetSubType)
                continue;

            // Try each conversion skill
            foreach (var conversionSkill in conversionSkills)
            {
                var virtualCard = conversionSkill.CreateVirtualCard(card, game, player);
                if (virtualCard is not null && virtualCard.CardSubType == targetSubType)
                {
                    // This card can be converted to the target type
                    if (!convertibleCards.Any(c => c.Id == card.Id))
                    {
                        convertibleCards.Add(card);
                    }
                    // Only need one successful conversion per card
                    break;
                }
            }
        }

        return convertibleCards;
    }

    /// <summary>
    /// Tries to find a valid conversion for a card using available conversion skills.
    /// Returns the target subtype if a valid conversion is found, null otherwise.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="player">The player whose card is being checked.</param>
    /// <param name="card">The card to check for conversion.</param>
    /// <param name="conversionSkills">The list of conversion skills to try.</param>
    /// <returns>The target subtype if a valid conversion is found, null otherwise.</returns>
    public ConversionResult? TryFindValidConversion(
        Game game,
        Player player,
        Card card,
        List<ICardConversionSkill> conversionSkills)
    {
        foreach (var conversionSkill in conversionSkills)
        {
            var virtualCard = conversionSkill.CreateVirtualCard(card, game, player);
            if (virtualCard is null)
                continue;
            
            var targetSubType = virtualCard.CardSubType;
            
            // Skip if the card is already of the target type
            if (card.CardSubType == targetSubType)
                continue;
            
            // Check if the virtual card can be used
            if (!CanUseConvertedCard(game, player, virtualCard))
                continue;

            // Check if there are legal targets (if required)
            if (!HasLegalTargets(game, player, virtualCard, targetSubType))
                continue;

            // Found a valid conversion
            return new ConversionResult(targetSubType);
        }
        
        return null;
    }

    /// <summary>
    /// Checks if a converted virtual card can be used.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="player">The player who would use the card.</param>
    /// <param name="virtualCard">The virtual card to check.</param>
    /// <returns>True if the card can be used, false otherwise.</returns>
    public bool CanUseConvertedCard(Game game, Player player, Card virtualCard)
    {
        var usage = new CardUsageContext(
            game,
            player,
            virtualCard,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 0);
        
        return _cardUsageRules.CanUseCard(usage).IsAllowed;
    }

    /// <summary>
    /// Gets all active conversion skills for the player.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="player">The player whose skills are being checked.</param>
    /// <returns>A list of active conversion skills.</returns>
    private List<ICardConversionSkill> GetConversionSkills(Game game, Player player)
    {
        if (_skillManager is null)
            return new List<ICardConversionSkill>();
        
        return _skillManager.GetActiveSkills(game, player)
            .OfType<ICardConversionSkill>()
            .ToList();
    }

    /// <summary>
    /// Discovers conversion candidates from a specific zone (hand or equipment).
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="player">The player whose cards are being checked.</param>
    /// <param name="cards">The cards in the zone to check.</param>
    /// <param name="conversionSkills">The list of conversion skills to try.</param>
    /// <param name="conversionTargets">The dictionary to add conversion results to.</param>
    private void DiscoverConversionsFromZone(
        Game game,
        Player player,
        IEnumerable<Card> cards,
        List<ICardConversionSkill> conversionSkills,
        Dictionary<CardSubType, List<Card>> conversionTargets)
    {
        foreach (var card in cards)
        {
            var converted = TryFindValidConversion(
                game, 
                player, 
                card, 
                conversionSkills);
            
            if (converted is not null)
            {
                AddConversionCandidate(
                    conversionTargets, 
                    converted.TargetSubType, 
                    card);
            }
        }
    }

    /// <summary>
    /// Checks if a converted card has legal targets (if targets are required).
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="player">The player who would use the card.</param>
    /// <param name="virtualCard">The virtual card to check.</param>
    /// <param name="targetSubType">The target card subtype.</param>
    /// <returns>True if the card has legal targets or doesn't require targets, false otherwise.</returns>
    private bool HasLegalTargets(Game game, Player player, Card virtualCard, CardSubType targetSubType)
    {
        // This will be implemented using TargetConstraintsFactory later
        // For now, we'll use a simple check
        var usage = new CardUsageContext(
            game,
            player,
            virtualCard,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 0);
        
        var legalTargetsResult = _cardUsageRules.GetLegalTargets(usage);
        return legalTargetsResult.HasAny;
    }

    /// <summary>
    /// Adds a card to the conversion candidates for a target subtype.
    /// Avoids duplicates.
    /// </summary>
    /// <param name="conversionTargets">The dictionary of conversion targets.</param>
    /// <param name="targetSubType">The target card subtype.</param>
    /// <param name="card">The card to add.</param>
    private static void AddConversionCandidate(
        Dictionary<CardSubType, List<Card>> conversionTargets,
        CardSubType targetSubType,
        Card card)
    {
        if (!conversionTargets.TryGetValue(targetSubType, out var candidates))
        {
            candidates = new List<Card>();
            conversionTargets[targetSubType] = candidates;
        }
        
        // Avoid duplicates
        if (!candidates.Any(c => c.Id == card.Id))
        {
            candidates.Add(card);
        }
    }

    /// <summary>
    /// Result of a conversion attempt.
    /// </summary>
    public record ConversionResult(CardSubType TargetSubType);
}
