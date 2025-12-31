using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Identity;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.GameMode;

/// <summary>
/// Standard identity mode (身份局) implementation.
/// Supports role assignment and win condition checking for identity mode.
/// </summary>
public sealed class StandardGameMode : IGameMode
{
    private readonly IRoleAssignmentService _roleAssignmentService;
    private readonly IWinConditionService _winConditionService;

    /// <summary>
    /// Creates a new StandardGameMode with default services.
    /// </summary>
    public StandardGameMode()
    {
        _roleAssignmentService = new BasicRoleAssignmentService();
        _winConditionService = new BasicWinConditionService();
    }

    /// <summary>
    /// Creates a new StandardGameMode with custom services.
    /// </summary>
    /// <param name="roleAssignmentService">Role assignment service to use.</param>
    /// <param name="winConditionService">Win condition service to use.</param>
    public StandardGameMode(
        IRoleAssignmentService? roleAssignmentService = null,
        IWinConditionService? winConditionService = null)
    {
        _roleAssignmentService = roleAssignmentService ?? new BasicRoleAssignmentService();
        _winConditionService = winConditionService ?? new BasicWinConditionService();
    }

    /// <inheritdoc />
    public string Id => "standard";

    /// <inheritdoc />
    public string DisplayName => "身份模式";

    /// <inheritdoc />
    public int SelectFirstPlayerSeat(Game game)
    {
        if (game is null) throw new System.ArgumentNullException(nameof(game));

        // In identity mode, the Lord should take the first turn
        var lord = game.Players.FirstOrDefault(p => p.CampId == Model.RoleConstants.Lord);
        if (lord is not null && lord.IsAlive)
        {
            return lord.Seat;
        }

        // Fallback: return the first alive player
        var firstAlive = game.Players.FirstOrDefault(p => p.IsAlive);
        return firstAlive?.Seat ?? 0;
    }

    /// <inheritdoc />
    public IRoleAssignmentService? GetRoleAssignmentService()
    {
        return _roleAssignmentService;
    }

    /// <inheritdoc />
    public IWinConditionService? GetWinConditionService()
    {
        return _winConditionService;
    }
}

