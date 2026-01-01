using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Resolver for Harvest (五谷丰登) immediate trick card.
/// Effect: Reveal N cards from the top of the draw pile (N = number of alive players),
/// then each alive player (starting from the user) selects and gains one card from the pool in turn order.
/// Each target can be nullified individually before gaining a card.
/// </summary>
public sealed class HarvestResolver : UntargetedMassTrickResolverBase
{
    private const string TargetsKey = "HarvestTargets";
    private const string PoolKey = "HarvestPool";
    private const string CurrentTargetIndexKey = "HarvestCurrentTargetIndex";
    private const string CausingCardKey = "HarvestCausingCard";

    /// <inheritdoc />
    public override ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var game = context.Game;
        var sourcePlayer = context.SourcePlayer;

        // Initialize IntermediateResults if not present
        var intermediateResults = context.IntermediateResults;
        if (intermediateResults is null)
        {
            intermediateResults = new Dictionary<string, object>();
        }

        // Check if we're processing targets (continuation)
        if (intermediateResults.TryGetValue(CurrentTargetIndexKey, out var indexObj) &&
            indexObj is int currentIndex &&
            intermediateResults.TryGetValue(TargetsKey, out var targetsObj) &&
            targetsObj is IReadOnlyList<Player> targets &&
            intermediateResults.TryGetValue(PoolKey, out var poolObj) &&
            poolObj is Zone pool)
        {
            // Continue processing next target
            return ProcessNextTarget(context, targets, currentIndex, pool, intermediateResults);
        }

        // First time: initialize target list and create pool
        var allTargets = GetTargetsInTurnOrder(game, sourcePlayer);
        
        if (allTargets.Count == 0)
        {
            // No targets, nothing to do
            if (context.LogSink is not null)
            {
                var logEntry = new LogEntry
                {
                    EventType = "HarvestEffect",
                    Level = "Info",
                    Message = "Harvest: No targets available",
                    Data = new { SourcePlayerSeat = sourcePlayer.Seat }
                };
                context.LogSink.Log(logEntry);
            }
            return ResolutionResult.SuccessResult;
        }

        // Get the causing card from context
        var causingCard = context.ExtractCausingCard();

        // Create pool and reveal cards
        var poolResult = CreatePool(game, allTargets.Count, context.CardMoveService);
        if (!poolResult.IsSuccess)
        {
            return poolResult.FailureResult!;
        }

        var poolZone = poolResult.PoolZone!;

        // Store targets, pool, causing card, and start processing from index 0
        intermediateResults[TargetsKey] = allTargets;
        intermediateResults[PoolKey] = poolZone;
        intermediateResults[CurrentTargetIndexKey] = 0;
        if (causingCard is not null)
        {
            intermediateResults[CausingCardKey] = causingCard;
        }

        // Create new context with IntermediateResults
        var newContext = new ResolutionContext(
            context.Game,
            context.SourcePlayer,
            context.Action,
            context.Choice,
            context.Stack,
            context.CardMoveService,
            context.RuleService,
            context.PendingDamage,
            context.LogSink,
            context.GetPlayerChoice,
            intermediateResults,
            context.EventBus,
            context.LogCollector,
            context.SkillManager,
            context.EquipmentSkillRegistry,
            context.JudgementService
        );

        // Push self back onto stack to process first target
        context.Stack.Push(this, newContext);

