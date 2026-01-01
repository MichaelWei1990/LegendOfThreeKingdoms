using System;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Skills;

namespace LegendOfThreeKingdoms.Core.Rules;

/// <summary>
/// Facade that composes low-level rule services into the high-level <see cref="IRuleService"/> API.
/// </summary>
public sealed class RuleService : IRuleService
{
    private readonly IPhaseRuleService _phaseRules;
    private readonly ICardUsageRuleService _cardUsageRules;
    private readonly IResponseRuleService _responseRules;
    private readonly IRangeRuleService _rangeRules;
    private readonly ILimitRuleService _limitRules;
    private readonly IActionQueryService _actionQuery;
    private readonly IRuleModifierProvider _modifierProvider;

    /// <summary>
    /// Creates a new RuleService with optional service overrides.
    /// </summary>
    /// <param name="phaseRules">Optional phase rule service. If null, uses default.</param>
    /// <param name="cardUsageRules">Optional card usage rule service. If null, uses default.</param>
    /// <param name="responseRules">Optional response rule service. If null, uses default.</param>
    /// <param name="rangeRules">Optional range rule service. If null, uses default.</param>
    /// <param name="limitRules">Optional limit rule service. If null, uses default.</param>
    /// <param name="actionQuery">Optional action query service. If null, uses default.</param>
    /// <param name="modifierProvider">Optional rule modifier provider. If null, uses default.</param>
    /// <param name="skillManager">Optional skill manager for card conversion and skill-based rules.</param>
    public RuleService(
        IPhaseRuleService? phaseRules = null,
        ICardUsageRuleService? cardUsageRules = null,
        IResponseRuleService? responseRules = null,
        IRangeRuleService? rangeRules = null,
        ILimitRuleService? limitRules = null,
        IActionQueryService? actionQuery = null,
        IRuleModifierProvider? modifierProvider = null,
        SkillManager? skillManager = null)
    {
        _phaseRules = phaseRules ?? new PhaseRuleService();
        _limitRules = limitRules ?? new LimitRuleService();
        _modifierProvider = modifierProvider ?? new NoOpRuleModifierProvider();
        _rangeRules = rangeRules ?? new RangeRuleService(_modifierProvider);
        _cardUsageRules = cardUsageRules ?? new CardUsageRuleService(_phaseRules, _rangeRules, _limitRules, _modifierProvider, skillManager);
        _responseRules = responseRules ?? new ResponseRuleService(skillManager);
        _actionQuery = actionQuery ?? new ActionQueryService(_phaseRules, _cardUsageRules, skillManager, _modifierProvider);
    }

    /// <inheritdoc />
    public RuleResult CanUseCard(CardUsageContext context)
    {
        var baseResult = _cardUsageRules.CanUseCard(context);
        var modifiers = _modifierProvider.GetModifiersFor(context.Game, context.SourcePlayer);
        var result = baseResult;
        foreach (var modifier in modifiers)
        {
            result = modifier.ModifyCanUseCard(result, context);
        }

        return result;
    }

    /// <inheritdoc />
    public RuleQueryResult<Player> GetLegalTargetsForUse(CardUsageContext context)
    {
        return _cardUsageRules.GetLegalTargets(context);
    }

    /// <inheritdoc />
    public RuleResult CanRespondWithCard(ResponseContext context)
    {
        var baseResult = _responseRules.CanRespondWithCard(context);
        var modifiers = _modifierProvider.GetModifiersFor(context.Game, context.Responder);
        var result = baseResult;
        foreach (var modifier in modifiers)
        {
            result = modifier.ModifyCanRespondWithCard(result, context);
        }

        return result;
    }

    /// <inheritdoc />
    public RuleQueryResult<Card> GetUsableCards(RuleContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var game = context.Game;
        var player = context.CurrentPlayer;
        var handCards = player.HandZone.Cards;

        var usable = new List<Card>(handCards.Count);

        foreach (var card in handCards)
        {
            var usageContext = new CardUsageContext(
                game,
                player,
                card,
                game.Players,
                IsExtraAction: false,
                UsageCountThisTurn: 0);

            if (_cardUsageRules.CanUseCard(usageContext).IsAllowed)
            {
                usable.Add(card);
            }
        }

        return usable.Count == 0
            ? RuleQueryResult<Card>.Empty(RuleErrorCode.NoLegalOptions)
            : RuleQueryResult<Card>.FromItems(usable);
    }

    /// <inheritdoc />
    public RuleQueryResult<ActionDescriptor> GetAvailableActions(RuleContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        return _actionQuery.GetAvailableActions(context);
    }

    /// <inheritdoc />
    public RuleResult ValidateActionBeforeResolve(RuleContext context, ActionDescriptor action, ChoiceRequest? choice)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (action is null) throw new ArgumentNullException(nameof(action));

        // Base implementation simply approves the action; modifiers may change this.
        var baseResult = RuleResult.Allowed;
        var modifiers = _modifierProvider.GetModifiersFor(context.Game, context.CurrentPlayer);
        var result = baseResult;
        foreach (var modifier in modifiers)
        {
            result = modifier.ModifyValidateAction(result, context, action, choice);
        }

        return result;
    }

    /// <summary>
    /// Gets all rule modifiers for the specified player.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <param name="player">The player whose modifiers are requested.</param>
    /// <returns>A list of rule modifiers applicable to the player.</returns>
    public IReadOnlyList<IRuleModifier> GetModifiersFor(Game game, Player player)
    {
        return _modifierProvider.GetModifiersFor(game, player);
    }
}
