using System;
using System.Linq;
using LegendOfThreeKingdoms.Core.Judgement;
using LegendOfThreeKingdoms.Core.Logging;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Tricks;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Resolver for executing judgement and resolution of delayed tricks during the judge phase.
/// Handles:
/// 1. Retrieving delayed tricks from the player's judgement zone
/// 2. Creating and executing judgement requests
/// 3. Applying effects based on judgement results
/// 4. Moving cards from judgement zone to discard pile after resolution
/// </summary>
public sealed class DelayedTrickJudgementResolver : IResolver
{
    private readonly Card _delayedTrickCard;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelayedTrickJudgementResolver"/> class.
    /// </summary>
    /// <param name="delayedTrickCard">The delayed trick card to judge and resolve.</param>
    public DelayedTrickJudgementResolver(Card delayedTrickCard)
    {
        _delayedTrickCard = delayedTrickCard ?? throw new ArgumentNullException(nameof(delayedTrickCard));
    }

    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var game = context.Game;
        var judgeOwner = context.SourcePlayer;

        // Verify the card is still in the judgement zone
        if (!judgeOwner.JudgementZone.Cards.Contains(_delayedTrickCard))
        {
            // Card has been moved by another skill (e.g., Tiandu), skip judgement
            return ResolutionResult.SuccessResult;
        }

        // Get judgement service
        var judgementService = context.JudgementService ?? new BasicJudgementService(context.EventBus);

        // Create judgement rule based on card subtype
        IJudgementRule judgementRule = _delayedTrickCard.CardSubType switch
        {
            CardSubType.Lebusishu => new RedJudgementRule(), // 乐不思蜀：红色判定成功
            CardSubType.Shandian => new BlackJudgementRule(), // 闪电：黑色判定成功
            CardSubType.DelayedTrick => new RedJudgementRule(), // Default for generic delayed trick
            _ => throw new InvalidOperationException($"Unknown delayed trick subtype: {_delayedTrickCard.CardSubType}")
        };

        // Create effect source for the delayed trick
        var effectSource = new DelayedTrickEffectSource(_delayedTrickCard);

        // Create judgement request
        var judgementRequest = new JudgementRequest(
            JudgementId: Guid.NewGuid(),
            JudgeOwnerSeat: judgeOwner.Seat,
            Reason: JudgementReason.DelayedTrick,
            Source: effectSource,
            Rule: judgementRule,
            Tags: null,
            AllowModify: true
        );

        // Store judgement request in IntermediateResults for JudgementResolver
        if (context.IntermediateResults is null)
        {
            context.IntermediateResults = new System.Collections.Generic.Dictionary<string, object>();
        }
        context.IntermediateResults["JudgementRequest"] = judgementRequest;

        // Create new context for JudgementResolver
        var judgementContext = new ResolutionContext(
            game,
            judgeOwner,
            context.Action,
            context.Choice,
            context.Stack,
            context.CardMoveService,
            context.RuleService,
            context.PendingDamage,
            context.LogSink,
            context.GetPlayerChoice,
            context.IntermediateResults,
            context.EventBus,
            context.LogCollector,
            context.SkillManager,
            context.EquipmentSkillRegistry,
            judgementService
        );

        // Push JudgementResolver to execute the judgement
        context.Stack.Push(new JudgementResolver(), judgementContext);

        // Push handler resolver to process judgement result and apply effects
        var handlerContext = new ResolutionContext(
            game,
            judgeOwner,
            context.Action,
            context.Choice,
            context.Stack,
            context.CardMoveService,
            context.RuleService,
            context.PendingDamage,
            context.LogSink,
            context.GetPlayerChoice,
            context.IntermediateResults,
            context.EventBus,
            context.LogCollector,
            context.SkillManager,
            context.EquipmentSkillRegistry,
            judgementService
        );

        context.Stack.Push(new DelayedTrickEffectResolver(_delayedTrickCard), handlerContext);

        return ResolutionResult.SuccessResult;
    }
}

/// <summary>
/// Resolver that applies the effect of a delayed trick based on judgement result.
/// </summary>
internal sealed class DelayedTrickEffectResolver : IResolver
{
    private readonly Card _delayedTrickCard;

