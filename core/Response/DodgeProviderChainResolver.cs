using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Resolution;

namespace LegendOfThreeKingdoms.Core.Response;

/// <summary>
/// Resolver that executes Dodge providers in priority order.
/// Stops at the first provider that successfully provides Dodge.
/// </summary>
public sealed class DodgeProviderChainResolver : IResolver
{
    private readonly DodgeRequestContext _requestContext;
    private readonly IReadOnlyList<IDodgeProvider> _providers;

    /// <summary>
    /// Creates a new DodgeProviderChainResolver.
    /// </summary>
    /// <param name="requestContext">The Dodge request context to resolve.</param>
    /// <param name="providers">The list of Dodge providers to try, in priority order.</param>
    public DodgeProviderChainResolver(
        DodgeRequestContext requestContext,
        IReadOnlyList<IDodgeProvider> providers)
    {
        _requestContext = requestContext ?? throw new ArgumentNullException(nameof(requestContext));
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
    }

    /// <summary>
    /// Creates a new DodgeProviderChainResolver with default providers.
    /// </summary>
    /// <param name="requestContext">The Dodge request context to resolve.</param>
    public DodgeProviderChainResolver(DodgeRequestContext requestContext)
        : this(requestContext, CreateDefaultProviders())
    {
    }

    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        // Execute providers in priority order (lowest Priority value first)
        var sortedProviders = _providers.OrderBy(p => p.Priority).ToList();

        foreach (var provider in sortedProviders)
        {
            // Check if already resolved
            if (_requestContext.Resolved)
                break;

            // Check if high-priority provider has been activated
            // If so, stop trying lower-priority providers
            if (_requestContext.HighPriorityProviderActivated && provider.Priority > 0)
                break;

            // Try this provider
            var success = provider.TryProvideDodge(context, _requestContext);

            // If provider successfully resolved synchronously, stop
            if (success && _requestContext.Resolved)
            {
                // Store result in IntermediateResults for handler to read
                if (context.IntermediateResults is not null)
                {
                    context.IntermediateResults["DodgeResolved"] = true;
                    context.IntermediateResults["DodgeProvidedBy"] = _requestContext.ProvidedBy?.Seat;
                    if (_requestContext.ProvidedCard is not null)
                    {
                        context.IntermediateResults["DodgeProvidedCard"] = _requestContext.ProvidedCard;
                    }
                }
                return ResolutionResult.SuccessResult;
            }

            // If high-priority provider was activated, stop trying other providers
            if (_requestContext.HighPriorityProviderActivated)
                break;
        }

        // If we reach here, no provider resolved it yet (they may have pushed resolvers onto the stack)
        // The resolution will continue when those resolvers execute
        return ResolutionResult.SuccessResult;
    }

    /// <summary>
    /// Creates the default list of Dodge providers in priority order.
    /// </summary>
    private static IReadOnlyList<IDodgeProvider> CreateDefaultProviders()
    {
        return new IDodgeProvider[]
        {
            new ResponseAssistanceDodgeProvider(), // Priority 0: Highest (Hujia)
            new BaguaArrayDodgeProvider(),         // Priority 1: Medium (Bagua Array)
            new ManualDodgeProvider()              // Priority 2: Lowest (Manual Dodge)
        };
    }
}

