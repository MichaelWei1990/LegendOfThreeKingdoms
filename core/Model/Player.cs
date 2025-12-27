using System.Collections.Generic;
using LegendOfThreeKingdoms.Core.Model.Zones;

namespace LegendOfThreeKingdoms.Core.Model;

/// <summary>
/// Pure state representation of a single player.
/// Contains seat, faction and zone references, but no rule logic.
/// </summary>
public sealed class Player
{
    /// <summary>
    /// Seat index, starting from 0 and going clockwise.
    /// </summary>
    public int Seat { get; init; }

    /// <summary>
    /// Identity or camp identifier (e.g. lord/rebel or kingdom name).
    /// Concrete semantics are defined by the active game mode.
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
    /// Used by skills like Twin Swords (雌雄双股剑) that interact differently with opposite gender.
    /// </summary>
    public Gender Gender { get; init; } = Gender.Neutral;

    /// <summary>
    /// Maximum health for this player.
    /// </summary>
    public int MaxHealth { get; init; }

    /// <summary>
    /// Current health points.
    /// </summary>
    public int CurrentHealth { get; set; }

    /// <summary>
    /// Whether this player is currently alive.
    /// </summary>
    public bool IsAlive { get; set; } = true;

    /// <summary>
    /// Zone containing the player's hand cards.
    /// </summary>
    public IZone HandZone { get; init; } = new Zone("Hand_Default", ownerSeat: null, isPublic: false);

    /// <summary>
    /// Zone containing the player's equipment.
    /// </summary>
    public IZone EquipmentZone { get; init; } = new Zone("Equip_Default", ownerSeat: null, isPublic: true);

    /// <summary>
    /// Zone containing delayed tricks and judgement cards attached to the player.
    /// </summary>
    public IZone JudgementZone { get; init; } = new Zone("Judge_Default", ownerSeat: null, isPublic: true);

    /// <summary>
    /// Free-form flags for mode/skill specific state (e.g. chained, turned over).
    /// Keys are agreed by higher-level logic, not enforced here.
    /// </summary>
    public IDictionary<string, object?> Flags { get; } = new Dictionary<string, object?>();
}