    public DelayedTrickEffectResolver(Card delayedTrickCard)
    {
        _delayedTrickCard = delayedTrickCard ?? throw new ArgumentNullException(nameof(delayedTrickCard));
    }

    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var game = context.Game;
        var judgeOwner = context.SourcePlayer;

        // Get judgement result from IntermediateResults
        if (context.IntermediateResults is null || !context.IntermediateResults.TryGetValue("JudgementResult", out var resultObj))
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.delayedtrick.noJudgementResult");
        }

        if (resultObj is not JudgementResult judgementResult)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.delayedtrick.invalidJudgementResult");
        }

        // Verify the card is still in the judgement zone
        var cardInZone = judgeOwner.JudgementZone.Cards.FirstOrDefault(c => c.Id == _delayedTrickCard.Id);
        if (cardInZone is null)
        {
            // Card has been moved by another skill, skip effect
            return ResolutionResult.SuccessResult;
        }

        // Apply effect based on judgement result and card type
        // For now, we only handle specific delayed tricks
        if (judgementResult.IsSuccess)
        {
            // Judgement succeeded - apply negative effect
            ApplyJudgementSuccessEffect(context, game, judgeOwner, cardInZone);
        }
        else
        {
            // Judgement failed - no effect or positive effect
            ApplyJudgementFailureEffect(context, game, judgeOwner, cardInZone);
        }

        // Complete the judgement: move card from judgement zone to discard pile
        var judgementService = context.JudgementService ?? new BasicJudgementService(context.EventBus);
        judgementService.CompleteJudgement(game, judgeOwner, cardInZone, context.CardMoveService);

        return ResolutionResult.SuccessResult;
    }

    private void ApplyJudgementSuccessEffect(ResolutionContext context, Game game, Player judgeOwner, Card card)
    {
        // Judgement succeeded - apply negative effect based on card type
        switch (card.CardSubType)
        {
            case CardSubType.Lebusishu:
                // 乐不思蜀：判定成功，跳过出牌阶段
                // TODO: Implement skip play phase effect
                if (context.LogSink is not null)
                {
                    var logEntry = new Logging.LogEntry
                    {
                        EventType = "DelayedTrickEffect",
                        Level = "Info",
                        Message = $"Player {judgeOwner.Seat} skipped play phase due to Lebusishu",
                        Data = new
                        {
                            PlayerSeat = judgeOwner.Seat,
                            CardSubType = card.CardSubType.ToString(),
                            JudgementSuccess = true
                        }
                    };
                    context.LogSink.Log(logEntry);
                }
                break;

            case CardSubType.Shandian:
                // 闪电：判定成功，受到3点雷电伤害
                var damage = new DamageDescriptor(
                    SourceSeat: -1, // No source player for delayed trick damage
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
                break;

            default:
                // Generic delayed trick - no specific effect yet
                break;
        }
    }

    private void ApplyJudgementFailureEffect(ResolutionContext context, Game game, Player judgeOwner, Card card)
    {
        // Judgement failed - no negative effect
        if (context.LogSink is not null)
        {
            var logEntry = new Logging.LogEntry
            {
                EventType = "DelayedTrickEffect",
                Level = "Info",
                Message = $"Player {judgeOwner.Seat} avoided delayed trick effect: {card.CardSubType}",
                Data = new
                {
                    PlayerSeat = judgeOwner.Seat,
                    CardSubType = card.CardSubType.ToString(),
                    JudgementSuccess = false
                }
            };
            context.LogSink.Log(logEntry);
        }
    }
}

/// <summary>
/// Effect source implementation for delayed tricks.
/// </summary>
internal sealed class DelayedTrickEffectSource : IEffectSource
{
    private readonly Card _card;

    public DelayedTrickEffectSource(Card card)
    {
        _card = card ?? throw new ArgumentNullException(nameof(card));
    }

    public string SourceId => $"DelayedTrick_{_card.Id}";

    public string SourceType => "DelayedTrick";

    public string? DisplayName => _card.Name;
}