        return ResolutionResult.SuccessResult;
    }

    /// <summary>
    /// Creates a pool by revealing N cards from the top of the draw pile.
    /// </summary>
    private PoolCreationResult CreatePool(Game game, int targetCount, ICardMoveService cardMoveService)
    {
        if (game.DrawPile is not Zone drawZone)
        {
            return PoolCreationResult.CreateFailure(
                ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "resolution.harvest.drawPileNotZone"));
        }

        // Get available cards from draw pile
        var availableCards = drawZone.MutableCards;
        var cardsToReveal = Math.Min(targetCount, availableCards.Count);

        if (cardsToReveal == 0)
        {
            // No cards available, but this is not necessarily an error
            // Create empty pool
            var emptyPool = new Zone("HarvestPool", ownerSeat: null, isPublic: true);
            return PoolCreationResult.CreateSuccess(emptyPool);
        }

        // Get top N cards
        var topCards = availableCards.Take(cardsToReveal).ToList();

        // Create temporary pool zone (public so all players can see)
        var poolZone = new Zone("HarvestPool", ownerSeat: null, isPublic: true);

        // Move cards from draw pile to pool
        var moveDescriptor = new CardMoveDescriptor(
            SourceZone: drawZone,
            TargetZone: poolZone,
            Cards: topCards,
            Reason: CardMoveReason.Draw, // Using Draw reason for revealing cards
            Ordering: CardMoveOrdering.ToTop,
            Game: game
        );

        try
        {
            cardMoveService.MoveMany(moveDescriptor);
            return PoolCreationResult.CreateSuccess(poolZone);
        }
        catch (Exception ex)
        {
            return PoolCreationResult.CreateFailure(
                ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "resolution.harvest.poolCreationFailed",
                    details: new { Exception = ex.Message }));
        }
    }

    /// <summary>
    /// Processes the next target in the list.
    /// </summary>
    private ResolutionResult ProcessNextTarget(
        ResolutionContext context,
        IReadOnlyList<Player> targets,
        int currentIndex,
        Zone pool,
        Dictionary<string, object> intermediateResults)
    {
        if (currentIndex >= targets.Count)
        {
            // All targets processed, cleanup remaining cards in pool
            return CleanupPool(context, pool);
        }

        var target = targets[currentIndex];
        var sourcePlayer = context.SourcePlayer;

        // Skip if target is not alive (may have died during processing)
        if (!target.IsAlive)
        {
            // Move to next target
            intermediateResults[CurrentTargetIndexKey] = currentIndex + 1;
            var nextContext = new ResolutionContext(
                context.Game,
                context.SourcePlayer,
                context.Action,
                context.Choice,
                context.Stack,
                context.CardMoveService,
                context.RuleService,
                context.PendingDamage,
                context.LogSink,
                context.GetPlayerChoice,
                intermediateResults,
                context.EventBus,
                context.LogCollector,
                context.SkillManager,
                context.EquipmentSkillRegistry,
                context.JudgementService
            );
            context.Stack.Push(this, nextContext);
            return ResolutionResult.SuccessResult;
        }

        // Check if pool is empty
        if (pool.Cards is null || pool.Cards.Count == 0)
        {
            // Pool is empty, cleanup and finish
            return CleanupPool(context, pool);
        }

        // Get causing card from intermediate results
        Card? causingCard = null;
        if (intermediateResults.TryGetValue(CausingCardKey, out var cardObj) && cardObj is Card card)
        {
            causingCard = card;
        }

        // Create handler resolver context (will check nullification and apply card gain if needed)
        var handlerContext = new ResolutionContext(
            context.Game,
            context.SourcePlayer,
            context.Action,
            context.Choice,
            context.Stack,
            context.CardMoveService,
            context.RuleService,
            context.PendingDamage,
            context.LogSink,
            context.GetPlayerChoice,
            intermediateResults,
            context.EventBus,
            context.LogCollector,
            context.SkillManager,
            context.EquipmentSkillRegistry,
            context.JudgementService
        );

        // For source player: no nullification window (source always gains a card)
        // For other players: nullification window executes first, then handler
        // Due to LIFO stack: push order determines execution order (last pushed executes first)
        bool isSourcePlayer = target.Seat == sourcePlayer.Seat;

        if (isSourcePlayer)
        {
            // Source player: directly push handler (no nullification window for source's own gain)
            // Source player can use gained cards to nullify other players' gains when their windows open
            context.Stack.Push(new HarvestTargetHandlerResolver(target, pool, targets, currentIndex), handlerContext);
        }
        else
        {
            // Other players: create nullification effect and open nullification window
            var nullifiableEffect = NullificationHelper.CreateNullifiableEffect(
                effectKey: "Harvest.GainFromPool",
                targetPlayer: target,
                causingCard: causingCard,
                isNullifiable: true);

            var nullificationResultKey = $"HarvestNullification_{target.Seat}";

            // Push handler first, then nullification window
            // Execution order: nullification window -> handler
            context.Stack.Push(new HarvestTargetHandlerResolver(target, pool, targets, currentIndex), handlerContext);
            NullificationHelper.OpenNullificationWindow(context, nullifiableEffect, nullificationResultKey);
        }

        return ResolutionResult.SuccessResult;
    }

    /// <summary>
    /// Cleans up remaining cards in the pool by moving them to discard pile.
    /// </summary>
    private ResolutionResult CleanupPool(ResolutionContext context, Zone pool)
    {
        if (pool.Cards is null || pool.Cards.Count == 0)
        {
            return ResolutionResult.SuccessResult;
        }

        var game = context.Game;
        var remainingCards = pool.Cards.ToList();

        try
        {
            var moveDescriptor = new CardMoveDescriptor(
                SourceZone: pool,
                TargetZone: game.DiscardPile,
                Cards: remainingCards,
                Reason: CardMoveReason.Discard,
                Ordering: CardMoveOrdering.ToTop,
                Game: game
            );

            context.CardMoveService.MoveMany(moveDescriptor);

            // Log cleanup if log sink is available
            if (context.LogSink is not null)
            {
                var logEntry = new LogEntry
                {
                    EventType = "HarvestPoolCleanup",
                    Level = "Info",
                    Message = $"Harvest: {remainingCards.Count} remaining card(s) moved to discard pile",
                    Data = new
                    {
                        RemainingCardCount = remainingCards.Count,
                        RemainingCardIds = remainingCards.Select(c => c.Id).ToArray()
                    }
                };
                context.LogSink.Log(logEntry);
            }

            return ResolutionResult.SuccessResult;
        }
        catch (Exception ex)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.harvest.poolCleanupFailed",
                details: new { Exception = ex.Message });
        }
    }

    /// <summary>
    /// Gets all alive players including the source, in turn order starting from the source player.
    /// </summary>
    private static IReadOnlyList<Player> GetTargetsInTurnOrder(Game game, Player sourcePlayer)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        if (sourcePlayer is null) throw new ArgumentNullException(nameof(sourcePlayer));

        var players = game.Players;
        var total = players.Count;

        if (total == 0)
        {
            return Array.Empty<Player>();
        }

        // Find the index of the source player
        var sourceIndex = -1;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].Seat == sourcePlayer.Seat)
            {
                sourceIndex = i;
                break;
            }
        }

        if (sourceIndex < 0)
        {
            return Array.Empty<Player>();
        }

        // Collect alive players in turn order starting from source player
        var result = new List<Player>();
        for (int i = 0; i < total; i++)
        {
            var index = (sourceIndex + i) % total;
            var player = players[index];
            if (player.IsAlive)
            {
                result.Add(player);
            }
        }

        return result;
    }

    /// <summary>
    /// Result of pool creation operation.
    /// </summary>
    private sealed record PoolCreationResult
    {
        public bool IsSuccess { get; init; }
        public Zone? PoolZone { get; init; }
        public ResolutionResult? FailureResult { get; init; }

        public static PoolCreationResult CreateSuccess(Zone poolZone)
        {
            return new PoolCreationResult { IsSuccess = true, PoolZone = poolZone };
        }

        public static PoolCreationResult CreateFailure(ResolutionResult failureResult)
        {
            return new PoolCreationResult { IsSuccess = false, FailureResult = failureResult };
        }
    }
}

