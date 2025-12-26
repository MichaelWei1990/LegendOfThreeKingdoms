using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Skills;

namespace LegendOfThreeKingdoms.Core.Rules;

/// <summary>
/// Service for determining legal targets for card usage based on target selection types.
/// </summary>
public sealed class TargetSelectionService
{
    private readonly IRangeRuleService _rangeRules;
    private readonly SkillManager? _skillManager;

    /// <summary>
    /// Creates a new TargetSelectionService.
    /// </summary>
    /// <param name="rangeRules">The range rule service for calculating distances and attack ranges.</param>
    /// <param name="skillManager">Optional skill manager for applying target filtering skills.</param>
    public TargetSelectionService(
        IRangeRuleService rangeRules,
        SkillManager? skillManager = null)
    {
        _rangeRules = rangeRules ?? throw new ArgumentNullException(nameof(rangeRules));
        _skillManager = skillManager;
    }

    /// <summary>
    /// Gets legal targets for a card based on its target selection type.
    /// </summary>
    /// <param name="context">The card usage context.</param>
    /// <returns>The legal targets for the card.</returns>
    public RuleQueryResult<Player> GetLegalTargets(CardUsageContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var game = context.Game;
        var source = context.SourcePlayer;
        var card = context.Card;

        // Get target selection type for this card
        var targetSelectionType = GetTargetSelectionType(card.CardSubType);
        
        return targetSelectionType switch
        {
            TargetSelectionType.SingleOtherWithRange => GetSingleOtherTargetWithRange(game, source, card),
            TargetSelectionType.SingleOtherWithDistance1 => GetSingleOtherTargetWithDistance1(game, source, card),
            TargetSelectionType.SingleOtherNoDistance => GetSingleOtherTargetNoDistance(game, source, card),
            TargetSelectionType.AllOther => GetAllOtherTargets(game, source),
            TargetSelectionType.Self => GetSelfTarget(game, source),
            TargetSelectionType.None => RuleQueryResult<Player>.Empty(RuleErrorCode.NoLegalOptions),
            _ => RuleQueryResult<Player>.Empty(RuleErrorCode.NoLegalOptions)
        };
    }

    /// <summary>
    /// Gets the target selection type for a card subtype.
    /// </summary>
    private static TargetSelectionType GetTargetSelectionType(CardSubType cardSubType)
    {
        return cardSubType switch
        {
            CardSubType.Slash => TargetSelectionType.SingleOtherWithRange,
            CardSubType.ShunshouQianyang => TargetSelectionType.SingleOtherWithDistance1,
            CardSubType.GuoheChaiqiao => TargetSelectionType.SingleOtherNoDistance,
            CardSubType.Lebusishu => TargetSelectionType.SingleOtherNoDistance,
            CardSubType.Duel => TargetSelectionType.SingleOtherNoDistance,
            CardSubType.WanjianQifa => TargetSelectionType.AllOther,
            CardSubType.NanmanRushin => TargetSelectionType.AllOther,
            CardSubType.Shandian => TargetSelectionType.Self,
            _ => TargetSelectionType.None
        };
    }

    /// <summary>
    /// Gets legal targets for cards that require a single other target within attack range.
    /// </summary>
    private RuleQueryResult<Player> GetSingleOtherTargetWithRange(Game game, Player source, Card card)
    {
        var legalTargets = game.Players
            .Where(p => p.IsAlive && p.Seat != source.Seat && _rangeRules.IsWithinAttackRange(game, source, p))
            .ToArray();

        if (legalTargets.Length == 0)
        {
            return RuleQueryResult<Player>.Empty(RuleErrorCode.NoLegalOptions);
        }

        return RuleQueryResult<Player>.FromItems(legalTargets);
    }

    /// <summary>
    /// Gets legal targets for cards that require a single other target within distance 1.
    /// </summary>
    private RuleQueryResult<Player> GetSingleOtherTargetWithDistance1(Game game, Player source, Card card)
    {
        var legalTargets = game.Players
            .Where(p => p.IsAlive 
                && p.Seat != source.Seat 
                && _rangeRules.GetSeatDistance(game, source, p) <= 1)
            .ToArray();

        // Apply target filtering skills (e.g., Modesty)
        legalTargets = ApplyTargetFilteringSkills(game, card, legalTargets).ToArray();

        if (legalTargets.Length == 0)
        {
            return RuleQueryResult<Player>.Empty(RuleErrorCode.NoLegalOptions);
        }

        return RuleQueryResult<Player>.FromItems(legalTargets);
    }

    /// <summary>
    /// Gets legal targets for cards that require a single other target with no distance restriction.
    /// </summary>
    private RuleQueryResult<Player> GetSingleOtherTargetNoDistance(Game game, Player source, Card card)
    {
        var legalTargets = game.Players
            .Where(p => p.IsAlive && p.Seat != source.Seat)
            .ToArray();

        // Apply target filtering skills for specific cards (e.g., Modesty for Lebusishu, Duel)
        if (card.CardSubType == CardSubType.Lebusishu || card.CardSubType == CardSubType.Duel)
        {
            legalTargets = ApplyTargetFilteringSkills(game, card, legalTargets).ToArray();
        }

        if (legalTargets.Length == 0)
        {
            return RuleQueryResult<Player>.Empty(RuleErrorCode.NoLegalOptions);
        }

        return RuleQueryResult<Player>.FromItems(legalTargets);
    }

    /// <summary>
    /// Gets legal targets for cards that target all other players (no target selection needed).
    /// </summary>
    private RuleQueryResult<Player> GetAllOtherTargets(Game game, Player source)
    {
        var legalTargets = game.Players
            .Where(p => p.IsAlive && p.Seat != source.Seat)
            .ToArray();

        if (legalTargets.Length == 0)
        {
            return RuleQueryResult<Player>.Empty(RuleErrorCode.NoLegalOptions);
        }

        // Return empty list since no target selection is needed (all targets are automatically selected)
        // But we validate that at least one target exists above
        return RuleQueryResult<Player>.FromItems(Array.Empty<Player>());
    }

    /// <summary>
    /// Gets legal targets for cards that target self.
    /// </summary>
    private RuleQueryResult<Player> GetSelfTarget(Game game, Player source)
    {
        var legalTargets = game.Players
            .Where(p => p.IsAlive && p.Seat == source.Seat)
            .ToArray();

        if (legalTargets.Length == 0)
        {
            return RuleQueryResult<Player>.Empty(RuleErrorCode.NoLegalOptions);
        }

        return RuleQueryResult<Player>.FromItems(legalTargets);
    }

    /// <summary>
    /// Applies target filtering skills from all players to filter out excluded targets.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="card">The card being used.</param>
    /// <param name="potentialTargets">The list of potential targets before filtering.</param>
    /// <returns>The filtered list of targets after applying all target filtering skills.</returns>
    private IEnumerable<Player> ApplyTargetFilteringSkills(Game game, Card card, IEnumerable<Player> potentialTargets)
    {
        if (_skillManager is null)
        {
            // No skill manager available, return targets as-is
            return potentialTargets;
        }

        var filteredTargets = potentialTargets.ToList();

        // Check each player's target filtering skills
        foreach (var player in game.Players)
        {
            var skills = _skillManager.GetActiveSkills(game, player);
            foreach (var skill in skills)
            {
                if (skill is ITargetFilteringSkill targetFilteringSkill)
                {
                    // Remove targets that should be excluded by this skill
                    filteredTargets.RemoveAll(target => 
                        targetFilteringSkill.ShouldExcludeTarget(game, player, card, target));
                }
            }
        }

        return filteredTargets;
    }
}
