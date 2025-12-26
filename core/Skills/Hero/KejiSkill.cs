using System;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Keji (克己) skill: Locked skill that allows skipping discard phase if no Slash was used or played during play phase.
/// </summary>
public sealed class KejiSkill : BaseSkill
{
    private Game? _game;
    private Player? _owner;
    private IEventBus? _eventBus;
    private bool _slashUsedOrPlayedThisPlayPhase;

    /// <inheritdoc />
    public override string Id => "keji";

    /// <inheritdoc />
    public override string Name => "克己";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Locked;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.ModifiesRules;

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

        eventBus.Subscribe<PhaseStartEvent>(OnPhaseStart);
        eventBus.Subscribe<PhaseEndEvent>(OnPhaseEnd);
        eventBus.Subscribe<CardUsedEvent>(OnCardUsed);
        eventBus.Subscribe<CardPlayedEvent>(OnCardPlayed);
    }

    /// <inheritdoc />
    public override void Detach(Game game, Player owner, IEventBus eventBus)
    {
        if (eventBus is null)
            return;

        eventBus.Unsubscribe<PhaseStartEvent>(OnPhaseStart);
        eventBus.Unsubscribe<PhaseEndEvent>(OnPhaseEnd);
        eventBus.Unsubscribe<CardUsedEvent>(OnCardUsed);
        eventBus.Unsubscribe<CardPlayedEvent>(OnCardPlayed);

        _game = null;
        _owner = null;
        _eventBus = null;
    }

    private void OnPhaseStart(PhaseStartEvent evt)
    {
        // Only track for the owner's play phase
        if (_owner is null || evt.PlayerSeat != _owner.Seat)
            return;

        // Reset tracking flag when play phase starts
        if (evt.Phase == Phase.Play)
        {
            _slashUsedOrPlayedThisPlayPhase = false;
        }
    }

    private void OnPhaseEnd(PhaseEndEvent evt)
    {
        // Only process for the owner's play phase end
        if (_game is null || _owner is null || evt.PlayerSeat != _owner.Seat)
            return;

        // Check condition when play phase ends
        if (evt.Phase == Phase.Play)
        {
            // Check if skill is active
            if (!IsActive(_game, _owner))
                return;

            // If no Slash was used or played during play phase, set flag to skip discard phase
            if (!_slashUsedOrPlayedThisPlayPhase)
            {
                _owner.Flags["SkipDiscardPhase"] = true;
            }
        }
    }

    private void OnCardUsed(CardUsedEvent evt)
    {
        // Only track for the owner during play phase
        if (_game is null || _owner is null || evt.SourcePlayerSeat != _owner.Seat)
            return;

        // Only track during play phase
        if (_game.CurrentPhase != Phase.Play)
            return;

        // Track if Slash was used
        if (evt.CardSubType == CardSubType.Slash)
        {
            _slashUsedOrPlayedThisPlayPhase = true;
        }
    }

    private void OnCardPlayed(CardPlayedEvent evt)
    {
        // Only track for the owner during play phase
        if (_game is null || _owner is null || evt.ResponderSeat != _owner.Seat)
            return;

        // Only track during play phase
        if (_game.CurrentPhase != Phase.Play)
            return;

        // Track if Slash was played (in response)
        if (evt.CardSubType == CardSubType.Slash)
        {
            _slashUsedOrPlayedThisPlayPhase = true;
        }
    }
}

/// <summary>
/// Factory for creating KejiSkill instances.
/// </summary>
public sealed class KejiSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new KejiSkill();
    }
}
