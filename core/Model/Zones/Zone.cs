using System.Collections.Generic;

namespace LegendOfThreeKingdoms.Core.Model.Zones;

/// <summary>
/// Base abstraction for any card-holding zone (draw pile, discard pile, hand, equipment, judgement, etc.).
/// This interface intentionally exposes only read-only views; mutation will be coordinated by higher-level services.
/// </summary>
public interface IZone
{
    /// <summary>
    /// Identifier of this zone (e.g. "DrawPile", "DiscardPile", "Hand_0").
    /// </summary>
    string ZoneId { get; }

    /// <summary>
    /// Seat index of the owning player, if applicable (e.g. hand/equipment/judgement zones).
    /// Null for global zones like draw pile or discard pile.
    /// </summary>
    int? OwnerSeat { get; }

    /// <summary>
    /// Whether cards in this zone are visible to all players by default.
    /// </summary>
    bool IsPublic { get; }

    /// <summary>
    /// Ordered list of cards in this zone.
    /// </summary>
    IReadOnlyList<Card> Cards { get; }
}

/// <summary>
/// Simple in-memory implementation of <see cref="IZone"/> used by the core model.
/// Higher layers are responsible for applying game rules when mutating the underlying card list.
/// </summary>
public class Zone : IZone
{
    private readonly List<Card> _cards = new();

    public Zone(string zoneId, int? ownerSeat, bool isPublic)
    {
        ZoneId = zoneId;
        OwnerSeat = ownerSeat;
        IsPublic = isPublic;
    }

    public string ZoneId { get; }

    public int? OwnerSeat { get; }

    public bool IsPublic { get; }

    public IReadOnlyList<Card> Cards => _cards;

    /// <summary>
    /// Internal helper used by higher layers to modify the contents of the zone.
    /// Game rules should be enforced by services above the model layer.
    /// </summary>
    internal IList<Card> MutableCards => _cards;
}

public static class ZoneIds
{
    public const string DrawPile = "DrawPile";
    public const string DiscardPile = "DiscardPile";
}
