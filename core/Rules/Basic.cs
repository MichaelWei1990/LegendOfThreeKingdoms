using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Skills;

namespace LegendOfThreeKingdoms.Core.Rules;

/// <summary>
/// Default phase rule service: only the active player's Play phase allows normal card usage.
/// </summary>
public sealed class PhaseRuleService : IPhaseRuleService
{
    public bool IsCardUsagePhase(Game game, Player player)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        if (player is null) throw new ArgumentNullException(nameof(player));

        if (!player.IsAlive)
        {
            return false;
        }

        // Only the active player's Play phase allows using basic cards in the initial implementation.
        return game.CurrentPhase == Phase.Play && player.Seat == game.CurrentPlayerSeat;
    }
}

/// <summary>
/// Default implementation of usage limits (e.g. Slash once per turn).
/// </summary>
public sealed class LimitRuleService : ILimitRuleService
{
    /// <inheritdoc />
    public int GetMaxSlashPerTurn(Game game, Player player)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        if (player is null) throw new ArgumentNullException(nameof(player));

        // Initial implementation: fixed 1 Slash per turn.
        return 1;
    }
}

/// <summary>
/// Default seat / attack range rules.
/// </summary>
public sealed class RangeRuleService : IRangeRuleService
{
    private readonly IRuleModifierProvider? _modifierProvider;

    /// <summary>
    /// Creates a new RangeRuleService.
    /// </summary>
    /// <param name="modifierProvider">Optional rule modifier provider for applying equipment and skill modifications.</param>
    public RangeRuleService(IRuleModifierProvider? modifierProvider = null)
    {
        _modifierProvider = modifierProvider;
    }

    public int GetSeatDistance(Game game, Player from, Player to)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        if (from is null) throw new ArgumentNullException(nameof(from));
        if (to is null) throw new ArgumentNullException(nameof(to));

        if (ReferenceEquals(from, to) || from.Seat == to.Seat)
        {
            throw new InvalidOperationException(
                "GetSeatDistance was called with the same player as both source and target. " +
                "Seat distance is only defined between distinct players.");
        }

        var players = game.Players;
        var count = players.Count;
        if (count == 0)
        {
            throw new InvalidOperationException(
                "GetSeatDistance was called on a game with zero players. " +
                "This indicates an invalid Game state; at least one player is required.");
        }

        var fromIndex = IndexOfSeat(players, from.Seat);
        var toIndex = IndexOfSeat(players, to.Seat);
        if (fromIndex < 0 || toIndex < 0)
        {
            throw new InvalidOperationException(
                $"Player seat not found in game.Players (fromSeat={from.Seat}, toSeat={to.Seat}). " +
                "This indicates a model consistency bug and should never happen in normal gameplay.");
        }

        var clockwise = (toIndex - fromIndex + count) % count;
        var counterClockwise = (fromIndex - toIndex + count) % count;
        var distance = Math.Min(clockwise, counterClockwise);

