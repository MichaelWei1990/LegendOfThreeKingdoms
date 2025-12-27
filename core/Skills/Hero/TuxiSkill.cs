using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Tuxi (突袭) skill: At the start of draw phase, you can choose to replace normal drawing
/// with obtaining up to 2 hand cards from other players (one from each target).
/// This is an active trigger skill that implements IDrawPhaseReplacementSkill.
/// </summary>
public sealed class TuxiSkill : BaseSkill, IDrawPhaseReplacementSkill
{
    private Game? _game;
    private Player? _owner;
    private IEventBus? _eventBus;

    /// <inheritdoc />
    public override string Id => "tuxi";

    /// <inheritdoc />
    public override string Name => "突袭";

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
        // Tuxi does not subscribe to events directly; it's called by DrawPhaseResolver
    }

    /// <inheritdoc />
    public override void Detach(Game game, Player owner, IEventBus eventBus)
    {
        _game = null;
        _owner = null;
        _eventBus = null;
    }

    /// <inheritdoc />
    public bool CanReplaceDrawPhase(Game game, Player owner)
    {
        // Tuxi can replace draw phase if the owner is alive
        return owner.IsAlive;
    }

    /// <inheritdoc />
    public bool ShouldReplaceDrawPhase(Game game, Player owner, Func<Rules.ChoiceRequest, Rules.ChoiceResult> getPlayerChoice)
    {
        if (!CanReplaceDrawPhase(game, owner))
            return false;

        // Ask player if they want to activate Tuxi
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
    public void ExecuteDrawPhaseReplacement(
        Game game,
        Player owner,
        Func<Rules.ChoiceRequest, Rules.ChoiceResult> getPlayerChoice,
        ICardMoveService cardMoveService,
        IEventBus? eventBus,
        IResolutionStack stack,
        ResolutionContext context)
    {
        if (game is null)
            throw new ArgumentNullException(nameof(game));
        if (owner is null)
            throw new ArgumentNullException(nameof(owner));
        if (getPlayerChoice is null)
            throw new ArgumentNullException(nameof(getPlayerChoice));
        if (cardMoveService is null)
            throw new ArgumentNullException(nameof(cardMoveService));
        if (stack is null)
            throw new ArgumentNullException(nameof(stack));

        // Get valid targets (other alive players with at least one hand card)
        var validTargets = game.Players
            .Where(p => p.Seat != owner.Seat && p.IsAlive && p.HandZone.Cards.Count > 0)
            .ToList();

        if (validTargets.Count == 0)
        {
            // No valid targets, skip Tuxi (fall back to normal draw)
            return;
        }

        // Ask player to select up to 2 targets
        var targetConstraints = new Rules.TargetConstraints(
            MinTargets: 0,
            MaxTargets: Math.Min(2, validTargets.Count),
            FilterType: Rules.TargetFilterType.Any
        );

        var selectTargetsRequest = new Rules.ChoiceRequest(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: owner.Seat,
            ChoiceType: Rules.ChoiceType.SelectTargets,
            TargetConstraints: targetConstraints,
            AllowedCards: null,
            ResponseWindowId: null,
            CanPass: true // Player can choose 0 targets (equivalent to not using Tuxi)
        );

        var selectTargetsResult = getPlayerChoice(selectTargetsRequest);
        var selectedTargetSeats = selectTargetsResult?.SelectedTargetSeats ?? Array.Empty<int>();

        if (selectedTargetSeats.Count == 0)
        {
            // Player chose not to use Tuxi, skip (fall back to normal draw)
            return;
        }

        // Validate selected targets
        var validatedTargetSeats = new List<int>();
        foreach (var seat in selectedTargetSeats)
        {
            var target = game.Players.FirstOrDefault(p => p.Seat == seat);
            if (target is not null && target.IsAlive && target.HandZone.Cards.Count > 0)
            {
                validatedTargetSeats.Add(seat);
            }
        }

        if (validatedTargetSeats.Count == 0)
        {
            // No valid targets after validation, skip
            return;
        }

        // Publish DrawPhaseReplacedEvent if event bus is available
        if (eventBus is not null)
        {
            var replacedEvent = new DrawPhaseReplacedEvent(
                game,
                owner.Seat,
                "突袭",
                validatedTargetSeats);
            eventBus.Publish(replacedEvent);
        }

        // Push TuxiResolver to execute the card stealing
        var tuxiResolver = new TuxiResolver(validatedTargetSeats);
        stack.Push(tuxiResolver, context);
    }
}

/// <summary>
/// Factory for creating TuxiSkill instances.
/// </summary>
public sealed class TuxiSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new TuxiSkill();
    }
}

