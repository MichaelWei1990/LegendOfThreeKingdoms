using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Rules;

/// <summary>
/// Default rule modifier that performs no modifications.
/// All methods return the original value or null (no modification).
/// </summary>
public sealed class NoOpRuleModifier : IRuleModifier
{
    /// <inheritdoc />
    public RuleResult ModifyCanUseCard(RuleResult current, CardUsageContext context)
    {
        return current;
    }

    /// <inheritdoc />
    public RuleResult ModifyCanRespondWithCard(RuleResult current, ResponseContext context)
    {
        return current;
    }

    /// <inheritdoc />
    public RuleResult ModifyValidateAction(RuleResult current, RuleContext context, ActionDescriptor action, ChoiceRequest? choice)
    {
        return current;
    }

    /// <inheritdoc />
    public int? ModifyMaxSlashPerTurn(int current, Game game, Player player)
    {
        return null;
    }

    /// <inheritdoc />
    public int? ModifyAttackDistance(int current, Game game, Player from, Player to)
    {
        return null;
    }

    /// <inheritdoc />
    public int? ModifySeatDistance(int current, Game game, Player from, Player to)
    {
        return null;
    }

    /// <inheritdoc />
    public int? ModifyDrawCount(int current, Game game, Player player)
    {
        return null;
    }

    /// <inheritdoc />
    public int? ModifyMaxTargets(int current, CardUsageContext context)
    {
        return null;
    }
}
