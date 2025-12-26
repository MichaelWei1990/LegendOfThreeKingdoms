using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Resolver for Lebusishu (乐不思蜀) delayed trick card effects.
/// Handles the application of Lebusishu effects based on judgement results.
/// </summary>
internal sealed class LebusishuResolver
{
    /// <summary>
    /// Applies the effect when judgement succeeds (Heart suit).
    /// For Lebusishu: Judgement success means no negative effect, player proceeds normally.
    /// </summary>
    /// <param name="context">The resolution context.</param>
    /// <param name="judgeOwner">The player who owns the judgement.</param>
    public static void ApplySuccessEffect(ResolutionContext context, Player judgeOwner)
    {
        // 乐不思蜀：判定成功（红桃），正常进行回合，无负面效果
        if (context.LogSink is not null)
        {
            var logEntry = new LogEntry
            {
                EventType = "DelayedTrickEffect",
                Level = "Info",
                Message = $"Player {judgeOwner.Seat} avoided Lebusishu effect (Heart judgement)",
                Data = new
                {
                    PlayerSeat = judgeOwner.Seat,
                    CardSubType = "Lebusishu",
                    JudgementSuccess = true
                }
            };
            context.LogSink.Log(logEntry);
        }
    }

    /// <summary>
    /// Applies the effect when judgement fails (non-Heart suit).
    /// For Lebusishu: Judgement failure means skip play phase.
    /// </summary>
    /// <param name="context">The resolution context.</param>
    /// <param name="judgeOwner">The player who owns the judgement.</param>
    public static void ApplyFailureEffect(ResolutionContext context, Player judgeOwner)
    {
        // 乐不思蜀：判定失败（非红桃），跳过出牌阶段
        judgeOwner.Flags["SkipPlayPhase"] = true;
        if (context.LogSink is not null)
        {
            var logEntry = new LogEntry
            {
                EventType = "DelayedTrickEffect",
                Level = "Info",
                Message = $"Player {judgeOwner.Seat} will skip play phase due to Lebusishu (non-Heart judgement)",
                Data = new
                {
                    PlayerSeat = judgeOwner.Seat,
                    CardSubType = "Lebusishu",
                    JudgementSuccess = false
                }
            };
            context.LogSink.Log(logEntry);
        }
    }
}
