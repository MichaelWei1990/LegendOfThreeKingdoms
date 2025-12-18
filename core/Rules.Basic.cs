using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;

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

        // Initial implementation: base attack distance is always 1.
        // Equipment and skills will modify this in later phases.
        return 1;
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

    public CardUsageRuleService(
        IPhaseRuleService phaseRules,
        IRangeRuleService rangeRules,
        ILimitRuleService limitRules)
    {
        _phaseRules = phaseRules ?? throw new ArgumentNullException(nameof(phaseRules));
        _rangeRules = rangeRules ?? throw new ArgumentNullException(nameof(rangeRules));
        _limitRules = limitRules ?? throw new ArgumentNullException(nameof(limitRules));
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

        switch (context.Card.CardSubType)
        {
            case CardSubType.Slash:
                {
                    var maxSlash = _limitRules.GetMaxSlashPerTurn(game, source);
                    if (context.UsageCountThisTurn >= maxSlash)
                    {
                        return RuleResult.Disallowed(
                            RuleErrorCode.UsageLimitReached,
                            messageKey: "rules.slash.limitReached",
                            details: new { context.UsageCountThisTurn, maxSlash });
                    }

                    var targets = GetLegalTargets(context);
                    if (!targets.HasAny)
                    {
                        return RuleResult.Disallowed(RuleErrorCode.NoLegalOptions);
                    }

                    return RuleResult.Allowed;
                }
            case CardSubType.Peach:
                {
                    if (source.CurrentHealth >= source.MaxHealth)
                    {
                        return RuleResult.Disallowed(
                            RuleErrorCode.NoLegalOptions,
                            messageKey: "rules.peach.noInjury");
                    }

                    return RuleResult.Allowed;
                }
            default:
                // Other card types are not handled in the initial implementation.
                return RuleResult.Disallowed(RuleErrorCode.CardTypeNotAllowed);
        }
    }

    public RuleQueryResult<Player> GetLegalTargets(CardUsageContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var game = context.Game;
        var source = context.SourcePlayer;

        if (context.Card.CardSubType != CardSubType.Slash)
        {
            // Only Slash has target logic at this phase.
            return RuleQueryResult<Player>.Empty(RuleErrorCode.NoLegalOptions);
        }

        var legalTargets = game.Players
            .Where(p => p.IsAlive && p.Seat != source.Seat && _rangeRules.IsWithinAttackRange(game, source, p))
            .ToArray();

        if (legalTargets.Length == 0)
        {
            return RuleQueryResult<Player>.Empty(RuleErrorCode.NoLegalOptions);
        }

        return RuleQueryResult<Player>.FromItems(legalTargets);
    }
}

/// <summary>
/// Default implementation of basic response rules for Jink and Peach.
/// </summary>
public sealed class ResponseRuleService : IResponseRuleService
{
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

    public RuleQueryResult<Card> GetLegalResponseCards(ResponseContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var responder = context.Responder;
        var handCards = responder.HandZone.Cards;

        IReadOnlyList<Card> result = context.ResponseType switch
        {
            ResponseType.JinkAgainstSlash => handCards
                .Where(c => c.CardSubType == CardSubType.Dodge)
                .ToArray(),
            ResponseType.PeachForDying => handCards
                .Where(c => c.CardSubType == CardSubType.Peach)
                .ToArray(),
            _ => Array.Empty<Card>()
        };

        if (result.Count == 0)
        {
            return RuleQueryResult<Card>.Empty(RuleErrorCode.NoLegalOptions);
        }

        return RuleQueryResult<Card>.FromItems(result);
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
        IRuleModifierProvider? modifierProvider = null)
    {
        _phaseRules = phaseRules ?? new PhaseRuleService();
        _rangeRules = rangeRules ?? new RangeRuleService();
        _limitRules = limitRules ?? new LimitRuleService();
        _cardUsageRules = cardUsageRules ?? new CardUsageRuleService(_phaseRules, _rangeRules, _limitRules);
        _responseRules = responseRules ?? new ResponseRuleService();
        _actionQuery = actionQuery ?? new ActionQueryService(_phaseRules, _cardUsageRules);
        _modifierProvider = modifierProvider ?? new NoOpRuleModifierProvider();
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
}

/// <summary>
/// Default implementation that derives currently available high-level actions for a player.
/// </summary>
public sealed class ActionQueryService : IActionQueryService
{
    private readonly IPhaseRuleService _phaseRules;
    private readonly ICardUsageRuleService _cardUsageRules;

    public ActionQueryService(
        IPhaseRuleService phaseRules,
        ICardUsageRuleService cardUsageRules)
    {
        _phaseRules = phaseRules ?? throw new ArgumentNullException(nameof(phaseRules));
        _cardUsageRules = cardUsageRules ?? throw new ArgumentNullException(nameof(cardUsageRules));
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
            // UseSlash: if there is any usable Slash in hand.
            var slashCandidates = player.HandZone.Cards
                .Where(c => c.CardSubType == CardSubType.Slash)
                .Where(c =>
                {
                    var usage = new CardUsageContext(
                        game,
                        player,
                        c,
                        game.Players,
                        IsExtraAction: false,
                        UsageCountThisTurn: 0);
                    return _cardUsageRules.CanUseCard(usage).IsAllowed;
                })
                .ToArray();

            if (slashCandidates.Length > 0)
            {
                actions.Add(new ActionDescriptor(
                    ActionId: "UseSlash",
                    DisplayKey: "action.useSlash",
                    RequiresTargets: true,
                    TargetConstraints: new TargetConstraints(
                        MinTargets: 1,
                        MaxTargets: 1,
                        FilterType: TargetFilterType.Enemies),
                    CardCandidates: slashCandidates));
            }

            // UsePeach: if the player is wounded and has Peach in hand.
            var peachCandidates = player.HandZone.Cards
                .Where(c => c.CardSubType == CardSubType.Peach)
                .Where(c =>
                {
                    var usage = new CardUsageContext(
                        game,
                        player,
                        c,
                        game.Players,
                        IsExtraAction: false,
                        UsageCountThisTurn: 0);
                    return _cardUsageRules.CanUseCard(usage).IsAllowed;
                })
                .ToArray();

            if (peachCandidates.Length > 0)
            {
                actions.Add(new ActionDescriptor(
                    ActionId: "UsePeach",
                    DisplayKey: "action.usePeach",
                    RequiresTargets: false,
                    TargetConstraints: new TargetConstraints(
                        MinTargets: 0,
                        MaxTargets: 0,
                        FilterType: TargetFilterType.SelfOrFriends),
                    CardCandidates: peachCandidates));
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


