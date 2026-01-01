using System;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Rules;

/// <summary>
/// Default modifier provider used by the basic rules implementation.
/// It returns no modifiers, effectively leaving all rule results unchanged.
/// </summary>
public sealed class NoOpRuleModifierProvider : IRuleModifierProvider
{
    private static readonly IReadOnlyList<IRuleModifier> EmptyModifiers = Array.Empty<IRuleModifier>();

    /// <inheritdoc />
    public IReadOnlyList<IRuleModifier> GetModifiersFor(Game game, Player player)
    {
        return EmptyModifiers;
    }
}
