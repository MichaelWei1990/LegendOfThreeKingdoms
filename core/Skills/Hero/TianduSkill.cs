using System;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Judgement;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Tiandu skill: When your judgement card takes effect, you obtain that judgement card.
/// This is a locked passive trigger skill that responds to JudgementCompletedEvent.
/// </summary>
public sealed class TianduSkill : BaseSkill
{
    private Game? _game;
    private Player? _owner;
    private IEventBus? _eventBus;
    private ICardMoveService? _cardMoveService;

    /// <summary>
    /// Creates a new TianduSkill instance.
    /// </summary>
    /// <param name="cardMoveService">The card move service to use for moving cards. If null, the skill will not be able to move cards.</param>
    public TianduSkill(ICardMoveService? cardMoveService = null)
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
    public override string Id => "tiandu";

    /// <inheritdoc />
    public override string Name => "天妒";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Locked;

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

        eventBus.Subscribe<JudgementCompletedEvent>(OnJudgementCompleted);
    }

    /// <inheritdoc />
    public override void Detach(Game game, Player owner, IEventBus eventBus)
    {
        if (eventBus is null)
            return;

        eventBus.Unsubscribe<JudgementCompletedEvent>(OnJudgementCompleted);

        _game = null;
        _owner = null;
        _eventBus = null;
    }

    private void OnJudgementCompleted(JudgementCompletedEvent evt)
    {
        // Only trigger if we have all required dependencies
        if (_game is null || _owner is null || _cardMoveService is null)
            return;

        // Check if the judgement owner is the skill owner
        if (evt.Result.JudgeOwnerSeat != _owner.Seat)
            return;

        // Check if skill is active
        if (!IsActive(_game, _owner))
            return;

        // Get the judgement card
        var judgementCard = evt.Result.FinalCard;

        // Check if the card is still in JudgementZone (prevent duplicate processing)
        if (!_owner.JudgementZone.Cards.Contains(judgementCard))
            return;

        // Move the card from JudgementZone to HandZone
        try
        {
            var moveDescriptor = new CardMoveDescriptor(
                SourceZone: _owner.JudgementZone,
                TargetZone: _owner.HandZone,
                Cards: new[] { judgementCard },
                Reason: CardMoveReason.Draw, // Using Draw as the reason since we're obtaining the card
                Ordering: CardMoveOrdering.ToTop,
                Game: _game);

            _cardMoveService.MoveSingle(moveDescriptor);
        }
        catch
        {
            // If moving fails, silently ignore
            // In a production system, we might want to log this
        }
    }
}

/// <summary>
/// Factory for creating TianduSkill instances.
/// </summary>
public sealed class TianduSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new TianduSkill();
    }
}
