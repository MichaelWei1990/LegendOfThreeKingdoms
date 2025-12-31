using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Events;

namespace LegendOfThreeKingdoms.Core.Identity;

/// <summary>
/// Extension methods for setting up identity mode win condition checking.
/// </summary>
public static class IdentityModeExtensions
{
    /// <summary>
    /// Sets up win condition checking for identity mode by creating and registering a WinConditionChecker.
    /// This should be called after game initialization and before gameplay begins.
    /// </summary>
    /// <param name="eventBus">The event bus to use for event subscription.</param>
    /// <param name="winConditionService">The win condition service to use.</param>
    /// <returns>The created WinConditionChecker instance (can be discarded if not needed).</returns>
    public static WinConditionChecker SetupWinConditionChecking(
        this IEventBus eventBus,
        IWinConditionService winConditionService)
    {
        if (eventBus is null) throw new System.ArgumentNullException(nameof(eventBus));
        if (winConditionService is null) throw new System.ArgumentNullException(nameof(winConditionService));

        return new WinConditionChecker(winConditionService, eventBus);
    }
}

