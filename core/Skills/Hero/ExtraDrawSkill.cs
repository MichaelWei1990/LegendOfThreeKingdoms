using System;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Example skill: Extra Draw - allows the owner to draw an extra card during draw phase.
/// This is a locked skill that modifies rules.
/// </summary>
public sealed class ExtraDrawSkill : BaseSkill
{
    private Game? _game;
    private Player? _owner;
    private IEventBus? _eventBus;
    private ICardMoveService? _cardMoveService;

    /// <summary>
    /// Creates a new ExtraDrawSkill instance.
    /// </summary>
    /// <param name="cardMoveService">The card move service to use for drawing cards. If null, the skill will not be able to draw cards.</param>
    public ExtraDrawSkill(ICardMoveService? cardMoveService = null)
    {
        _cardMoveService = cardMoveService;
    }

    /// <summary>
    /// Sets the card move service for this skill instance.
    /// This allows the service to be injected after skill creation.
    /// </summary>
    public void SetCardMoveService(ICardMoveService cardMoveService)
    {
        _cardMoveService = cardMoveService ?? throw new ArgumentNullException(nameof(cardMoveService));
    }

    /// <inheritdoc />
    public override string Id => "extra_draw";

    /// <inheritdoc />
    public override string Name => "Extra Draw";

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
    }

    private void OnPhaseStart(PhaseStartEvent evt)
    {
        // Only trigger for the owner's draw phase
        if (_game is null || _owner is null || _cardMoveService is null)
            return;

        if (evt.PlayerSeat != _owner.Seat || evt.Phase != Phase.Draw)
            return;

        if (!IsActive(_game, _owner))
            return;

        // Draw an extra card
        try
        {
            _cardMoveService.DrawCards(_game, _owner, 1);
        }
        catch
        {
            // If drawing fails (e.g., draw pile is empty), silently ignore
            // In a production system, we might want to log this
        }
    }
}

/// <summary>
/// Factory for creating ExtraDrawSkill instances.
/// </summary>
public sealed class ExtraDrawSkillFactory : ISkillFactory
{
    private readonly ICardMoveService? _cardMoveService;

    /// <summary>
    /// Creates a new ExtraDrawSkillFactory.
    /// </summary>
    /// <param name="cardMoveService">Optional card move service to inject into skill instances.</param>
    public ExtraDrawSkillFactory(ICardMoveService? cardMoveService = null)
    {
        _cardMoveService = cardMoveService;
    }

    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new ExtraDrawSkill(_cardMoveService);
    }
}
