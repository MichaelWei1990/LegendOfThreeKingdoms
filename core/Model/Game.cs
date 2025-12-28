using LegendOfThreeKingdoms.Core.Configuration;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Turns;

namespace LegendOfThreeKingdoms.Core.Model;

/// <summary>
/// Pure state representation of a single game instance.
/// Contains only data; rules and progression logic live in higher layers.
/// </summary>
public sealed class Game
{
    /// <summary>
    /// Player states in seat order starting from 0.
    /// </summary>
    public IReadOnlyList<Player> Players { get; init; } = Array.Empty<Player>();

    /// <summary>
    /// Seat index of the player whose turn it currently is.
    /// </summary>
    public int CurrentPlayerSeat { get; set; }

    /// <summary>
    /// Current phase of the active player's turn.
    /// </summary>
    public Phase CurrentPhase { get; set; } = Phase.None;

    /// <summary>
    /// Optional turn counter for debugging and logging.
    /// </summary>
    public int TurnNumber { get; set; } = 1;

    /// <summary>
    /// Convenience immutable view that exposes the current turn/phase state
    /// as a single value object for use by higher level engines.
    /// Backed by <see cref="CurrentPlayerSeat"/>, <see cref="CurrentPhase"/> and <see cref="TurnNumber"/>.
    /// </summary>
    public TurnState Turn
    {
        get => new TurnState
        {
            CurrentPlayerSeat = CurrentPlayerSeat,
            CurrentPhase = CurrentPhase,
            TurnNumber = TurnNumber
        };
        set
        {
            CurrentPlayerSeat = value.CurrentPlayerSeat;
            CurrentPhase = value.CurrentPhase;
            TurnNumber = value.TurnNumber;
        }
    }

    /// <summary>
    /// Global draw pile zone.
    /// </summary>
    public IZone DrawPile { get; init; } = new Zone(ZoneIds.DrawPile, ownerSeat: null, isPublic: false);

    /// <summary>
    /// Global discard pile zone.
    /// </summary>
    public IZone DiscardPile { get; init; } = new Zone(ZoneIds.DiscardPile, ownerSeat: null, isPublic: true);

    /// <summary>
    /// Whether the game has finished.
    /// </summary>
    public bool IsFinished { get; set; }

    /// <summary>
    /// Optional free-form description of the winner (for logs/UI).
    /// Actual win conditions are determined by the active game mode.
    /// </summary>
    public string? WinnerDescription { get; set; }

    /// <summary>
    /// Creates a minimal game state from configuration.
    /// This method maps config to state but does not apply any game rules.
    /// </summary>
    public static Game FromConfig(GameConfig config)
    {
        var players = config.PlayerConfigs
            .OrderBy(p => p.Seat)
            .Select(p => new Player
            {
                Seat = p.Seat,
                CampId = p.CampId,
                FactionId = p.FactionId,
                HeroId = p.HeroId,
                Gender = p.Gender,
                MaxHealth = p.MaxHealth,
                CurrentHealth = p.InitialHealth,
                IsAlive = true,
                HandZone = new Zone($"Hand_{p.Seat}", p.Seat, isPublic: false),
                EquipmentZone = new Zone($"Equip_{p.Seat}", p.Seat, isPublic: true),
                JudgementZone = new Zone($"Judge_{p.Seat}", p.Seat, isPublic: true)
            })
            .ToArray();

        return new Game
        {
            Players = players,
            CurrentPlayerSeat = players.Length > 0 ? players[0].Seat : 0,
            CurrentPhase = Phase.None,
            DrawPile = new Zone(ZoneIds.DrawPile, ownerSeat: null, isPublic: false),
            DiscardPile = new Zone(ZoneIds.DiscardPile, ownerSeat: null, isPublic: true),
            IsFinished = false,
            WinnerDescription = null
        };
    }
}
