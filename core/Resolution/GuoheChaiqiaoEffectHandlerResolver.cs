using System;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Handler resolver for Guohe Chaiqiao effect that checks nullification result
/// and applies the effect if not nullified.
/// </summary>
internal sealed class GuoheChaiqiaoEffectHandlerResolver : IResolver
{
    private readonly Player _target;
    private readonly Card _cardToDiscard;
    private readonly IZone _sourceZone;

    public GuoheChaiqiaoEffectHandlerResolver(Player target, Card cardToDiscard, IZone sourceZone)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _cardToDiscard = cardToDiscard ?? throw new ArgumentNullException(nameof(cardToDiscard));
        _sourceZone = sourceZone ?? throw new ArgumentNullException(nameof(sourceZone));
    }

    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        // Check nullification result
        var nullificationResultKey = $"GuoheChaiqiaoNullification_{_target.Seat}";
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
                        EventType = "GuoheChaiqiaoNullified",
                        Level = "Info",
                        Message = $"Guohe Chaiqiao effect on player {_target.Seat} was nullified",
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
                TargetZone: game.DiscardPile,
                Cards: new[] { _cardToDiscard },
                Reason: CardMoveReason.Discard,
                Ordering: CardMoveOrdering.ToTop,
                Game: game
            );

            context.CardMoveService.MoveSingle(moveDescriptor);

            // Log the effect if log sink is available
            if (context.LogSink is not null)
            {
                context.LogSink.Log(new LogEntry
                {
                    EventType = "GuoheChaiqiaoEffect",
                    Level = "Info",
                    Message = $"Player {context.SourcePlayer.Seat} discarded card {_cardToDiscard.Id} from player {_target.Seat}",
                    Data = new
                    {
                        SourcePlayerSeat = context.SourcePlayer.Seat,
                        TargetPlayerSeat = _target.Seat,
                        CardId = _cardToDiscard.Id,
                        CardSubType = _cardToDiscard.CardSubType
                    }
                });
            }

            return ResolutionResult.SuccessResult;
        }
        catch (Exception ex)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.guohechaiqiao.cardMoveFailed",
                details: new { Exception = ex.Message });
        }
    }
}
