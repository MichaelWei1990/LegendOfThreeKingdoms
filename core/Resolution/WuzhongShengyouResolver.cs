using System;
using System.Linq;
using LegendOfThreeKingdoms.Core.Logging;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Resolver for Wuzhong Shengyou (无中生有) immediate trick card.
/// Effect: The user draws 2 cards.
/// </summary>
public sealed class WuzhongShengyouResolver : IResolver
{
    private const int DrawCount = 2;

    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var game = context.Game;
        var sourcePlayer = context.SourcePlayer;

        // Draw 2 cards for the user
        try
        {
            var drawnCards = context.CardMoveService.DrawCards(game, sourcePlayer, DrawCount);

            // Log to log sink if available
            if (context.LogSink is not null)
            {
                var logEntry = new LogEntry
                {
                    EventType = "WuzhongShengyouEffect",
                    Level = "Info",
                    Message = $"Player {sourcePlayer.Seat} drew {drawnCards.Count} card(s) from Wuzhong Shengyou",
                    Data = new
                    {
                        SourcePlayerSeat = sourcePlayer.Seat,
                        DrawCount = drawnCards.Count,
                        DrawnCardIds = drawnCards.Select(c => c.Id).ToArray()
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
                messageKey: "resolution.wuzhongshengyou.drawFailed",
                details: new { Exception = ex.Message });
        }
    }
}
