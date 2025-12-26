using LegendOfThreeKingdoms.Core.Judgement;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Interface for delayed trick card effect resolvers.
/// Provides judgement rule and effect application methods for delayed trick cards.
/// </summary>
internal interface IDelayedTrickEffectResolver
{
    /// <summary>
    /// Gets the judgement rule for this delayed trick card.
    /// </summary>
    IJudgementRule JudgementRule { get; }

    /// <summary>
    /// Applies the effect when judgement succeeds.
    /// </summary>
    /// <param name="context">The resolution context.</param>
    /// <param name="game">The game state.</param>
    /// <param name="judgeOwner">The player who owns the judgement.</param>
    void ApplySuccessEffect(ResolutionContext context, Game game, Player judgeOwner);

    /// <summary>
    /// Applies the effect when judgement fails.
    /// </summary>
    /// <param name="context">The resolution context.</param>
    /// <param name="game">The game state.</param>
    /// <param name="judgeOwner">The player who owns the judgement.</param>
    /// <param name="card">The delayed trick card.</param>
    void ApplyFailureEffect(ResolutionContext context, Game game, Player judgeOwner, Card card);
}