/// <summary>
/// Handler resolver for Harvest target that checks nullification result
/// and applies the card gain effect if not nullified.
/// </summary>
internal sealed class HarvestTargetHandlerResolver : IResolver
{
    private readonly Player _target;
    private readonly Zone _pool;
    private readonly IReadOnlyList<Player> _targets;
    private readonly int _currentIndex;

    public HarvestTargetHandlerResolver(Player target, Zone pool, IReadOnlyList<Player> targets, int currentIndex)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        _targets = targets ?? throw new ArgumentNullException(nameof(targets));
        _currentIndex = currentIndex;
    }

    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        // For source player, nullification window is used to nullify other players' gains,
        // not their own gain. So source player should always gain a card.
        // For other players, check nullification result.
        bool isSourcePlayer = _target.Seat == context.SourcePlayer.Seat;
        
        if (!isSourcePlayer)
        {
            // Check nullification result for non-source players
            var nullificationResultKey = $"HarvestNullification_{_target.Seat}";
            if (context.IntermediateResults is not null &&
                context.IntermediateResults.TryGetValue(nullificationResultKey, out var resultObj) &&
                resultObj is NullificationResult nullificationResult)
            {
                if (nullificationResult.IsNullified)
                {
                    // Effect was nullified, skip execution
                    if (context.LogSink is not null)
                    {
                        context.LogSink.Log(new LogEntry
                        {
                            EventType = "HarvestNullified",
                            Level = "Info",
                            Message = $"Harvest effect on player {_target.Seat} was nullified",
                            Data = new
                            {
                                TargetPlayerSeat = _target.Seat,
                                NullificationCount = nullificationResult.NullificationCount
                            }
                        });
                    }

                    // Move to next target
                    MoveToNextTarget(context);
                    return ResolutionResult.SuccessResult;
                }
            }
        }

        // Effect was not nullified, apply the effect
        var game = context.Game;

        // Check if pool is still available and has cards
        if (_pool.Cards is null || _pool.Cards.Count == 0)
        {
            // Pool is empty, move to next target (which will trigger cleanup)
            MoveToNextTarget(context);
            return ResolutionResult.SuccessResult;
        }

        // Request target to select a card from pool
        if (context.GetPlayerChoice is null)
        {
            // Auto-select first card if no choice function available
            var firstCard = _pool.Cards.FirstOrDefault();
            if (firstCard is not null)
            {
                GainCard(context, game, firstCard);
            }
            MoveToNextTarget(context);
            return ResolutionResult.SuccessResult;
        }

        var selectableCards = _pool.Cards.ToList();
        var choiceRequest = new ChoiceRequest(
            RequestId: Guid.NewGuid().ToString("N"),
            PlayerSeat: _target.Seat,
            ChoiceType: ChoiceType.SelectCards,
            TargetConstraints: null,
            AllowedCards: selectableCards,
            ResponseWindowId: null,
            CanPass: false  // Must select one card
        );

        try
        {
            var choiceResult = context.GetPlayerChoice(choiceRequest);
            if (choiceResult?.SelectedCardIds is null || choiceResult.SelectedCardIds.Count == 0)
            {
                // No card selected, move to next target
                MoveToNextTarget(context);
                return ResolutionResult.SuccessResult;
            }

            var selectedCardId = choiceResult.SelectedCardIds[0];
            var selectedCard = selectableCards.FirstOrDefault(c => c.Id == selectedCardId);

            if (selectedCard is null)
            {
                // Invalid card selected, move to next target
                MoveToNextTarget(context);
                return ResolutionResult.SuccessResult;
            }

            // Gain the selected card
            GainCard(context, game, selectedCard);

            // Move to next target
            MoveToNextTarget(context);
            return ResolutionResult.SuccessResult;
        }
        catch
        {
            // If getting choice fails, move to next target
            MoveToNextTarget(context);
            return ResolutionResult.SuccessResult;
        }
    }

    /// <summary>
    /// Moves the selected card from pool to target's hand.
    /// </summary>
    private void GainCard(ResolutionContext context, Game game, Card card)
    {
        if (_target.HandZone is not Zone handZone)
        {
            return;
        }

        try
        {
            var moveDescriptor = new CardMoveDescriptor(
                SourceZone: _pool,
                TargetZone: handZone,
                Cards: new[] { card },
                Reason: CardMoveReason.Draw, // Using Draw reason for gaining cards
                Ordering: CardMoveOrdering.ToTop,
                Game: game
            );

            context.CardMoveService.MoveSingle(moveDescriptor);

            // Log the effect if log sink is available
            if (context.LogSink is not null)
            {
                context.LogSink.Log(new LogEntry
                {
                    EventType = "HarvestCardGained",
                    Level = "Info",
                    Message = $"Player {_target.Seat} gained card {card.Id} from Harvest pool",
                    Data = new
                    {
                        TargetPlayerSeat = _target.Seat,
                        CardId = card.Id,
                        CardSubType = card.CardSubType
                    }
                });
            }
        }
        catch (Exception ex)
        {
            // If moving fails, log but continue
            if (context.LogSink is not null)
            {
                context.LogSink.Log(new LogEntry
                {
                    EventType = "HarvestCardGainFailed",
                    Level = "Error",
                    Message = $"Failed to gain card {card.Id} for player {_target.Seat}: {ex.Message}",
                    Data = new
                    {
                        TargetPlayerSeat = _target.Seat,
                        CardId = card.Id,
                        Exception = ex.Message
                    }
                });
            }
        }
    }

    /// <summary>
    /// Moves to the next target by pushing the main resolver back onto the stack.
    /// </summary>
    private void MoveToNextTarget(ResolutionContext context)
    {
        var intermediateResults = context.IntermediateResults ?? new Dictionary<string, object>();
        intermediateResults["HarvestCurrentTargetIndex"] = _currentIndex + 1;

        var nextContext = new ResolutionContext(
            context.Game,
            context.SourcePlayer,
            context.Action,
            context.Choice,
            context.Stack,
            context.CardMoveService,
            context.RuleService,
            context.PendingDamage,
            context.LogSink,
            context.GetPlayerChoice,
            intermediateResults,
            context.EventBus,
            context.LogCollector,
            context.SkillManager,
            context.EquipmentSkillRegistry,
            context.JudgementService
        );

        // Push main resolver back to process next target
        context.Stack.Push(new HarvestResolver(), nextContext);
    }
}
