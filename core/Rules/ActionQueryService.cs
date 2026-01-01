using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Skills;

namespace LegendOfThreeKingdoms.Core.Rules;

/// <summary>
/// Default implementation that derives currently available high-level actions for a player.
/// </summary>
public sealed class ActionQueryService : IActionQueryService
{
    private readonly IPhaseRuleService _phaseRules;
    private readonly ICardUsageRuleService _cardUsageRules;
    private readonly Skills.SkillManager? _skillManager;
    private readonly CardConversionService? _cardConversionService;
    private readonly IRuleModifierProvider? _modifierProvider;

    /// <summary>
    /// Creates a new ActionQueryService.
    /// </summary>
    /// <param name="phaseRules">The phase rule service.</param>
    /// <param name="cardUsageRules">The card usage rule service.</param>
    /// <param name="skillManager">Optional skill manager for card conversion and skill-based actions.</param>
    /// <param name="modifierProvider">Optional rule modifier provider for modifying target constraints.</param>
    public ActionQueryService(
        IPhaseRuleService phaseRules,
        ICardUsageRuleService cardUsageRules,
        Skills.SkillManager? skillManager = null,
        IRuleModifierProvider? modifierProvider = null)
    {
        _phaseRules = phaseRules ?? throw new ArgumentNullException(nameof(phaseRules));
        _cardUsageRules = cardUsageRules ?? throw new ArgumentNullException(nameof(cardUsageRules));
        _skillManager = skillManager;
        _modifierProvider = modifierProvider;
        _cardConversionService = skillManager is not null 
            ? new CardConversionService(skillManager, cardUsageRules) 
            : null;
    }

    /// <inheritdoc />
    public RuleQueryResult<ActionDescriptor> GetAvailableActions(RuleContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var game = context.Game;
        var player = context.CurrentPlayer;

        var actions = new List<ActionDescriptor>();

        // For phase 2, only consider basic Play-phase actions.
        if (_phaseRules.IsCardUsagePhase(game, player))
        {
            // Collect all usable cards (direct + converted) grouped by card subtype
            var cardCandidatesByType = CollectCardCandidates(game, player);
            
            // Generate actions for each card type that has candidates
            // Use Dictionary for efficient lookup and update
            var actionsByActionId = new Dictionary<string, ActionDescriptor>();
            
            foreach (var kvp in cardCandidatesByType)
            {
                var cardSubType = kvp.Key;
                var candidates = kvp.Value;
                
                if (candidates.Count == 0)
                    continue;
                
                // Create or update action for this card type
                CreateOrUpdateActionForCardType(
                    actionsByActionId,
                    game,
                    player,
                    cardSubType,
                    candidates,
                    mergeCandidates: true);
            }

            // Convert dictionary to list
            actions.AddRange(actionsByActionId.Values);

            // Generate actions for active skills (including multi-card conversion skills)
            if (_skillManager is not null)
            {
                var skillActions = GenerateSkillActions(game, player, actionsByActionId);
                actions.AddRange(skillActions);
            }

            // EndPlayPhase: always available during Play phase.
            actions.Add(new ActionDescriptor(
                ActionId: "EndPlayPhase",
                DisplayKey: "action.endPlayPhase",
                RequiresTargets: false,
                TargetConstraints: new TargetConstraints(
                    MinTargets: 0,
                    MaxTargets: 0,
                    FilterType: TargetFilterType.Any),
                CardCandidates: null));
        }

        return actions.Count == 0
            ? RuleQueryResult<ActionDescriptor>.Empty(RuleErrorCode.NoLegalOptions)
            : RuleQueryResult<ActionDescriptor>.FromItems(actions);
    }

