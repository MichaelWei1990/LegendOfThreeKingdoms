using System;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;

namespace LegendOfThreeKingdoms.Core.Skills;

/// <summary>
/// Base implementation of ISkill that provides default behavior.
/// Subclasses can override methods to implement specific skill logic.
/// </summary>
public abstract class BaseSkill : ISkill
{
    /// <inheritdoc />
    public abstract string Id { get; }

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract SkillType Type { get; }

    /// <inheritdoc />
    public abstract SkillCapability Capabilities { get; }

    /// <inheritdoc />
    public virtual bool IsActive(Game game, Player owner)
    {
        // Default implementation: skill is always active if owner is alive
        return owner.IsAlive;
    }

    /// <inheritdoc />
    public virtual void Attach(Game game, Player owner, IEventBus eventBus)
    {
        // Default implementation: no-op, subclasses should override to subscribe to events
    }

    /// <inheritdoc />
    public virtual void Detach(Game game, Player owner, IEventBus eventBus)
    {
        // Default implementation: no-op, subclasses should override to unsubscribe from events
    }
}

/// <summary>
/// Base class for skills that modify rules.
/// Provides default implementations for all IRuleModifyingSkill methods (returning null/no modification).
/// Subclasses only need to override the methods they actually need to modify.
/// </summary>
public abstract class RuleModifyingSkillBase : BaseSkill, IRuleModifyingSkill
{
    /// <inheritdoc />
    public virtual RuleResult? ModifyCanUseCard(RuleResult current, CardUsageContext context)
    {
        return null;
    }

    /// <inheritdoc />
    public virtual RuleResult? ModifyCanRespondWithCard(RuleResult current, ResponseContext context)
    {
        return null;
    }

    /// <inheritdoc />
    public virtual RuleResult? ModifyValidateAction(RuleResult current, RuleContext context, ActionDescriptor action, ChoiceRequest? choice)
    {
        return null;
    }

    /// <inheritdoc />
    public virtual int? ModifyMaxSlashPerTurn(int current, Game game, Player owner)
    {
        return null;
    }

    /// <inheritdoc />
    public virtual int? ModifyAttackDistance(int current, Game game, Player from, Player to)
    {
        return null;
    }

    /// <inheritdoc />
    public virtual int? ModifySeatDistance(int current, Game game, Player from, Player to)
    {
        return null;
    }

    /// <inheritdoc />
    public virtual int? ModifyDrawCount(int current, Game game, Player owner)
    {
        return null;
    }
}

/// <summary>
/// Base class for skills that decrease attack distance requirement by 1.
/// Used by both Horsemanship (hero skill) and Offensive Horse (equipment skill).
/// </summary>
public abstract class OffensiveDistanceModifyingSkillBase : BaseSkill, ISeatDistanceModifyingSkill
{
    /// <inheritdoc />
    public int? ModifySeatDistance(int current, Game game, Player from, Player to)
    {
        // Decrease seat distance by 1 (making it easier to attack from farther away)
        // This applies when the owner (from) attacks someone
        if (!IsActive(game, from))
            return null;

        // Decrease seat distance by 1
        // Ensure the result is at least 1 (distance cannot be negative)
        return Math.Max(1, current - 1);
    }
}

/// <summary>
/// Base class for skills that allow unlimited Slash usage per turn.
/// Used by both Roar (hero skill) and Zhuge Crossbow (equipment skill).
/// </summary>
public abstract class UnlimitedSlashSkillBase : RuleModifyingSkillBase
{
    /// <inheritdoc />
    public override int? ModifyMaxSlashPerTurn(int current, Game game, Player owner)
    {
        if (!IsActive(game, owner))
            return null;

        // Return int.MaxValue to represent unlimited Slash usage
        return int.MaxValue;
    }
}
