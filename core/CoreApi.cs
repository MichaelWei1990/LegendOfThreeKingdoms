using System;
using System.Collections.Generic;
using LegendOfThreeKingdoms.Core.Configuration;

namespace LegendOfThreeKingdoms.Core;

/// <summary>
/// Public entry point for the Legend of Three Kingdoms core engine.
/// In early phases this class mainly exposes configuration helpers.
/// </summary>
public static class CoreApi
{
    /// <summary>
    /// Creates a basic game configuration with reasonable defaults.
    /// This is a convenience factory to reduce boilerplate in tests and samples.
    /// </summary>
    public static GameConfig CreateDefaultConfig(int playerCount, string gameModeId = GameModes.Standard, int? seed = null)
    {
        if (playerCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(playerCount));
        }

        var players = new List<PlayerConfig>(playerCount);

        for (var seat = 0; seat < playerCount; seat++)
        {
            players.Add(new PlayerConfig
            {
                Seat = seat,
                MaxHealth = 4,
                InitialHealth = 4
            });
        }

        return new GameConfig
        {
            PlayerConfigs = players,
            DeckConfig = new DeckConfig
            {
                IncludedPacks = new List<string> { "Base" }
            },
            GameModeId = gameModeId,
            GameVariantOptions = null,
            Seed = seed
        };
    }
}

/// <summary>
/// Well-known game mode identifiers used by the core.
/// </summary>
public static class GameModes
{
    public const string Standard = "standard";
    public const string GuoZhan = "guozhan";
}
