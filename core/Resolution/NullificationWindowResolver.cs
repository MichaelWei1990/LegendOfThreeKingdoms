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
        // Check if this is a chain nullification window by checking if resultKey contains "_Chain_"
        // For chain windows, we need to extract the original result key to find the chain count
        string originalResultKey = _resultKey;
        if (_resultKey.Contains("_Chain_"))
        {
            // Extract original result key: "JieDaoShaRenNullification_1_Chain_1" -> "JieDaoShaRenNullification_1"
            var lastChainIndex = _resultKey.LastIndexOf("_Chain_");
            if (lastChainIndex > 0)
            {
                originalResultKey = _resultKey.Substring(0, lastChainIndex);
            }
        }

        // Check if there's an existing nullification count for this effect
        var chainKey = $"{originalResultKey}_ChainCount";
        if (context.IntermediateResults is not null &&
            context.IntermediateResults.TryGetValue(chainKey, out var countObj) &&
            countObj is int existingCount)
        {
            // This is a chain nullification window (not the first one)
            // Increment the count if a nullification was played
            if (responseResult.State == ResponseWindowState.ResponseSuccess &&
                responseResult.ResponseCard?.CardSubType == CardSubType.Wuxiekeji)
            {
                return existingCount + 1;
            }
            // If no nullification was played in the chain window, keep the existing count
            // The chain handler will process this result
            return existingCount;
        }

        // First nullification in the chain (not a chain window)
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

        // Check if the chain nullification window has completed
        // Look for the latest chain result (the chain window result key is "{_resultKey}_Chain_{currentCount}")
        var latestChainKey = $"{_resultKey}_Chain_{currentCount}";
        if (context.IntermediateResults is not null &&
            context.IntermediateResults.TryGetValue(latestChainKey, out var chainResultObj) &&
            chainResultObj is NullificationResult chainResult)
        {
            // Chain window has completed
            // If another nullification was played in the chain, increment count
            if (chainResult.IsNullified)
            {
                currentCount++;
                if (context.IntermediateResults is not null)
                {
                    context.IntermediateResults[_chainKey] = currentCount;
                }
            }
            // If chain window had no response (IsNullified = false), keep currentCount as is
            // This means the first nullification stands, so effect is nullified (odd count = nullified)
        }
        // If chain result doesn't exist yet, the chain window hasn't executed yet
        // In this case, use the previous count (which should be the first nullification count)

        // Calculate final result based on current count
        // Odd count (1, 3, 5...) = nullified, even count (0, 2, 4...) = not nullified
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