    /// <summary>
    /// Generates actions for active skills, including IActionProvidingSkill and multi-card conversion skills.
    /// Multi-card conversion skills are merged into the existing actions dictionary.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="player">The player to generate actions for.</param>
    /// <param name="actionsByActionId">Dictionary to merge multi-card conversion actions into. Can be null if not needed.</param>
    /// <returns>List of action descriptors from IActionProvidingSkill skills.</returns>
    private List<ActionDescriptor> GenerateSkillActions(
        Game game, 
        Player player, 
        Dictionary<string, ActionDescriptor>? actionsByActionId = null)
    {
        var actions = new List<ActionDescriptor>();

        if (_skillManager is null)
            return actions;

        var activeSkills = _skillManager.GetActiveSkills(game, player)
            .Where(s => s.Type == SkillType.Active && s.IsActive(game, player))
            .ToList();

        foreach (var skill in activeSkills)
        {
            // Handle IActionProvidingSkill - these generate complete actions
            if (skill is Skills.IActionProvidingSkill actionProvidingSkill)
            {
                var skillAction = actionProvidingSkill.GenerateAction(game, player);
                if (skillAction is not null)
                {
                    actions.Add(skillAction);
                }
            }
            // Handle IMultiCardConversionSkill - these modify existing card-based actions
            else if (skill is Skills.IMultiCardConversionSkill multiConversionSkill && actionsByActionId is not null)
            {
                // Check if player has enough hand cards for this skill
                var handCards = player.HandZone.Cards?.ToList() ?? new List<Card>();
                if (handCards.Count < multiConversionSkill.RequiredCardCount)
                    continue;

                // Create or update action for this conversion skill
                CreateOrUpdateActionForCardType(
                    actionsByActionId,
                    game,
                    player,
                    multiConversionSkill.TargetCardSubType,
                    handCards,
                    mergeCandidates: true,
                    deduplicateCandidates: true);
            }
        }

        return actions;
    }
    
    /// <summary>
    /// Collects all usable card candidates (both direct cards and converted cards) grouped by card subtype.
    /// Optimized to traverse hand cards only once.
    /// </summary>
    private Dictionary<CardSubType, List<Card>> CollectCardCandidates(Game game, Player player)
    {
        var candidatesByType = new Dictionary<CardSubType, List<Card>>();
        
        // Step 1: Collect direct usable cards - single traversal
        foreach (var card in player.HandZone.Cards)
        {
            var usage = new CardUsageContext(
                game,
                player,
                card,
                game.Players,
                IsExtraAction: false,
                UsageCountThisTurn: 0);
            
            if (_cardUsageRules.CanUseCard(usage).IsAllowed)
            {
                if (!candidatesByType.TryGetValue(card.CardSubType, out var candidates))
                {
                    candidates = new List<Card>();
                    candidatesByType[card.CardSubType] = candidates;
                }
                // Avoid duplicates
                if (!candidates.Any(c => c.Id == card.Id))
                {
                    candidates.Add(card);
                }
            }
        }
        
        // Step 2: Discover converted cards via conversion skills
        // Note: We check ALL cards for conversion, even if they're already usable as their original type.
        // This allows cards to be used both as their original type and as converted types.
        if (_cardConversionService is not null)
        {
            var conversionTargets = _cardConversionService.DiscoverConversionTargets(game, player);
            
            // Merge converted cards into candidates
            foreach (var kvp in conversionTargets)
            {
                var targetSubType = kvp.Key;
                var convertedCards = kvp.Value;
                
                if (!candidatesByType.TryGetValue(targetSubType, out var candidates))
                {
                    candidates = new List<Card>();
                    candidatesByType[targetSubType] = candidates;
                }
                
                // Add converted cards, avoiding duplicates
                foreach (var card in convertedCards)
                {
                    if (!candidates.Any(c => c.Id == card.Id))
                    {
                        candidates.Add(card);
                    }
                }
            }
        }
        
        return candidatesByType;
    }