        // Distance should be at least 1 when seats differ.
        return distance == 0 ? 1 : distance;
    }

    public int GetAttackDistance(Game game, Player from, Player to)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        if (from is null) throw new ArgumentNullException(nameof(from));
        if (to is null) throw new ArgumentNullException(nameof(to));

        // Base attack distance is always 1.
        int baseDistance = 1;

        // Apply rule modifiers from the attacker's perspective
        if (_modifierProvider is not null)
        {
            var modifiers = _modifierProvider.GetModifiersFor(game, from);
            foreach (var modifier in modifiers)
            {
                var modified = modifier.ModifyAttackDistance(baseDistance, game, from, to);
                if (modified.HasValue)
                {
                    baseDistance = modified.Value;
                }
            }
        }

        return baseDistance;
    }

    public bool IsWithinAttackRange(Game game, Player from, Player to)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        if (from is null) throw new ArgumentNullException(nameof(from));
        if (to is null) throw new ArgumentNullException(nameof(to));

        if (!to.IsAlive)
        {
            return false;
        }

        var seatDistance = GetSeatDistance(game, from, to);
        var attackDistance = GetAttackDistance(game, from, to);

        // Apply defensive rule modifiers from the defender's perspective
        // This allows defensive equipment (like defensive horse) to modify the seat distance requirement
        if (_modifierProvider is not null)
        {
            var modifiers = _modifierProvider.GetModifiersFor(game, to);
            foreach (var modifier in modifiers)
            {
                var modified = modifier.ModifySeatDistance(seatDistance, game, from, to);
                if (modified.HasValue)
                {
                    seatDistance = modified.Value;
                }
            }
        }

        // Apply offensive rule modifiers from the attacker's perspective
        // This allows offensive equipment (like offensive horse) to modify the seat distance requirement
        if (_modifierProvider is not null)
        {
            var modifiers = _modifierProvider.GetModifiersFor(game, from);
            foreach (var modifier in modifiers)
            {
                var modified = modifier.ModifySeatDistance(seatDistance, game, from, to);
                if (modified.HasValue)
                {
                    seatDistance = modified.Value;
                }
            }
        }

        return seatDistance <= attackDistance;
    }

    private static int IndexOfSeat(IReadOnlyList<Player> players, int seat)
    {
        for (var i = 0; i < players.Count; i++)
        {
            if (players[i].Seat == seat)
            {
                return i;
            }
        }

        return -1;
    }
}

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

/// <summary>
/// Default implementation of basic response rules for Jink and Peach.
/// </summary>
public sealed class ResponseRuleService : IResponseRuleService
{
    private readonly SkillManager? _skillManager;

    public ResponseRuleService(SkillManager? skillManager = null)
    {
        _skillManager = skillManager;
    }

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
    public RuleResult CanRespondWithCardWithHypotheticalState(
        ResponseContext context,
        Func<Game, Player> createHypotheticalState)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (createHypotheticalState is null) throw new ArgumentNullException(nameof(createHypotheticalState));

        // Create hypothetical player state
        var hypotheticalResponder = createHypotheticalState(context.Game);

        // Create new context with hypothetical responder
        var hypotheticalContext = new ResponseContext(
            Game: context.Game,
            Responder: hypotheticalResponder,
            ResponseType: context.ResponseType,
            SourceEvent: context.SourceEvent);

        // Use the standard CanRespondWithCard logic with hypothetical responder
        return CanRespondWithCard(hypotheticalContext);
    }

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
    /// </summary>
    private List<Card> GetConvertibleCards(Game game, Player player, CardSubType targetSubType)
    {
        var convertibleCards = new List<Card>();
        
        if (_skillManager is null)
            return convertibleCards;

        var conversionSkills = _skillManager.GetActiveSkills(game, player)
            .OfType<Skills.ICardConversionSkill>()
            .ToList();

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
                    // For equipment cards, check dependency (e.g., Wusheng)
                    if (conversionSkill is Skills.Hero.WushengSkill wushengSkill)
                    {
                        // For response scenarios, dependency checking is usually not needed
                        // But we still check if the skill supports it
                        // For now, we'll add the card and let the dependency check happen during actual conversion
                    }

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
}

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
        _actionQuery = actionQuery ?? new ActionQueryService(_phaseRules, _cardUsageRules, skillManager);
    }

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

    public RuleQueryResult<Player> GetLegalTargetsForUse(CardUsageContext context)
    {
        return _cardUsageRules.GetLegalTargets(context);
    }

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

    public RuleQueryResult<ActionDescriptor> GetAvailableActions(RuleContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        return _actionQuery.GetAvailableActions(context);
    }

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

/// <summary>
/// Default implementation that derives currently available high-level actions for a player.
/// </summary>
public sealed class ActionQueryService : IActionQueryService
{
    private readonly IPhaseRuleService _phaseRules;
    private readonly ICardUsageRuleService _cardUsageRules;
    private readonly Skills.SkillManager? _skillManager;
    
    // Cache for TargetConstraints instances to avoid repeated allocations
    // Using ConcurrentDictionary for thread-safety in parallel test execution
    private static readonly ConcurrentDictionary<CardSubType, TargetConstraints> _targetConstraintsCache = new();

