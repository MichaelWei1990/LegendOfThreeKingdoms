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
            TargetSelectionType.PeachTargets => GetPeachTargets(game, source, card),
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
            CardSubType.Peach => TargetSelectionType.PeachTargets,
            CardSubType.JieDaoShaRen => TargetSelectionType.SingleOtherNoDistance, // Basic type, actual validation in resolver
            _ => TargetSelectionType.None
        };
    }

    /// <summary>
    /// Gets legal targets for cards that require a single other target within attack range.
    /// </summary>
    private RuleQueryResult<Player> GetSingleOtherTargetWithRange(Game game, Player source, Card card)
    {
        // Check if source has Qicai skill and card is a trick card - if so, ignore distance restriction
        var ignoreDistance = ShouldIgnoreDistanceForTrickCard(game, source, card);

        var legalTargets = game.Players
            .Where(p => p.IsAlive 
                && p.Seat != source.Seat 
                && (ignoreDistance || _rangeRules.IsWithinAttackRange(game, source, p)))
            .ToArray();

        // Apply target filtering skills (e.g., Empty City for Slash)
        legalTargets = ApplyTargetFilteringSkills(game, card, legalTargets).ToArray();

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
        // Check if source has Qicai skill and card is a trick card - if so, ignore distance restriction
        var ignoreDistance = ShouldIgnoreDistanceForTrickCard(game, source, card);

        var legalTargets = game.Players
            .Where(p => p.IsAlive 
                && p.Seat != source.Seat 
                && (ignoreDistance || _rangeRules.GetSeatDistance(game, source, p) <= 1))
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
    /// Gets legal targets for Peach card.
    /// Peach can target:
    /// 1. Injured self (CurrentHealth < MaxHealth)
    /// 2. Any character in dying state (CurrentHealth <= 0)
    /// Rule: Cannot use Peach on self if no health loss (CurrentHealth >= MaxHealth)
    /// </summary>
    private RuleQueryResult<Player> GetPeachTargets(Game game, Player source, Card card)
    {
        var legalTargets = new List<Player>();

        // Add injured self (CurrentHealth < MaxHealth)
        // Rule: Cannot use Peach on self if no health loss
        if (source.IsAlive && source.CurrentHealth < source.MaxHealth)
        {
            legalTargets.Add(source);
        }

        // Add all characters in dying state (CurrentHealth <= 0)
        // Rule: Peach can be used on any character in dying state
        // Note: In dying state, we only check CurrentHealth <= 0, not IsAlive
        foreach (var player in game.Players)
        {
            if (player.CurrentHealth <= 0 && !legalTargets.Contains(player))
            {
                legalTargets.Add(player);
            }
        }

        // Apply target filtering skills if any
        var filteredTargets = ApplyTargetFilteringSkills(game, card, legalTargets).ToArray();

        // For Peach, MinTargets is 0, so even if no targets are available, we allow using it on self
        // But we still return the legal targets for selection
        return RuleQueryResult<Player>.FromItems(filteredTargets);
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

    /// <summary>
    /// Checks if distance restriction should be ignored for a trick card due to Qicai skill.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="source">The player using the card.</param>
    /// <param name="card">The card being used.</param>
    /// <returns>True if distance should be ignored (source has Qicai and card is a trick), false otherwise.</returns>
    private bool ShouldIgnoreDistanceForTrickCard(Game game, Player source, Card card)
    {
        // Only applies to trick cards
        if (card.CardType != CardType.Trick)
            return false;

        // Check if source has Qicai skill
        if (_skillManager is null)
            return false;

        var skills = _skillManager.GetActiveSkills(game, source);
        return skills.Any(skill => skill.Id == "qicai");
    }
}
