using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Rules;

/// <summary>
/// Default phase rule service: only the active player's Play phase allows normal card usage.
/// </summary>
public sealed class PhaseRuleService : IPhaseRuleService
{
    /// <inheritdoc />
    public bool IsCardUsagePhase(Game game, Player player)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        if (player is null) throw new ArgumentNullException(nameof(player));

        if (!player.IsAlive)
        {
            return false;
        }

        // Only the active player's Play phase allows using basic cards in the initial implementation.
        return game.CurrentPhase == Phase.Play && player.Seat == game.CurrentPlayerSeat;
    }
}