    public ActionQueryService(
        IPhaseRuleService phaseRules,
        ICardUsageRuleService cardUsageRules,
        Skills.SkillManager? skillManager = null)
    {
        _phaseRules = phaseRules ?? throw new ArgumentNullException(nameof(phaseRules));
        _cardUsageRules = cardUsageRules ?? throw new ArgumentNullException(nameof(cardUsageRules));
        _skillManager = skillManager;
    }

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
    /// </summary>
    private Dictionary<CardSubType, List<Card>> CollectCardCandidates(Game game, Player player)
    {
        var candidatesByType = new Dictionary<CardSubType, List<Card>>();
        
        // Step 1: Collect direct usable cards
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
        if (_skillManager is not null)
        {
            var conversionTargets = DiscoverConversionTargets(game, player);
            
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
    /// Discovers all card types that can be converted to via conversion skills.
    /// Returns a dictionary mapping target card subtypes to lists of source cards that can be converted.
    /// </summary>
    private Dictionary<CardSubType, List<Card>> DiscoverConversionTargets(
        Game game, 
        Player player)
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
    /// Gets all active conversion skills for the player.
    /// </summary>
    private List<Skills.ICardConversionSkill> GetConversionSkills(Game game, Player player)
    {
        if (_skillManager is null)
            return new List<Skills.ICardConversionSkill>();
        
        return _skillManager.GetActiveSkills(game, player)
            .OfType<Skills.ICardConversionSkill>()
            .ToList();
    }

    /// <summary>
    /// Discovers conversion candidates from a specific zone (hand or equipment).
    /// </summary>
    private void DiscoverConversionsFromZone(
        Game game,
        Player player,
        IEnumerable<Card> cards,
        List<Skills.ICardConversionSkill> conversionSkills,
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
    /// Result of a conversion attempt.
    /// </summary>
    private record ConversionResult(CardSubType TargetSubType);

    /// <summary>
    /// Tries to find a valid conversion for a card using available conversion skills.
    /// Returns the target subtype if a valid conversion is found, null otherwise.
    /// </summary>
    private ConversionResult? TryFindValidConversion(
        Game game,
        Player player,
        Card card,
        List<Skills.ICardConversionSkill> conversionSkills)
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
    private bool CanUseConvertedCard(Game game, Player player, Card virtualCard)
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
    /// Checks if a converted card has legal targets (if targets are required).
    /// </summary>
    private bool HasLegalTargets(Game game, Player player, Card virtualCard, CardSubType targetSubType)
    {
        var targetConstraints = GetTargetConstraintsForCardSubType(targetSubType);
        
        // If no targets are required, it's valid
        if (targetConstraints is null || targetConstraints.MinTargets == 0)
            return true;
        
        // Check if there are legal targets
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
    /// Gets the action ID for a given card subtype.
    /// Returns null if the card subtype does not have a corresponding action.
    /// </summary>
    private static string? GetActionIdForCardSubType(CardSubType cardSubType)
    {
        return cardSubType switch
        {
            CardSubType.Slash => "UseSlash",
            CardSubType.Peach => "UsePeach",
            CardSubType.GuoheChaiqiao => "UseGuoheChaiqiao",
            CardSubType.Lebusishu => "UseLebusishu",
            // Add more mappings as needed
            _ => null
        };
    }

    /// <summary>
    /// Creates or updates an action descriptor for a given card type in the actions dictionary.
    /// This method abstracts the common logic of creating/updating actions for both direct cards
    /// and multi-card conversion skills.
    /// </summary>
    /// <param name="actionsByActionId">Dictionary to store actions by action ID.</param>
    /// <param name="cardSubType">The card subtype to create/update action for.</param>
    /// <param name="candidates">The card candidates to include in the action.</param>
    /// <param name="mergeCandidates">Whether to merge candidates if action already exists. If false, replaces existing candidates.</param>
    /// <param name="deduplicateCandidates">Whether to deduplicate candidates when merging. Only used if mergeCandidates is true.</param>
    private void CreateOrUpdateActionForCardType(
        Dictionary<string, ActionDescriptor> actionsByActionId,
        CardSubType cardSubType,
        IReadOnlyList<Card> candidates,
        bool mergeCandidates = true,
        bool deduplicateCandidates = false)
    {
        if (candidates.Count == 0)
            return;

        // Get action configuration for this card type
        var actionId = GetActionIdForCardSubType(cardSubType);
        if (actionId is null)
            return; // Skip if no action mapping exists

        var targetConstraints = GetTargetConstraintsForCardSubType(cardSubType);
        if (targetConstraints is null)
            return; // Skip if no target constraints defined

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

    /// <summary>
    /// Gets the target constraints for a given card subtype.
    /// Returns null if the card subtype does not require targets or is not supported.
    /// Uses a cache to avoid repeated allocations of identical TargetConstraints instances.
    /// Thread-safe implementation using ConcurrentDictionary.GetOrAdd for atomic operations.
    /// </summary>
    private static TargetConstraints? GetTargetConstraintsForCardSubType(CardSubType cardSubType)
    {
        // Use GetOrAdd for atomic check-and-add operation to avoid race conditions
        // This ensures only one thread will create the value, even if multiple threads
        // call this method concurrently for the same cardSubType
        return _targetConstraintsCache.GetOrAdd(cardSubType, key =>
        {
            // Factory function: create new instance based on card subtype
            return key switch
            {
                CardSubType.Slash => new TargetConstraints(
                    MinTargets: 1,
                    MaxTargets: 1,
                    FilterType: TargetFilterType.Enemies),
                CardSubType.Peach => new TargetConstraints(
                    MinTargets: 0,
                    MaxTargets: 0,
                    FilterType: TargetFilterType.SelfOrFriends),
                CardSubType.GuoheChaiqiao => new TargetConstraints(
                    MinTargets: 1,
                    MaxTargets: 1,
                    FilterType: TargetFilterType.Any),
                CardSubType.Lebusishu => new TargetConstraints(
                    MinTargets: 1,
                    MaxTargets: 1,
                    FilterType: TargetFilterType.Any),
                // Add more mappings as needed
                _ => null
            };
        });
    }
}

/// <summary>
/// Default rule modifier that performs no modifications.
/// All methods return the original value or null (no modification).
/// </summary>
public sealed class NoOpRuleModifier : IRuleModifier
{
    /// <inheritdoc />
    public RuleResult ModifyCanUseCard(RuleResult current, CardUsageContext context)
    {
        return current;
    }

    /// <inheritdoc />
    public RuleResult ModifyCanRespondWithCard(RuleResult current, ResponseContext context)
    {
        return current;
    }

    /// <inheritdoc />
    public RuleResult ModifyValidateAction(RuleResult current, RuleContext context, ActionDescriptor action, ChoiceRequest? choice)
    {
        return current;
    }

    /// <inheritdoc />
    public int? ModifyMaxSlashPerTurn(int current, Game game, Player player)
    {
        return null;
    }

    /// <inheritdoc />
    public int? ModifyAttackDistance(int current, Game game, Player from, Player to)
    {
        return null;
    }

    /// <inheritdoc />
    public int? ModifySeatDistance(int current, Game game, Player from, Player to)
    {
        return null;
    }

    /// <inheritdoc />
    public int? ModifyDrawCount(int current, Game game, Player player)
    {
        return null;
    }
}

/// <summary>
/// Default modifier provider used by the basic rules implementation.
/// It returns no modifiers, effectively leaving all rule results unchanged.
/// </summary>
public sealed class NoOpRuleModifierProvider : IRuleModifierProvider
{
    private static readonly IReadOnlyList<IRuleModifier> EmptyModifiers = Array.Empty<IRuleModifier>();

    public IReadOnlyList<IRuleModifier> GetModifiersFor(Game game, Player player)
    {
        return EmptyModifiers;
    }
}


