using System;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Handler resolver for Shunshou Qianyang effect that checks nullification result
/// and applies the effect if not nullified.
/// </summary>
internal sealed class ShunshouQianyangEffectHandlerResolver : IResolver
{
    private readonly Player _target;
    private readonly Card _cardToObtain;
    private readonly IZone _sourceZone;
    private readonly Player _sourcePlayer;

    public ShunshouQianyangEffectHandlerResolver(Player target, Card cardToObtain, IZone sourceZone, Player sourcePlayer)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _cardToObtain = cardToObtain ?? throw new ArgumentNullException(nameof(cardToObtain));
        _sourceZone = sourceZone ?? throw new ArgumentNullException(nameof(sourceZone));
        _sourcePlayer = sourcePlayer ?? throw new ArgumentNullException(nameof(sourcePlayer));
    }

    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        // Check nullification result
        var nullificationResultKey = $"ShunshouQianyangNullification_{_target.Seat}";
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
                        EventType = "ShunshouQianyangNullified",
                        Level = "Info",
                        Message = $"Shunshou Qianyang effect on player {_target.Seat} was nullified",
                        Data = new
                        {
                            TargetPlayerSeat = _target.Seat,
                            NullificationCount = nullificationResult.NullificationCount
                        }
                    });
                }
                return ResolutionResult.SuccessResult;
            }
        }

        // Effect was not nullified, apply the effect
        var game = context.Game;
        try
        {
            var moveDescriptor = new CardMoveDescriptor(
                SourceZone: _sourceZone,
                TargetZone: _sourcePlayer.HandZone,
                Cards: new[] { _cardToObtain },
                Reason: CardMoveReason.Play,  // Using Play as there's no Steal reason yet
                Ordering: CardMoveOrdering.ToTop,
                Game: game
            );

            context.CardMoveService.MoveSingle(moveDescriptor);

            // Log the effect if log sink is available
            if (context.LogSink is not null)
            {
                context.LogSink.Log(new LogEntry
                {
                    EventType = "ShunshouQianyangEffect",
                    Level = "Info",
                    Message = $"Player {_sourcePlayer.Seat} obtained card {_cardToObtain.Id} from player {_target.Seat}",
                    Data = new
                    {
                        SourcePlayerSeat = _sourcePlayer.Seat,
                        TargetPlayerSeat = _target.Seat,
                        CardId = _cardToObtain.Id,
                        CardSubType = _cardToObtain.CardSubType
                    }
                });
            }

            return ResolutionResult.SuccessResult;
        }
        catch (Exception ex)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.shunshouqianyang.cardMoveFailed",
                details: new { Exception = ex.Message });
        }
    }
}
