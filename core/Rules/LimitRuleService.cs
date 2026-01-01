using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Rules;

/// <summary>
/// Default implementation of usage limits (e.g. Slash once per turn).
/// </summary>
public sealed class LimitRuleService : ILimitRuleService
{
    /// <inheritdoc />
    public int GetMaxSlashPerTurn(Game game, Player player)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        if (player is null) throw new ArgumentNullException(nameof(player));

        // Initial implementation: fixed 1 Slash per turn.
        return RulesConstants.DefaultMaxSlashPerTurn;
    }
}
