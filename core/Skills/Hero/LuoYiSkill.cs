using System;
using System.Linq;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// LuoYi (裸衣) skill: During draw phase, you can choose to draw one less card.
/// For this turn, damage from Slash or Duel that you cause is increased by 1.
/// </summary>
public sealed class LuoYiSkill : BaseSkill, IDrawPhaseModifyingSkill, IBeforeDamageSkill, IDamageModifyingSkill
{
    private Game? _game;
    private Player? _owner;
    private IEventBus? _eventBus;
    private bool _isActiveThisTurn = false;

    /// <inheritdoc />
    public override string Id => "luoyi";

    /// <inheritdoc />
    public override string Name => "裸衣";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Trigger;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.None;

    /// <inheritdoc />
    public override void Attach(Game game, Player owner, IEventBus eventBus)
    {
        if (game is null)
            throw new ArgumentNullException(nameof(game));
        if (owner is null)
            throw new ArgumentNullException(nameof(owner));
        if (eventBus is null)
            throw new ArgumentNullException(nameof(eventBus));

        _game = game;
        _owner = owner;
        _eventBus = eventBus;

        // Subscribe to events
        eventBus.Subscribe<BeforeDamageEvent>(OnBeforeDamage);
        eventBus.Subscribe<TurnEndEvent>(OnTurnEnd);
    }

    /// <inheritdoc />
    public override void Detach(Game game, Player owner, IEventBus eventBus)
    {
        if (eventBus is not null)
        {
            eventBus.Unsubscribe<BeforeDamageEvent>(OnBeforeDamage);
            eventBus.Unsubscribe<TurnEndEvent>(OnTurnEnd);
        }

        _game = null;
        _owner = null;
        _eventBus = null;
        _isActiveThisTurn = false;
    }

    /// <inheritdoc />
    public bool CanModifyDrawPhase(Game game, Player owner)
    {
        // LuoYi can modify draw phase if the owner is alive
        return owner.IsAlive;
    }

    /// <inheritdoc />
    public bool ShouldModifyDrawPhase(Game game, Player owner, Func<Rules.ChoiceRequest, Rules.ChoiceResult> getPlayerChoice)
    {
        if (!CanModifyDrawPhase(game, owner))
            return false;

        // Ask player if they want to activate LuoYi
        var confirmRequest = new Rules.ChoiceRequest(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: owner.Seat,
            ChoiceType: Rules.ChoiceType.Confirm,
            TargetConstraints: null,
            AllowedCards: null,
            ResponseWindowId: null,
            CanPass: true // Player can choose not to activate
        );

        var confirmResult = getPlayerChoice(confirmRequest);
        return confirmResult?.Confirmed == true;
    }

    /// <inheritdoc />
    public int GetDrawCountModification(Game game, Player owner)
    {
        // LuoYi reduces draw count by 1
        return -1;
    }

    /// <inheritdoc />
    public void OnDrawPhaseModified(Game game, Player owner, Events.IEventBus? eventBus)
    {
        // Activate the damage boost for this turn
        _isActiveThisTurn = true;
    }

    /// <inheritdoc />
    public void OnBeforeDamage(BeforeDamageEvent evt)
    {
        if (_game is null || _owner is null)
            return;

        // Only process if LuoYi is active this turn
        if (!_isActiveThisTurn)
            return;

        // Only process if the owner is the damage source
        if (evt.Damage.SourceSeat != _owner.Seat)
            return;

        // Check if damage is from Slash or Duel
        if (!IsSlashOrDuelDamage(evt.Damage))
            return;

        // Increase damage by 1
        evt.DamageModification += 1;
    }

    private void OnTurnEnd(TurnEndEvent evt)
    {
        if (_game is null || _owner is null)
            return;

        // Only reset if it's the owner's turn ending
        if (evt.PlayerSeat == _owner.Seat)
        {
            _isActiveThisTurn = false;
        }
    }

    /// <inheritdoc />
    public int ModifyDamage(DamageDescriptor damage, Game game, Player owner)
    {
        if (_game is null || _owner is null)
            return 0;

        // Only process if LuoYi is active this turn
        if (!_isActiveThisTurn)
            return 0;

        // Only process if the owner is the damage source
        if (damage.SourceSeat != _owner.Seat)
            return 0;

        // Check if damage is from Slash or Duel
        if (!IsSlashOrDuelDamage(damage))
            return 0;

        // Increase damage by 1
        return 1;
    }

    /// <summary>
    /// Checks if the damage is from Slash or Duel.
    /// </summary>
    private static bool IsSlashOrDuelDamage(DamageDescriptor damage)
    {
        // Check if damage reason is "Slash" or "Duel"
        return damage.Reason == "Slash" || damage.Reason == "Duel";
    }
}

/// <summary>
/// Factory for creating LuoYiSkill instances.
/// </summary>
public sealed class LuoYiSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new LuoYiSkill();
    }
}

