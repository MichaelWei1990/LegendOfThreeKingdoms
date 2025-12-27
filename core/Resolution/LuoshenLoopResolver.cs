using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Judgement;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Resolver for Luoshen (洛神) skill's judgement loop.
/// Handles the repeated judgement process:
/// - If result is black: obtain the card and continue
/// - If result is red: stop the loop
/// </summary>
public sealed class LuoshenLoopResolver : IResolver
{
    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        var game = context.Game;
        var owner = context.SourcePlayer;

        // Get required services
        var cardMoveService = context.CardMoveService;
        var judgementService = context.JudgementService ?? new BasicJudgementService(context.EventBus);
        var getPlayerChoice = context.GetPlayerChoice;

        if (cardMoveService is null || getPlayerChoice is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.luoshen.missingServices",
                details: new { Message = "Required services (CardMoveService or GetPlayerChoice) are missing" });
        }

        // Execute the judgement loop
        while (true)
        {
            // Check if draw pile has cards
            if (game.DrawPile.Cards.Count == 0)
            {
                // No cards in draw pile, stop the loop
                break;
            }

            // Create effect source for Luoshen
            var effectSource = new LuoshenEffectSource();

            // Create judgement request
            // Use BlackJudgementRule to check if card is black (but we'll check the result ourselves)
            var judgementRequest = new JudgementRequest(
                JudgementId: Guid.NewGuid(),
                JudgeOwnerSeat: owner.Seat,
                Reason: JudgementReason.Skill,
                Source: effectSource,
                Rule: new BlackJudgementRule(), // We'll check the color ourselves
                Tags: null,
                AllowModify: true // Allow modification (e.g., Guicai)
            );

            // Store judgement request in IntermediateResults
            var intermediateResults = context.IntermediateResults ?? new Dictionary<string, object>();
            intermediateResults["JudgementRequest"] = judgementRequest;

            // Create context for JudgementResolver
            var judgementContext = new ResolutionContext(
                game,
                owner,
                Action: null,
                Choice: null,
                context.Stack,
                cardMoveService,
                context.RuleService,
                context.PendingDamage,
                context.LogSink,
                getPlayerChoice,
                intermediateResults,
                context.EventBus,
                context.LogCollector,
                context.SkillManager,
                context.EquipmentSkillRegistry,
                judgementService);

            // Push JudgementResolver to execute the judgement
            context.Stack.Push(new JudgementResolver(), judgementContext);

            // Push a resolver to handle the result after judgement completes
            var resultHandlerContext = new ResolutionContext(
                game,
                owner,
                Action: null,
                Choice: null,
                context.Stack,
                cardMoveService,
                context.RuleService,
                context.PendingDamage,
                context.LogSink,
                getPlayerChoice,
                intermediateResults,
                context.EventBus,
                context.LogCollector,
                context.SkillManager,
                context.EquipmentSkillRegistry,
                judgementService);

            context.Stack.Push(new LuoshenResultHandlerResolver(), resultHandlerContext);

            // Break after pushing resolvers (they will execute and handle the loop continuation)
            break;
        }

        return ResolutionResult.SuccessResult;
    }
}

/// <summary>
/// Resolver that handles the result of a single Luoshen judgement and decides whether to continue the loop.
/// </summary>
internal sealed class LuoshenResultHandlerResolver : IResolver
{
    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        var game = context.Game;
        var owner = context.SourcePlayer;
        var cardMoveService = context.CardMoveService;
        var getPlayerChoice = context.GetPlayerChoice;

        if (cardMoveService is null || getPlayerChoice is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.luoshen.resultHandler.missingServices",
                details: new { Message = "Required services are missing" });
        }

        // Extract JudgementResult from IntermediateResults
        if (context.IntermediateResults is null || !context.IntermediateResults.TryGetValue("JudgementResult", out var resultObj))
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.luoshen.resultHandler.noResult",
                details: new { Message = "JudgementResult not found in IntermediateResults" });
        }

        if (resultObj is not JudgementResult judgementResult)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.luoshen.resultHandler.invalidResult",
                details: new { Message = "JudgementResult has invalid type" });
        }

        // Get the final judgement card (after modifications)
        var finalCard = judgementResult.FinalCard;

        // Check if the card is black
        var isBlack = finalCard.Suit.IsBlack();

        if (isBlack)
        {
            // Black card: obtain it and ask if player wants to continue
            // Move the card from JudgementZone to owner's hand
            try
            {
                var moveDescriptor = new CardMoveDescriptor(
                    SourceZone: owner.JudgementZone,
                    TargetZone: owner.HandZone,
                    Cards: new[] { finalCard },
                    Reason: CardMoveReason.Draw, // Use Draw reason for skill-obtained cards
                    Ordering: CardMoveOrdering.ToTop,
                    Game: game);

                cardMoveService.MoveSingle(moveDescriptor);
            }
            catch
            {
                // If moving fails, stop the loop
                return ResolutionResult.SuccessResult;
            }

            // Ask player if they want to continue
            var continueRequest = new ChoiceRequest(
                RequestId: Guid.NewGuid().ToString(),
                PlayerSeat: owner.Seat,
                ChoiceType: ChoiceType.Confirm,
                TargetConstraints: null,
                AllowedCards: null,
                ResponseWindowId: null,
                CanPass: true // Player can choose to stop
            );

            try
            {
                var continueResult = getPlayerChoice(continueRequest);
                if (continueResult?.Confirmed == true)
                {
                    // Player wants to continue: push another LuoshenLoopResolver
                    var loopContext = new ResolutionContext(
                        game,
                        owner,
                        Action: null,
                        Choice: null,
                        context.Stack,
                        cardMoveService,
                        context.RuleService,
                        context.PendingDamage,
                        context.LogSink,
                        getPlayerChoice,
                        context.IntermediateResults,
                        context.EventBus,
                        context.LogCollector,
                        context.SkillManager,
                        context.EquipmentSkillRegistry,
                        context.JudgementService);

                    context.Stack.Push(new LuoshenLoopResolver(), loopContext);
                }
            }
            catch
            {
                // If choice fails, stop the loop
            }
        }
        else
        {
            // Red card: move to discard pile and stop the loop
            try
            {
                var moveDescriptor = new CardMoveDescriptor(
                    SourceZone: owner.JudgementZone,
                    TargetZone: game.DiscardPile,
                    Cards: new[] { finalCard },
                    Reason: CardMoveReason.Discard,
                    Ordering: CardMoveOrdering.ToTop,
                    Game: game);

                cardMoveService.MoveSingle(moveDescriptor);
            }
            catch
            {
                // If moving fails, continue anyway
            }
        }

        return ResolutionResult.SuccessResult;
    }
}

/// <summary>
/// Effect source for Luoshen skill judgements.
/// </summary>
internal sealed class LuoshenEffectSource : IEffectSource
{
    /// <inheritdoc />
    public string SourceId => "luoshen";

    /// <inheritdoc />
    public string SourceType => "Skill";

    /// <inheritdoc />
    public string? DisplayName => "洛神";
}

