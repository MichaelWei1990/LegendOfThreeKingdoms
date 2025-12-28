using LegendOfThreeKingdoms.Core.Resolution;

namespace LegendOfThreeKingdoms.Core.Response;

/// <summary>
/// Interface for providers that can provide Dodge (闪) responses.
/// Used to implement a priority chain: Response Assistance (Hujia) > Bagua Array > Manual Dodge.
/// </summary>
public interface IDodgeProvider
{
    /// <summary>
    /// Gets the priority of this provider.
    /// Lower values indicate higher priority (executed first).
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Attempts to provide Dodge for the given request context.
    /// If successful, sets context.Resolved = true and updates context.ProvidedBy and context.ProvidedCard.
    /// </summary>
    /// <param name="context">The resolution context.</param>
    /// <param name="requestContext">The Dodge request context to resolve.</param>
    /// <returns>True if Dodge was successfully provided, false otherwise.</returns>
    bool TryProvideDodge(ResolutionContext context, DodgeRequestContext requestContext);
}

