using System.Collections.Generic;

namespace LegendOfThreeKingdoms.Core.Character;

/// <summary>
/// Context for character selection process.
/// Stores candidate characters and selection state for each player.
/// </summary>
public sealed class SelectionContext
{
    private readonly Dictionary<int, PlayerSelectionState> _playerStates = new();

    /// <summary>
    /// Gets or creates the selection state for a player.
    /// </summary>
    /// <param name="playerSeat">The seat index of the player.</param>
    /// <returns>The selection state for the player.</returns>
    public PlayerSelectionState GetOrCreatePlayerState(int playerSeat)
    {
        if (!_playerStates.TryGetValue(playerSeat, out var state))
        {
            state = new PlayerSelectionState(playerSeat);
            _playerStates[playerSeat] = state;
        }
        return state;
    }

    /// <summary>
    /// Gets the selection state for a player, or null if not found.
    /// </summary>
    /// <param name="playerSeat">The seat index of the player.</param>
    /// <returns>The selection state, or null if not found.</returns>
    public PlayerSelectionState? GetPlayerState(int playerSeat)
    {
        return _playerStates.TryGetValue(playerSeat, out var state) ? state : null;
    }

    /// <summary>
    /// Checks whether a player has been offered candidates.
    /// </summary>
    /// <param name="playerSeat">The seat index of the player.</param>
    /// <returns>True if candidates have been offered, false otherwise.</returns>
    public bool HasCandidates(int playerSeat)
    {
        var state = GetPlayerState(playerSeat);
        return state is not null && state.CandidateCharacterIds.Count > 0;
    }

    /// <summary>
    /// Checks whether a player has selected a character.
    /// </summary>
    /// <param name="playerSeat">The seat index of the player.</param>
    /// <returns>True if a character has been selected, false otherwise.</returns>
    public bool HasSelected(int playerSeat)
    {
        var state = GetPlayerState(playerSeat);
        return state is not null && !string.IsNullOrWhiteSpace(state.SelectedCharacterId);
    }

    /// <summary>
    /// Clears all selection states.
    /// </summary>
    public void Clear()
    {
        _playerStates.Clear();
    }
}

/// <summary>
/// Selection state for a single player.
/// </summary>
public sealed class PlayerSelectionState
{
    /// <summary>
    /// The seat index of the player.
    /// </summary>
    public int PlayerSeat { get; }

    /// <summary>
    /// List of candidate character IDs offered to this player.
    /// </summary>
    public List<string> CandidateCharacterIds { get; } = new();

    /// <summary>
    /// The selected character ID, or null if not selected yet.
    /// </summary>
    public string? SelectedCharacterId { get; set; }

    /// <summary>
    /// Creates a new player selection state.
    /// </summary>
    /// <param name="playerSeat">The seat index of the player.</param>
    public PlayerSelectionState(int playerSeat)
    {
        PlayerSeat = playerSeat;
    }
}
