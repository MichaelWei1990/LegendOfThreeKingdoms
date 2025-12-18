namespace LegendOfThreeKingdoms.Core.Abstractions;

/// <summary>
/// Registry used by the core to resolve game mode implementations by identifier.
/// </summary>
public interface IGameModeRegistry
{
    /// <summary>
    /// Returns the game mode associated with the given identifier.
    /// Implementations should throw when the id is unknown.
    /// </summary>
    IGameMode GetById(string id);
}
