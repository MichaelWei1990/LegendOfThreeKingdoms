using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Judgement;
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
    private readonly IDelayedTrickEffectResolver? _effectResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelayedTrickJudgementResolver"/> class.
    /// </summary>
    /// <param name="delayedTrickCard">The delayed trick card to judge and resolve.</param>
    public DelayedTrickJudgementResolver(Card delayedTrickCard)
    {
        _delayedTrickCard = delayedTrickCard ?? throw new ArgumentNullException(nameof(delayedTrickCard));
        _effectResolver = CreateEffectResolver(delayedTrickCard);
    }

    /// <summary>
    /// Creates the appropriate effect resolver for the given delayed trick card.
    /// </summary>
    private static IDelayedTrickEffectResolver? CreateEffectResolver(Card card)
    {
        return card.CardSubType switch
        {
            CardSubType.Lebusishu => new LebusishuResolver(),
            CardSubType.Shandian => new ShandianResolver(),
            _ => null // Generic delayed trick or unknown type
        };
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

        // Get judgement rule from effect resolver, or use default for generic delayed trick
        IJudgementRule judgementRule = _effectResolver?.JudgementRule 
            ?? (_delayedTrickCard.CardSubType == CardSubType.DelayedTrick 
                ? new RedJudgementRule() 
                : throw new InvalidOperationException($"Unknown delayed trick subtype: {_delayedTrickCard.CardSubType}"));

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
        var intermediateResults = context.IntermediateResults != null
            ? new Dictionary<string, object>(context.IntermediateResults)
            : new Dictionary<string, object>();
        intermediateResults["JudgementRequest"] = judgementRequest;

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
            intermediateResults,
            context.EventBus,
            context.LogCollector,
            context.SkillManager,
            context.EquipmentSkillRegistry,
            judgementService
        );

        // Push handler resolver to process judgement result and apply effects (push first so it executes after JudgementResolver)
        // Use the same intermediateResults dictionary so DelayedTrickEffectResolver can access JudgementResult
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
            intermediateResults, // Use the same intermediateResults so JudgementResult is accessible
            context.EventBus,
            context.LogCollector,
            context.SkillManager,
            context.EquipmentSkillRegistry,
            judgementService
        );

        context.Stack.Push(new DelayedTrickEffectResolver(_delayedTrickCard, _effectResolver), handlerContext);

        // Push JudgementResolver to execute the judgement (push last so it executes first)
        context.Stack.Push(new JudgementResolver(), judgementContext);

        return ResolutionResult.SuccessResult;
    }
}

/// <summary>
/// Resolver that applies the effect of a delayed trick based on judgement result.
/// </summary>
internal sealed class DelayedTrickEffectResolver : IResolver
{
    private readonly Card _delayedTrickCard;
    private readonly IDelayedTrickEffectResolver? _effectResolver;

    public DelayedTrickEffectResolver(Card delayedTrickCard, IDelayedTrickEffectResolver? effectResolver)
    {
        _delayedTrickCard = delayedTrickCard ?? throw new ArgumentNullException(nameof(delayedTrickCard));
        _effectResolver = effectResolver;
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
        // For Lebusishu: 红桃 = 判定成功 = 无效果（正常进行回合），非红桃 = 判定失败 = 跳过出牌阶段
        // For Shandian: 黑桃2-9 = 判定成功 = 受到3点雷电伤害，其他 = 判定失败 = 移动到下家
        if (judgementResult.IsSuccess)
        {
            // Judgement succeeded - apply success effect
            ApplyJudgementSuccessEffect(context, game, judgeOwner, cardInZone);
        }
        else
        {
            // Judgement failed - apply failure effect
            ApplyJudgementFailureEffect(context, game, judgeOwner, cardInZone);
        }

        // Complete the judgement: move card from judgement zone to discard pile
        // Exception: For Shandian on failure, the card is moved to next player's judgement zone,
        // so we skip CompleteJudgement in that case
        var shouldCompleteJudgement = !(cardInZone.CardSubType == CardSubType.Shandian && !judgementResult.IsSuccess);
        
        if (shouldCompleteJudgement)
        {
            var judgementService = context.JudgementService ?? new BasicJudgementService(context.EventBus);
            judgementService.CompleteJudgement(game, judgeOwner, cardInZone, context.CardMoveService);
        }

        return ResolutionResult.SuccessResult;
    }

    private void ApplyJudgementSuccessEffect(ResolutionContext context, Game game, Player judgeOwner, Card card)
    {
        // Judgement succeeded - apply effect using resolver if available
        if (_effectResolver is not null)
        {
            _effectResolver.ApplySuccessEffect(context, game, judgeOwner);
        }
        else
        {
            // Generic delayed trick - no specific effect yet
        }
    }

    private void ApplyJudgementFailureEffect(ResolutionContext context, Game game, Player judgeOwner, Card card)
    {
        // Judgement failed - apply failure effect using resolver if available
        if (_effectResolver is not null)
        {
            _effectResolver.ApplyFailureEffect(context, game, judgeOwner, card);
        }
        else
        {
            // Generic delayed trick - no negative effect on failure
            if (context.LogSink is not null)
            {
                var logEntry = new LogEntry
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
