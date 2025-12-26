using System.Linq;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Extension methods for ResolutionContext to extract commonly needed information.
/// </summary>
internal static class ResolutionContextExtensions
{
    /// <summary>
    /// Extracts the causing card from the resolution context.
    /// Tries to get the card from Action.CardCandidates first, then from Choice.SelectedCardIds.
    /// </summary>
    /// <param name="context">The resolution context.</param>
    /// <returns>The causing card if found, null otherwise.</returns>
    public static Card? ExtractCausingCard(this ResolutionContext context)
    {
        if (context is null)
            return null;

        var sourcePlayer = context.SourcePlayer;

        // Try to get card from Action.CardCandidates first
        if (context.Action?.CardCandidates is not null && context.Action.CardCandidates.Count > 0)
        {
            return context.Action.CardCandidates[0];
        }

        // If not found, try to get from Choice.SelectedCardIds
        if (context.Choice?.SelectedCardIds is not null && context.Choice.SelectedCardIds.Count > 0)
        {
            var cardId = context.Choice.SelectedCardIds[0];
            return sourcePlayer.HandZone.Cards.FirstOrDefault(c => c.Id == cardId);
        }

        return null;
    }
}
