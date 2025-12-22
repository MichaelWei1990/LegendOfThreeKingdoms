using LegendOfThreeKingdoms.Core.Judgement;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Tricks;

/// <summary>
/// Type of trick card (immediate or delayed).
/// </summary>
public enum TrickCardType
{
    /// <summary>
    /// Immediate trick card (即时锦囊).
    /// </summary>
    Immediate,

    /// <summary>
    /// Delayed trick card (延时锦囊).
    /// </summary>
    Delayed
}

/// <summary>
/// Base interface for all trick cards.
/// Trick cards are cards that produce effects through immediate resolution or delayed judgement.
/// </summary>
public interface ITrickCard
{
    /// <summary>
    /// The card instance.
    /// </summary>
    Card Card { get; }

    /// <summary>
    /// Type of the trick card (Immediate or Delayed).
    /// </summary>
    TrickCardType TrickType { get; }
}

/// <summary>
/// Interface for immediate trick cards.
/// Immediate tricks are resolved immediately after being used during the play phase.
/// </summary>
public interface IImmediateTrick : ITrickCard
{
    // Immediate tricks are resolved immediately after use.
    // Resolution logic is implemented by corresponding Resolvers.
}

/// <summary>
/// Interface for delayed trick cards.
/// Delayed tricks require:
/// 1. Placement in the target player's judgement zone
/// 2. Judgement during the target player's judgement phase
/// 3. Effect execution based on judgement result
/// </summary>
public interface IDelayedTrick : ITrickCard
{
    /// <summary>
    /// The judgement rule that determines success/failure of the delayed trick.
    /// </summary>
    IJudgementRule JudgementRule { get; }

    /// <summary>
    /// The seat of the target player for this delayed trick.
    /// </summary>
    int TargetPlayerSeat { get; }
}
