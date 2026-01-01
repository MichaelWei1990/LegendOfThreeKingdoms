using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Skills;

namespace LegendOfThreeKingdoms.Core.Rules;

/// <summary>
/// Default implementation of basic response rules for Jink and Peach.
/// </summary>
public sealed class ResponseRuleService : IResponseRuleService
{
    private readonly SkillManager? _skillManager;

    /// <summary>
    /// Creates a new ResponseRuleService.
    /// </summary>
    /// <param name="skillManager">Optional skill manager for card conversion.</param>
    public ResponseRuleService(SkillManager? skillManager = null)
    {
        _skillManager = skillManager;
    }

    /// <inheritdoc />
    public RuleResult CanRespondWithCard(ResponseContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var responder = context.Responder;
        if (!responder.IsAlive)
        {
            return RuleResult.Disallowed(RuleErrorCode.ResponseNotAllowed);
        }

        var legalCards = GetLegalResponseCards(context);
        return legalCards.HasAny
            ? RuleResult.Allowed
            : RuleResult.Disallowed(RuleErrorCode.NoLegalOptions);
    }

    /// <inheritdoc />
    public RuleQueryResult<Card> GetLegalResponseCards(ResponseContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var responder = context.Responder;
        var handCards = responder.HandZone.Cards;

        // Get expected card subtype for this response type
        var expectedCardSubType = GetExpectedCardSubTypeForResponse(context.ResponseType);
        if (!expectedCardSubType.HasValue)
        {
            return RuleQueryResult<Card>.Empty(RuleErrorCode.NoLegalOptions);
        }

        // Get direct legal cards (cards that already match the expected type)
        var directCards = handCards
            .Where(c => c.CardSubType == expectedCardSubType.Value)
            .ToList();

        // Get convertible cards (cards that can be converted to the expected type via conversion skills)
        var convertibleCards = _skillManager is not null
            ? GetConvertibleCards(context.Game, responder, expectedCardSubType.Value)
            : new List<Card>();

        // Merge direct and convertible cards, avoiding duplicates
        var result = new List<Card>(directCards);
        foreach (var card in convertibleCards)
        {
            if (!result.Any(c => c.Id == card.Id))
            {
                result.Add(card);
            }
        }

        if (result.Count == 0)
        {
            return RuleQueryResult<Card>.Empty(RuleErrorCode.NoLegalOptions);
        }

        return RuleQueryResult<Card>.FromItems(result);
    }

    /// <summary>
    /// Gets the expected card subtype for a given response type.
    /// This maps response types to the card types that can be used to respond.
    /// </summary>
    /// <param name="responseType">The response type.</param>
    /// <returns>The expected card subtype, or null if the response type doesn't require a specific card type.</returns>
    private static CardSubType? GetExpectedCardSubTypeForResponse(ResponseType responseType)
    {
        return responseType switch
        {
            ResponseType.JinkAgainstSlash => CardSubType.Dodge,
            ResponseType.JinkAgainstWanjianqifa => CardSubType.Dodge,
            ResponseType.PeachForDying => CardSubType.Peach,
            ResponseType.SlashAgainstNanmanRushin => CardSubType.Slash,
            ResponseType.SlashAgainstDuel => CardSubType.Slash,
            ResponseType.Nullification => CardSubType.Wuxiekeji,
            _ => null
        };
    }

    /// <summary>
    /// Gets cards that can be converted to the target card subtype via conversion skills.
    /// Note: This method is kept for backward compatibility but delegates to CardConversionService.
    /// In the future, ResponseRuleService should be refactored to accept CardConversionService via dependency injection.
    /// </summary>
    private List<Card> GetConvertibleCards(Game game, Player player, CardSubType targetSubType)
    {
        if (_skillManager is null)
            return new List<Card>();

        // Create a temporary CardConversionService for this operation
        // TODO: Refactor ResponseRuleService to accept CardConversionService via dependency injection
        var cardUsageRules = new CardUsageRuleService(
            new PhaseRuleService(),
            new RangeRuleService(),
            new LimitRuleService());
        var conversionService = new CardConversionService(_skillManager, cardUsageRules);
        
        return conversionService.GetConvertibleCards(game, player, targetSubType);
    }
}
