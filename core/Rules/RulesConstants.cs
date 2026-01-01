namespace LegendOfThreeKingdoms.Core.Rules;

/// <summary>
/// Constants used throughout the Rules layer.
/// Centralizes configuration values to avoid magic numbers and improve maintainability.
/// </summary>
public static class RulesConstants
{
    /// <summary>
    /// Default maximum number of Slash cards a player can use per turn.
    /// </summary>
    public const int DefaultMaxSlashPerTurn = 1;

    /// <summary>
    /// Default attack distance (base range for attacking).
    /// </summary>
    public const int DefaultAttackDistance = 1;

    /// <summary>
    /// Minimum seat distance between players (always at least 1 for distinct players).
    /// </summary>
    public const int MinimumSeatDistance = 1;
}
