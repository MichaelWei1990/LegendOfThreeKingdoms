using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Configuration;

/// <summary>
/// Per-player configuration at game start.
/// This describes static properties such as seat, faction and starting health.
/// </summary>
public sealed class PlayerConfig
{
    /// <summary>
    /// Seat index, starting from 0 and going clockwise.
    /// </summary>
    public int Seat { get; init; }

    /// <summary>
    /// Identity or camp identifier (e.g. lord / rebel / loyalist / renegade, or factions in other modes).
    /// The concrete meaning is interpreted by the selected game mode.
    /// </summary>
    public string? CampId { get; init; }

    /// <summary>
    /// Optional faction identifier for modes that distinguish between camp and faction.
    /// </summary>
    public string? FactionId { get; init; }

    /// <summary>
    /// Hero/character identifier used to load abilities from content.
    /// </summary>
    public string? HeroId { get; init; }

    /// <summary>
    /// Gender of the player character.
    /// </summary>
    public Gender Gender { get; init; } = Gender.Neutral;

    /// <summary>
    /// Maximum health at the start of the game.
    /// </summary>
    public int MaxHealth { get; init; } = 4;

    /// <summary>
    /// Initial health at the start of the game.
    /// </summary>
    public int InitialHealth { get; init; } = 4;
}
