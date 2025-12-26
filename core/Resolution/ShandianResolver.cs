using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Judgement;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Resolver for Shandian (闪电) delayed trick card effects.
/// Handles the application of Shandian effects based on judgement results.
/// </summary>
internal sealed class ShandianResolver : IDelayedTrickEffectResolver
{
    /// <summary>
    /// Gets the judgement rule for Shandian.
    /// Shandian succeeds when the judgement card is Spade with rank 2-9.
    /// </summary>
    public IJudgementRule JudgementRule { get; } = new CompositeJudgementRule(
        new IJudgementRule[]
        {
            new SuitJudgementRule(Suit.Spade), // 黑桃
            new RankRangeJudgementRule(2, 9)  // 2-9点
        },
        JudgementRuleOperator.And);

    /// <summary>
    /// Applies the effect when judgement succeeds (Spade 2-9).
    /// For Shandian: Judgement success means take 3 thunder damage.
    /// </summary>
    /// <param name="context">The resolution context.</param>
    /// <param name="game">The game state.</param>
    /// <param name="judgeOwner">The player who owns the judgement.</param>
    public void ApplySuccessEffect(ResolutionContext context, Game game, Player judgeOwner)
    {
        // 闪电：判定成功（黑桃2-9），受到3点雷电伤害
        var damage = new DamageDescriptor(
            SourceSeat: 0, // Use 0 for delayed trick damage (no specific source player)
            TargetSeat: judgeOwner.Seat,
            Amount: 3,
            Type: DamageType.Thunder,
            Reason: "Shandian"
        );

        var damageContext = new ResolutionContext(
            game,
            judgeOwner,
            context.Action,
            context.Choice,
            context.Stack,
            context.CardMoveService,
            context.RuleService,
            PendingDamage: damage,
            LogSink: context.LogSink,
            context.GetPlayerChoice,
            context.IntermediateResults,
            context.EventBus,
            context.LogCollector,
            context.SkillManager,
            context.EquipmentSkillRegistry,
            context.JudgementService
        );

        context.Stack.Push(new DamageResolver(), damageContext);
    }

    /// <summary>
    /// Applies the effect when judgement fails (non-Spade 2-9).
    /// For Shandian: Judgement failure means move the card to the next alive player's judgement zone.
    /// </summary>
    /// <param name="context">The resolution context.</param>
    /// <param name="game">The game state.</param>
    /// <param name="judgeOwner">The player who owns the judgement.</param>
    /// <param name="card">The delayed trick card.</param>
    public void ApplyFailureEffect(ResolutionContext context, Game game, Player judgeOwner, Card card)
    {
        // 闪电：判定失败（非黑桃2-9），移动到下家角色的判定区
        var nextPlayer = FindNextAlivePlayer(game, judgeOwner);
        
        if (nextPlayer is null)
        {
            // No next alive player, discard the card
            if (context.LogSink is not null)
            {
                var logEntry = new LogEntry
                {
                    EventType = "DelayedTrickEffect",
                    Level = "Info",
                    Message = $"No next alive player found, Shandian discarded",
                    Data = new
                    {
                        CurrentOwnerSeat = judgeOwner.Seat,
                        CardSubType = "Shandian",
                        JudgementSuccess = false
                    }
                };
                context.LogSink.Log(logEntry);
            }
            return;
        }

        // Move card from current owner's judgement zone to next player's judgement zone
        try
        {
            var moveDescriptor = new CardMoveDescriptor(
                SourceZone: judgeOwner.JudgementZone,
                TargetZone: nextPlayer.JudgementZone,
                Cards: new[] { card },
                Reason: CardMoveReason.Judgement,
                Ordering: CardMoveOrdering.ToTop,
                Game: game
            );

            context.CardMoveService.MoveSingle(moveDescriptor);

            if (context.LogSink is not null)
            {
                var logEntry = new LogEntry
                {
                    EventType = "DelayedTrickEffect",
                    Level = "Info",
                    Message = $"Shandian moved from player {judgeOwner.Seat} to player {nextPlayer.Seat} (non-Spade 2-9 judgement)",
                    Data = new
                    {
                        CurrentOwnerSeat = judgeOwner.Seat,
                        NextOwnerSeat = nextPlayer.Seat,
                        CardSubType = "Shandian",
                        JudgementSuccess = false
                    }
                };
                context.LogSink.Log(logEntry);
            }
        }
        catch (System.Exception ex)
        {
            if (context.LogSink is not null)
            {
                var logEntry = new LogEntry
                {
                    EventType = "DelayedTrickEffect",
                    Level = "Error",
                    Message = $"Failed to move Shandian to next player: {ex.Message}",
                    Data = new
                    {
                        CurrentOwnerSeat = judgeOwner.Seat,
                        CardSubType = "Shandian",
                        Exception = ex.Message
                    }
                };
                context.LogSink.Log(logEntry);
            }
        }
    }

    /// <summary>
    /// Finds the next alive player in turn order after the current player.
    /// </summary>
    private static Player? FindNextAlivePlayer(Game game, Player currentPlayer)
    {
        if (game is null) throw new System.ArgumentNullException(nameof(game));
        if (currentPlayer is null) throw new System.ArgumentNullException(nameof(currentPlayer));

        var players = game.Players;
        var total = players.Count;

        if (total == 0)
        {
            return null;
        }

        // Find the index of the current player
        var currentIndex = -1;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].Seat == currentPlayer.Seat)
            {
                currentIndex = i;
                break;
            }
        }

        if (currentIndex < 0)
        {
            return null;
        }

        // Search for the next alive player starting from currentIndex + 1
        for (int i = 1; i <= total; i++)
        {
            var index = (currentIndex + i) % total;
            var player = players[index];
            if (player.IsAlive)
            {
                return player;
            }
        }

        // No next alive player found
        return null;
    }
}
