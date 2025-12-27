using System.Linq;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Qingnang (青囊) skill: Active skill that allows discarding a hand card to heal a target by 1 HP, once per turn.
/// </summary>
public sealed class QingnangSkill : BaseSkill, IActionProvidingSkill
{
    /// <inheritdoc />
    public override string Id => "qingnang";

    /// <inheritdoc />
    public override string Name => "青囊";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Active;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.InitiatesChoices;

    /// <inheritdoc />
    public ActionDescriptor? GenerateAction(Game game, Player owner)
    {
        // Check conditions:
        // 1. Player has at least 1 hand card
        if (owner.HandZone.Cards.Count == 0)
            return null;

        // 2. At least 1 alive player exists (can target self or others)
        var alivePlayers = game.Players.Where(p => p.IsAlive).ToList();
        if (alivePlayers.Count == 0)
            return null;

        // 3. Check if already used this turn
        var usageKey = $"qingnang_used_turn_{game.TurnNumber}_seat_{game.CurrentPlayerSeat}";
        if (owner.Flags.ContainsKey(usageKey))
        {
            return null; // Already used this turn
        }

        // Create action with all hand cards as candidates
        return new ActionDescriptor(
            ActionId: "UseQingnang",
            DisplayKey: "action.useQingnang",
            RequiresTargets: true,
            TargetConstraints: new TargetConstraints(
                MinTargets: 1,
                MaxTargets: 1,
                FilterType: TargetFilterType.Any),
            CardCandidates: owner.HandZone.Cards.ToList());
    }
}

/// <summary>
/// Factory for creating QingnangSkill instances.
/// </summary>
public sealed class QingnangSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new QingnangSkill();
    }
}
