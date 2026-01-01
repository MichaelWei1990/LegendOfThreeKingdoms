using System;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Skills;

namespace LegendOfThreeKingdoms.Core.Rules;

/// <summary>
/// Default implementation of basic card usage rules for Slash and Peach.
/// </summary>
public sealed class CardUsageRuleService : ICardUsageRuleService
{
    private readonly IPhaseRuleService _phaseRules;
    private readonly IRangeRuleService _rangeRules;
    private readonly ILimitRuleService _limitRules;
    private readonly IRuleModifierProvider? _modifierProvider;
    private readonly TargetSelectionService _targetSelection;

    /// <summary>
    /// Creates a new CardUsageRuleService.
    /// </summary>
    /// <param name="phaseRules">The phase rule service.</param>
    /// <param name="rangeRules">The range rule service.</param>
    /// <param name="limitRules">The limit rule service.</param>
    /// <param name="modifierProvider">Optional rule modifier provider.</param>
    /// <param name="skillManager">Optional skill manager.</param>
    public CardUsageRuleService(
        IPhaseRuleService phaseRules,
        IRangeRuleService rangeRules,
        ILimitRuleService limitRules,
        IRuleModifierProvider? modifierProvider = null,
        SkillManager? skillManager = null)
    {
        _phaseRules = phaseRules ?? throw new ArgumentNullException(nameof(phaseRules));
        _rangeRules = rangeRules ?? throw new ArgumentNullException(nameof(rangeRules));
        _limitRules = limitRules ?? throw new ArgumentNullException(nameof(limitRules));
        _modifierProvider = modifierProvider;
        _targetSelection = new TargetSelectionService(rangeRules, skillManager);
    }

    /// <inheritdoc />
    public RuleResult CanUseCard(CardUsageContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var game = context.Game;
        var source = context.SourcePlayer;

        if (!source.IsAlive)
        {
            return RuleResult.Disallowed(RuleErrorCode.PlayerNotActive);
        }

        if (!_phaseRules.IsCardUsagePhase(game, source))
        {
            return RuleResult.Disallowed(RuleErrorCode.PhaseNotAllowed);
        }

        // Check card type first
        if (context.Card.CardType == CardType.Equip)
        {
            // Equipment cards can be used during play phase
            // No target required, no usage limit
            return RuleResult.Allowed;
        }

        switch (context.Card.CardSubType)
        {
            case CardSubType.Slash:
                {
                    // Get base limit
                    var baseMaxSlash = _limitRules.GetMaxSlashPerTurn(game, source);
                    
                    // Apply skill modifications if modifier provider is available
                    var maxSlash = baseMaxSlash;
                    if (_modifierProvider is not null)
                    {
                        var modifiers = _modifierProvider.GetModifiersFor(game, source);
                        foreach (var modifier in modifiers)
                        {
                            var modified = modifier.ModifyMaxSlashPerTurn(maxSlash, game, source);
                            if (modified.HasValue)
                            {
                                maxSlash = modified.Value;
                            }
                        }
                    }
                    
                    if (context.UsageCountThisTurn >= maxSlash)
                    {
                        return RuleResult.Disallowed(
                            RuleErrorCode.UsageLimitReached,
                            messageKey: "rules.slash.limitReached",
                            details: new { context.UsageCountThisTurn, maxSlash });
                    }

                    var targets = _targetSelection.GetLegalTargets(context);
                    if (!targets.HasAny)
                    {
                        return RuleResult.Disallowed(RuleErrorCode.NoLegalOptions);
                    }

                    return RuleResult.Allowed;
                }
            case CardSubType.Peach:
                {
                    // Peach can be used on:
                    // 1. Injured self (CurrentHealth < MaxHealth)
                    // 2. Any character in dying state (CurrentHealth <= 0)
                    // Rule: Cannot use Peach on self if no health loss (CurrentHealth >= MaxHealth)
                    var hasValidTarget = false;
                    
                    // Check if self is injured
                    if (source.CurrentHealth < source.MaxHealth)
                    {
                        hasValidTarget = true;
                    }
                    else
                    {
                        // Check if there's any character in dying state (CurrentHealth <= 0)
                        // Rule: Peach can be used on any character in dying state
                        // Note: In dying state, we only check CurrentHealth <= 0, not IsAlive
                        foreach (var player in game.Players)
                        {
                            if (player.CurrentHealth <= 0)
                            {
                                hasValidTarget = true;
                                break;
                            }
                        }
                    }
                    
                    if (!hasValidTarget)
                    {
                        return RuleResult.Disallowed(
                            RuleErrorCode.NoLegalOptions,
                            messageKey: "rules.peach.noInjury");
                    }

                    return RuleResult.Allowed;
                }
            case CardSubType.ImmediateTrick:
            case CardSubType.DelayedTrick:
            case CardSubType.WuzhongShengyou:
            case CardSubType.TaoyuanJieyi:
            case CardSubType.ShunshouQianyang:
            case CardSubType.GuoheChaiqiao:
            case CardSubType.WanjianQifa:
            case CardSubType.NanmanRushin:
            case CardSubType.Duel:
            case CardSubType.Lebusishu:
            case CardSubType.Shandian:
                {
                    // Trick cards can be used during play phase.
                    // Specific rules for each trick card type will be handled by their respective resolvers.
                    // For now, we allow all trick cards to be used if the phase is correct.
                    return RuleResult.Allowed;
                }
            default:
                // Other card types are not handled in the initial implementation.
                return RuleResult.Disallowed(RuleErrorCode.CardTypeNotAllowed);
        }
    }

    /// <inheritdoc />
    public RuleQueryResult<Player> GetLegalTargets(CardUsageContext context)
    {
        return _targetSelection.GetLegalTargets(context);
    }

    /// <inheritdoc />
    public RuleResult CanUseCardWithHypotheticalState(
        CardUsageContext context,
        Func<Game, Player> createHypotheticalState)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (createHypotheticalState is null) throw new ArgumentNullException(nameof(createHypotheticalState));

        // Create hypothetical player state
        var hypotheticalPlayer = createHypotheticalState(context.Game);

        // Create new context with hypothetical player
        var hypotheticalContext = new CardUsageContext(
            Game: context.Game,
            SourcePlayer: hypotheticalPlayer,
            Card: context.Card,
            CandidateTargets: context.CandidateTargets,
            IsExtraAction: context.IsExtraAction,
            UsageCountThisTurn: context.UsageCountThisTurn);

        // Use the standard CanUseCard logic with hypothetical player
        return CanUseCard(hypotheticalContext);
    }
}