    /// <summary>
    /// Creates or updates an action descriptor for a given card type in the actions dictionary.
    /// This method abstracts the common logic of creating/updating actions for both direct cards
    /// and multi-card conversion skills.
    /// </summary>
    /// <param name="actionsByActionId">Dictionary to store actions by action ID.</param>
    /// <param name="game">The current game state.</param>
    /// <param name="player">The player who owns the cards.</param>
    /// <param name="cardSubType">The card subtype to create/update action for.</param>
    /// <param name="candidates">The card candidates to include in the action.</param>
    /// <param name="mergeCandidates">Whether to merge candidates if action already exists. If false, replaces existing candidates.</param>
    /// <param name="deduplicateCandidates">Whether to deduplicate candidates when merging. Only used if mergeCandidates is true.</param>
    private void CreateOrUpdateActionForCardType(
        Dictionary<string, ActionDescriptor> actionsByActionId,
        Game game,
        Player player,
        CardSubType cardSubType,
        IReadOnlyList<Card> candidates,
        bool mergeCandidates = true,
        bool deduplicateCandidates = false)
    {
        if (candidates.Count == 0)
            return;

        // Get action configuration for this card type using ActionIdMapper
        var actionId = ActionIdMapper.GetActionIdForCardSubType(cardSubType);
        if (actionId is null)
            return; // Skip if no action mapping exists

        // Get target constraints using TargetConstraintsFactory
        var baseTargetConstraints = TargetConstraintsFactory.GetTargetConstraintsForCardSubType(cardSubType);
        if (baseTargetConstraints is null)
            return; // Skip if no target constraints defined

        // Apply target limit modifiers for each candidate card
        // Since different cards may have different target limits (e.g., Fang Tian Hua Ji),
        // we need to find the maximum allowed targets across all candidates
        var maxTargets = baseTargetConstraints.MaxTargets;
        if (_modifierProvider is not null)
        {
            var modifiers = _modifierProvider.GetModifiersFor(game, player);
            foreach (var card in candidates)
            {
                var usageContext = new CardUsageContext(
                    game,
                    player,
                    card,
                    game.Players,
                    IsExtraAction: false,
                    UsageCountThisTurn: 0);
                
                var cardMaxTargets = baseTargetConstraints.MaxTargets;
                foreach (var modifier in modifiers)
                {
                    var modified = modifier.ModifyMaxTargets(cardMaxTargets, usageContext);
                    if (modified.HasValue)
                    {
                        cardMaxTargets = modified.Value;
                    }
                }
                
                // Track the maximum across all candidates
                if (cardMaxTargets > maxTargets)
                {
                    maxTargets = cardMaxTargets;
                }
            }
        }

        // Create modified target constraints with the maximum targets
        var targetConstraints = baseTargetConstraints with { MaxTargets = maxTargets };

        // Check if action already exists
        if (actionsByActionId.TryGetValue(actionId, out var existingAction))
        {
            if (mergeCandidates)
            {
                // Merge candidates if action already exists
                var mergedCandidates = new List<Card>(existingAction.CardCandidates ?? Array.Empty<Card>());
                
                if (deduplicateCandidates)
                {
                    // Add candidates with deduplication
                    foreach (var card in candidates)
                    {
                        if (!mergedCandidates.Any(c => c.Id == card.Id))
                        {
                            mergedCandidates.Add(card);
                        }
                    }
                }
                else
                {
                    // Add all candidates without deduplication
                    mergedCandidates.AddRange(candidates);
                }
                
                actionsByActionId[actionId] = existingAction with { CardCandidates = mergedCandidates };
            }
            else
            {
                // Replace existing candidates
                actionsByActionId[actionId] = existingAction with { CardCandidates = candidates };
            }
        }
        else
        {
            // Create new action
            actionsByActionId[actionId] = new ActionDescriptor(
                ActionId: actionId,
                DisplayKey: $"action.use{cardSubType}",
                RequiresTargets: targetConstraints.MinTargets > 0,
                TargetConstraints: targetConstraints,
                CardCandidates: candidates);
        }
    }
}
