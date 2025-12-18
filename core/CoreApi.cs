using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    public static GameConfig CreateDefaultConfig(int playerCount, GameModeId gameModeId = GameModeId.Standard, int? seed = null)
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

        var modeId = gameModeId switch
        {
            GameModeId.Standard => "standard",
            GameModeId.GuoZhan => "guozhan",
            _ => throw new ArgumentOutOfRangeException(nameof(gameModeId))
        };

        return new GameConfig
        {
            PlayerConfigs = players,
            DeckConfig = new DeckConfig
            {
                IncludedPacks = new List<string> { "Base" }
            },
            GameModeId = modeId,
            GameVariantOptions = null,
            Seed = seed
        };
    }
}

/// <summary>
/// Well-known game modes supported by the core.
/// </summary>
public enum GameModeId
{
    /// <summary>
    /// 身份模式（主忠内反）。
    /// </summary>
    [Description("身份模式")]
    Standard,

    /// <summary>
    /// 国战模式。
    /// </summary>
    [Description("国战")]
    GuoZhan
}
