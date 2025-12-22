using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;

namespace LegendOfThreeKingdoms.Core.Skills;

/// <summary>
/// Wraps an ISkill instance and implements IRuleModifier to allow skills to modify rule judgments.
/// </summary>
public sealed class SkillRuleModifier : IRuleModifier
{
    private readonly ISkill _skill;
    private readonly Game _game;
    private readonly Player _owner;

    /// <summary>
    /// Creates a new SkillRuleModifier that wraps the given skill.
    /// </summary>
    /// <param name="skill">The skill to wrap.</param>
    /// <param name="game">The current game state.</param>
    /// <param name="owner">The player who owns the skill.</param>
    /// <exception cref="ArgumentNullException">Thrown if skill, game, or owner is null.</exception>
    public SkillRuleModifier(ISkill skill, Game game, Player owner)
    {
        _skill = skill ?? throw new ArgumentNullException(nameof(skill));
        _game = game ?? throw new ArgumentNullException(nameof(game));
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    /// <inheritdoc />
    public RuleResult ModifyCanUseCard(RuleResult current, CardUsageContext context)
    {
        if (!_skill.IsActive(_game, _owner))
            return current;

        if ((_skill.Capabilities & SkillCapability.ModifiesRules) == 0)
            return current;

        if (_skill is IRuleModifyingSkill ruleModifyingSkill)
        {
            var modified = ruleModifyingSkill.ModifyCanUseCard(current, context);
            return modified ?? current;
        }

        return current;
    }

    /// <inheritdoc />
    public RuleResult ModifyCanRespondWithCard(RuleResult current, ResponseContext context)
    {
        if (!_skill.IsActive(_game, _owner))
            return current;

        if ((_skill.Capabilities & SkillCapability.ModifiesRules) == 0)
            return current;

        if (_skill is IRuleModifyingSkill ruleModifyingSkill)
        {
            var modified = ruleModifyingSkill.ModifyCanRespondWithCard(current, context);
            return modified ?? current;
        }

        return current;
    }

    /// <inheritdoc />
    public RuleResult ModifyValidateAction(RuleResult current, RuleContext context, ActionDescriptor action, ChoiceRequest? choice)
    {
        if (!_skill.IsActive(_game, _owner))
            return current;

        if ((_skill.Capabilities & SkillCapability.ModifiesRules) == 0)
            return current;

        if (_skill is IRuleModifyingSkill ruleModifyingSkill)
        {
            var modified = ruleModifyingSkill.ModifyValidateAction(current, context, action, choice);
            return modified ?? current;
        }

        return current;
    }

    /// <inheritdoc />
    public int? ModifyMaxSlashPerTurn(int current, Game game, Player player)
    {
        if (!_skill.IsActive(_game, _owner))
            return null;

        if ((_skill.Capabilities & SkillCapability.ModifiesRules) == 0)
            return null;

        if (_skill is IRuleModifyingSkill ruleModifyingSkill)
        {
            return ruleModifyingSkill.ModifyMaxSlashPerTurn(current, game, _owner);
        }

        return null;
    }

    /// <inheritdoc />
    public int? ModifyAttackDistance(int current, Game game, Player from, Player to)
    {
        if (!_skill.IsActive(_game, _owner))
            return null;

        if ((_skill.Capabilities & SkillCapability.ModifiesRules) == 0)
            return null;

        // Check for attack distance-specific interface first (most focused)
        if (_skill is IAttackDistanceModifyingSkill attackDistanceSkill)
        {
            return attackDistanceSkill.ModifyAttackDistance(current, game, from, to);
        }

        // Fall back to full rule modifying interface
        if (_skill is IRuleModifyingSkill ruleModifyingSkill)
        {
            return ruleModifyingSkill.ModifyAttackDistance(current, game, from, to);
        }

        return null;
    }

    /// <inheritdoc />
    public int? ModifySeatDistance(int current, Game game, Player from, Player to)
    {
        if (!_skill.IsActive(_game, _owner))
            return null;

        if ((_skill.Capabilities & SkillCapability.ModifiesRules) == 0)
            return null;

        // Check for seat distance-specific interface first (most focused)
        if (_skill is ISeatDistanceModifyingSkill seatDistanceSkill)
        {
            return seatDistanceSkill.ModifySeatDistance(current, game, from, to);
        }

        // Fall back to full rule modifying interface
        if (_skill is IRuleModifyingSkill ruleModifyingSkill)
        {
            return ruleModifyingSkill.ModifySeatDistance(current, game, from, to);
        }

        return null;
    }

    /// <inheritdoc />
    public int? ModifyDrawCount(int current, Game game, Player player)
    {
        if (!_skill.IsActive(_game, _owner))
            return null;

        if ((_skill.Capabilities & SkillCapability.ModifiesRules) == 0)
            return null;

        if (_skill is IRuleModifyingSkill ruleModifyingSkill)
        {
            return ruleModifyingSkill.ModifyDrawCount(current, game, _owner);
        }

        return null;
    }
}

/// <summary>
/// Provides rule modifiers by querying the SkillManager for active skills with rule-modifying capabilities.
/// </summary>
public sealed class SkillRuleModifierProvider : IRuleModifierProvider
{
    private readonly SkillManager _skillManager;

    /// <summary>
    /// Creates a new SkillRuleModifierProvider.
    /// </summary>
    /// <param name="skillManager">The skill manager to use for querying skills.</param>
    /// <exception cref="ArgumentNullException">Thrown if skillManager is null.</exception>
    public SkillRuleModifierProvider(SkillManager skillManager)
    {
        _skillManager = skillManager ?? throw new ArgumentNullException(nameof(skillManager));
    }

    /// <inheritdoc />
    public IReadOnlyList<IRuleModifier> GetModifiersFor(Game game, Player player)
    {
        if (game is null)
            throw new ArgumentNullException(nameof(game));
        if (player is null)
            throw new ArgumentNullException(nameof(player));

        var skills = _skillManager.GetActiveSkills(game, player);
        var modifiers = new List<IRuleModifier>();

        foreach (var skill in skills)
        {
            if ((skill.Capabilities & SkillCapability.ModifiesRules) != 0)
            {
                modifiers.Add(new SkillRuleModifier(skill, game, player));
            }
        }

        return modifiers;
    }
}
