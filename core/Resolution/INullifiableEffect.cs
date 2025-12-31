using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Represents an effect instance that can be nullified by Nullification (无懈可击).
/// The target is not the card itself, but a specific effect instance of a trick card
/// being resolved against a particular target player.
/// </summary>
public interface INullifiableEffect
{
    /// <summary>
    /// Gets a value indicating whether this effect can be nullified.
    /// Some effects (e.g., untargeted mass tricks like Taoyuan Jieyi) cannot be nullified per target.
    /// </summary>
    bool IsNullifiable { get; }

    /// <summary>
    /// Gets a unique key identifying this effect instance.
    /// Examples: "Dismantle.Resolve", "BarbarianInvasion.TargetStep", "DelayedTrick.Judgement"
    /// </summary>
    string EffectKey { get; }

    /// <summary>
    /// Gets the target player for this effect instance.
    /// </summary>
    Player TargetPlayer { get; }

    /// <summary>
    /// Gets the trick card that causes this effect, if any.
    /// </summary>
    Card? CausingCard { get; }
}
