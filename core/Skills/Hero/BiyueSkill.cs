using System;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Biyue (闭月) skill: Optional trigger skill that allows you to draw 1 card during your End Phase.
/// </summary>
public sealed class BiyueSkill : BaseSkill
{
    private Game? _game;
    private Player? _owner;
    private IEventBus? _eventBus;
    private ICardMoveService? _cardMoveService;
    private Func<ChoiceRequest, ChoiceResult>? _getPlayerChoice;

    /// <inheritdoc />
    public override string Id => "biyue";

    /// <inheritdoc />
    public override string Name => "闭月";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Trigger;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.None;

    /// <summary>
    /// Sets the card move service for drawing cards.
    /// This is called by the skill manager during attachment.
    /// </summary>
    public void SetCardMoveService(ICardMoveService cardMoveService)
    {
        _cardMoveService = cardMoveService ?? throw new ArgumentNullException(nameof(cardMoveService));
    }

    /// <summary>
    /// Sets the player choice function for optional trigger.
    /// This is called by the skill manager during attachment.
    /// </summary>
    public void SetGetPlayerChoice(Func<ChoiceRequest, ChoiceResult> getPlayerChoice)
    {
        _getPlayerChoice = getPlayerChoice;
    }

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
    }

    /// <inheritdoc />
    public override void Detach(Game game, Player owner, IEventBus eventBus)
    {
        if (eventBus is null)
            return;

        eventBus.Unsubscribe<PhaseStartEvent>(OnPhaseStart);

        _game = null;
        _owner = null;
        _eventBus = null;
        _cardMoveService = null;
        _getPlayerChoice = null;
    }

    private void OnPhaseStart(PhaseStartEvent evt)
    {
        if (_game is null || _owner is null || _cardMoveService is null)
            return;

        // Only trigger during End Phase
        if (evt.Phase != Phase.End)
            return;

        // Only trigger for the owner's own End Phase
        if (evt.PlayerSeat != _owner.Seat)
            return;

        // Check if skill is active
        if (!IsActive(_game, _owner))
            return;

        // Ask player if they want to activate Biyue
        if (_getPlayerChoice is not null)
        {
            var confirmRequest = new ChoiceRequest(
                RequestId: Guid.NewGuid().ToString(),
                PlayerSeat: _owner.Seat,
                ChoiceType: ChoiceType.Confirm,
                TargetConstraints: null,
                AllowedCards: null,
                ResponseWindowId: null,
                CanPass: true // Player can choose not to activate
            );

            try
            {
                var confirmResult = _getPlayerChoice(confirmRequest);
                if (confirmResult?.Confirmed != true)
                {
                    return; // Player chose not to activate
                }
            }
            catch
            {
                // If getting choice fails, skip activation
                return;
            }
        }
        else
        {
            // If no getPlayerChoice, skip (Biyue requires player choice)
            return;
        }

        // Draw 1 card
        try
        {
            _cardMoveService.DrawCards(_game, _owner, 1);
        }
        catch (Exception)
        {
            // If drawing fails (e.g., draw pile empty), silently ignore
            // This matches the behavior of other trigger skills
        }
    }
}

/// <summary>
/// Factory for creating BiyueSkill instances.
/// </summary>
public sealed class BiyueSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new BiyueSkill();
    }
}

