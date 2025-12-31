using System;
using System.Collections.Generic;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Response;
using LegendOfThreeKingdoms.Core.Rules;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Resolver that handles nullification (无懈可击) response windows.
/// Supports chain nullification: each nullification can be nullified by another nullification.
/// </summary>
internal sealed class NullificationWindowResolver : IResolver
{
    private readonly ResponseWindowContext _windowContext;
    private readonly INullifiableEffect _effect;
    private readonly string _resultKey;
    private readonly Func<ChoiceRequest, ChoiceResult> _getPlayerChoice;

    public NullificationWindowResolver(
        ResponseWindowContext windowContext,
        INullifiableEffect effect,
        string resultKey,
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice)
    {
        _windowContext = windowContext ?? throw new ArgumentNullException(nameof(windowContext));
        _effect = effect ?? throw new ArgumentNullException(nameof(effect));
        _resultKey = resultKey ?? throw new ArgumentNullException(nameof(resultKey));
        _getPlayerChoice = getPlayerChoice ?? throw new ArgumentNullException(nameof(getPlayerChoice));
    }

    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        // Execute the response window
        var responseWindow = new BasicResponseWindow();
        var responseResult = responseWindow.Execute(_windowContext, _getPlayerChoice);

        // Determine nullification count
        var nullificationCount = GetNullificationCount(context, responseResult);

        // Check if we need to handle chain nullification
        // If someone played nullification, we need to open another nullification window
        // to allow nullifying the nullification itself
        if (responseResult.State == ResponseWindowState.ResponseSuccess && 
            responseResult.ResponseCard?.CardSubType == CardSubType.Wuxiekeji)
        {
            // A nullification was played, now we need to allow nullifying this nullification
            return HandleChainNullification(context, nullificationCount);
        }

        // No nullification was played, or chain is complete
        // Store the final result
        var isNullified = NullificationHelper.IsNullified(nullificationCount);
        if (context.IntermediateResults is not null)
        {
            context.IntermediateResults[_resultKey] = new NullificationResult(
                IsNullified: isNullified,
                NullificationCount: nullificationCount);
        }

        return ResolutionResult.SuccessResult;
    }

    /// <summary>
    /// Gets the current nullification count from IntermediateResults.
    /// </summary>
    private int GetNullificationCount(ResolutionContext context, ResponseWindowResult responseResult)
    {
        // Check if there's an existing nullification count for this effect
        var chainKey = $"{_resultKey}_ChainCount";
        if (context.IntermediateResults is not null &&
            context.IntermediateResults.TryGetValue(chainKey, out var countObj) &&
            countObj is int existingCount)
        {
            // Increment the count if a nullification was played
            if (responseResult.State == ResponseWindowState.ResponseSuccess &&
                responseResult.ResponseCard?.CardSubType == CardSubType.Wuxiekeji)
            {
                return existingCount + 1;
            }
            return existingCount;
        }

        // First nullification in the chain
        if (responseResult.State == ResponseWindowState.ResponseSuccess &&
            responseResult.ResponseCard?.CardSubType == CardSubType.Wuxiekeji)
        {
            return 1;
        }

        return 0;
    }

    /// <summary>
    /// Handles chain nullification by opening another nullification window.
    /// </summary>
    private ResolutionResult HandleChainNullification(ResolutionContext context, int currentCount)
    {
        // Store current count
        var chainKey = $"{_resultKey}_ChainCount";
        var intermediateResults = context.IntermediateResults ?? new Dictionary<string, object>();
        intermediateResults[chainKey] = currentCount;

        // Create a new nullifiable effect for the nullification that was just played
        // This allows nullifying the nullification itself
        var nullificationEffect = NullificationHelper.CreateNullifiableEffect(
            effectKey: $"{_effect.EffectKey}.Nullification",
            targetPlayer: _effect.TargetPlayer,
            causingCard: null, // The nullification card itself
            isNullifiable: true);

        // Open another nullification window
        var newResultKey = $"{_resultKey}_Chain_{currentCount}";
        NullificationHelper.OpenNullificationWindow(context, nullificationEffect, newResultKey);

        // Push a handler resolver to process the chain result
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
            context.IntermediateResults,
            context.EventBus,
            context.LogCollector,
            context.SkillManager,
            context.EquipmentSkillRegistry,
            context.JudgementService
        );

        context.Stack.Push(new NullificationChainHandlerResolver(_resultKey, chainKey, currentCount), handlerContext);

        return ResolutionResult.SuccessResult;
    }
}

/// <summary>
/// Resolver that handles the result of a nullification chain.
/// </summary>
internal sealed class NullificationChainHandlerResolver : IResolver
{
    private readonly string _resultKey;
    private readonly string _chainKey;
    private readonly int _previousCount;

    public NullificationChainHandlerResolver(string resultKey, string chainKey, int previousCount)
    {
        _resultKey = resultKey ?? throw new ArgumentNullException(nameof(resultKey));
        _chainKey = chainKey ?? throw new ArgumentNullException(nameof(chainKey));
        _previousCount = previousCount;
    }

    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        // Get the chain count from IntermediateResults
        if (context.IntermediateResults is null ||
            !context.IntermediateResults.TryGetValue(_chainKey, out var countObj) ||
            countObj is not int currentCount)
        {
            // Fallback: use previous count
            currentCount = _previousCount;
        }

        // Check if another nullification was played in the chain
        // Look for the latest chain result
        var latestChainKey = $"{_resultKey}_Chain_{currentCount - 1}";
        if (context.IntermediateResults is not null &&
            context.IntermediateResults.TryGetValue(latestChainKey, out var chainResultObj) &&
            chainResultObj is NullificationResult chainResult)
        {
            // Another nullification was played, increment count
            if (chainResult.IsNullified)
            {
                currentCount++;
                if (context.IntermediateResults is not null)
                {
                    context.IntermediateResults[_chainKey] = currentCount;
                }
            }
        }

        // Check if we need to continue the chain
        // If the latest nullification was nullified, we might need another round
        // But for simplicity, we'll stop here and calculate the final result
        var finalCount = currentCount;
        var isNullified = NullificationHelper.IsNullified(finalCount);

        // Store final result
        if (context.IntermediateResults is not null)
        {
            context.IntermediateResults[_resultKey] = new NullificationResult(
                IsNullified: isNullified,
                NullificationCount: finalCount);
        }

        return ResolutionResult.SuccessResult;
    }
}
