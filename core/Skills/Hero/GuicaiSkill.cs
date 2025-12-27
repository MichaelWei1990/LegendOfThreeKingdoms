using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Judgement;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Guicai skill: When any player performs a judgement, after the judgement card is revealed,
/// you can play a hand card to replace the judgement card.
/// This is a trigger skill that responds to JudgementCardRevealedEvent.
/// </summary>
public sealed class GuicaiSkill : BaseSkill, IJudgementModifier
{
    private Game? _game;
    private Player? _owner;
    private IEventBus? _eventBus;

    /// <inheritdoc />
    public override string Id => "guicai";

    /// <inheritdoc />
    public override string Name => "鬼才";

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

        // Note: We don't subscribe to events here because the modification window
        // is handled through the IJudgementModifier interface
    }

    /// <inheritdoc />
    public override void Detach(Game game, Player owner, IEventBus eventBus)
    {
        if (eventBus is null)
            return;

        _game = null;
        _owner = null;
        _eventBus = null;
    }

    /// <inheritdoc />
    public bool CanModify(JudgementContext ctx, Player self)
    {
        if (ctx is null || self is null)
            return false;

        if (_game is null || _owner is null)
            return false;

        // Check if skill is active
        if (!IsActive(_game, _owner))
            return false;

        // Check if owner has hand cards
        if (_owner.HandZone?.Cards is null || _owner.HandZone.Cards.Count == 0)
            return false;

        // Guicai can modify any judgement
        return true;
    }

    /// <inheritdoc />
    public JudgementModifyDecision? GetDecision(
        JudgementContext ctx,
        Player self,
        Func<ChoiceRequest, ChoiceResult>? getPlayerChoice)
    {
        if (ctx is null || self is null)
            return null;

        if (_game is null || _owner is null)
            return null;

        // Check if skill is active
        if (!IsActive(_game, _owner))
            return null;

        // Get hand cards
        var handCards = _owner.HandZone?.Cards;
        if (handCards is null || handCards.Count == 0)
            return null;

        // If no getPlayerChoice function, cannot ask player, skip modification
        if (getPlayerChoice is null)
            return null;

        // Ask player if they want to use Guicai
        var confirmRequest = new ChoiceRequest(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: _owner.Seat,
            ChoiceType: ChoiceType.Confirm,
            TargetConstraints: null,
            AllowedCards: null,
            ResponseWindowId: null,
            CanPass: true // Player can choose not to use Guicai
        );

        try
        {
            var confirmResult = getPlayerChoice(confirmRequest);
            if (confirmResult?.Confirmed != true)
            {
                // Player chose not to use Guicai
                return null;
            }
        }
        catch
        {
            // If getting choice fails, skip modification
            return null;
        }

        // Ask player to select a hand card to replace the judgement card
        var selectRequest = new ChoiceRequest(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: _owner.Seat,
            ChoiceType: ChoiceType.SelectCards,
            TargetConstraints: null,
            AllowedCards: handCards.ToList(),
            ResponseWindowId: null,
            CanPass: false // Must select one card
        );

        try
        {
            var selectResult = getPlayerChoice(selectRequest);
            if (selectResult?.SelectedCardIds is null || selectResult.SelectedCardIds.Count == 0)
            {
                // No card selected, skip modification
                return null;
            }

            // Find the selected card
            var selectedCard = handCards.FirstOrDefault(c => selectResult.SelectedCardIds.Contains(c.Id));
            if (selectedCard is null)
            {
                // Selected card not found, skip modification
                return null;
            }

            // Return the modification decision
            return new JudgementModifyDecision(
                ModifierSeat: _owner.Seat,
                ModifierSource: "鬼才",
                ReplacementCard: selectedCard
            );
        }
        catch
        {
            // If getting choice fails, skip modification
            return null;
        }
    }
}

/// <summary>
/// Factory for creating GuicaiSkill instances.
/// </summary>
public sealed class GuicaiSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new GuicaiSkill();
    }
}

