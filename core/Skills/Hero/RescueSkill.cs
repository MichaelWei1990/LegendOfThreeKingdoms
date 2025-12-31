using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Rescue (救援) skill: Lord skill, Locked skill.
/// When other Wu faction characters use Peach on you, the recovery amount is increased by 1.
/// </summary>
public sealed class RescueSkill : BaseSkill, ILordSkill, IRecoverAmountModifyingSkill
{
    private Game? _game;
    private Player? _owner;

    /// <inheritdoc />
    public override string Id => "rescue";

    /// <inheritdoc />
    public override string Name => "救援";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Locked;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.IntervenesResolution;

    /// <summary>
    /// Checks if the player is a lord.
    /// Uses Flags dictionary to check for "IsLord" flag.
    /// </summary>
    private static bool IsLord(Player player)
    {
        return player.Flags.TryGetValue("IsLord", out var isLordValue) && 
               isLordValue is bool isLord && isLord;
    }

    /// <summary>
    /// Checks if the player belongs to Wu faction.
    /// </summary>
    private static bool IsWuFaction(Player player)
    {
        return player.FactionId == "Wu";
    }

    /// <inheritdoc />
    public void OnBeforeRecover(BeforeRecoverEvent evt)
    {
        if (_game is null || _owner is null)
            return;

        if (evt is null) throw new ArgumentNullException(nameof(evt));

        // Only process if the target is the owner (the one with Rescue skill)
        if (evt.Target.Seat != _owner.Seat)
            return;

        // Check if skill is active
        if (!IsActive(_game, _owner))
            return;

        // Check if the owner is a lord
        if (!IsLord(_owner))
            return;

        // Check if the source is a different player (other character)
        if (evt.Source.Seat == _owner.Seat)
            return;

        // Check if the source is Wu faction
        if (!IsWuFaction(evt.Source))
            return;

        // Check if the effect card is Peach
        if (evt.EffectCard.CardSubType != CardSubType.Peach)
            return;

        // Apply +1 recovery modification
        evt.RecoveryModification += 1;
    }

    /// <inheritdoc />
    public override void Attach(Game game, Player owner, IEventBus eventBus)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        if (owner is null) throw new ArgumentNullException(nameof(owner));
        if (eventBus is null) throw new ArgumentNullException(nameof(eventBus));

        _game = game;
        _owner = owner;

        // Subscribe to BeforeRecoverEvent
        eventBus.Subscribe<BeforeRecoverEvent>(OnBeforeRecover);
    }

    /// <inheritdoc />
    public override void Detach(Game game, Player owner, IEventBus eventBus)
    {
        if (eventBus is null)
            return;

        // Unsubscribe from BeforeRecoverEvent
        eventBus.Unsubscribe<BeforeRecoverEvent>(OnBeforeRecover);

        _game = null;
        _owner = null;
    }
}

/// <summary>
/// Factory for creating RescueSkill instances.
/// </summary>
public sealed class RescueSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new RescueSkill();
    }
}

