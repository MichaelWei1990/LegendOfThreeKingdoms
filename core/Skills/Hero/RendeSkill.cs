using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Rende (仁德) skill: Active skill that allows giving any number of hand cards to other players.
/// If the total number of cards given is 2 or more, the owner restores 1 health point.
/// Once per play phase.
/// </summary>
public sealed class RendeSkill : BaseSkill, IPhaseLimitedActionProvidingSkill
{
    /// <inheritdoc />
    public override string Id => "rende";

    /// <inheritdoc />
    public override string Name => "仁德";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Active;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.InitiatesChoices;

    /// <inheritdoc />
    public SkillUsageLimitType UsageLimitType => SkillUsageLimitType.OncePerPlayPhase;

    /// <inheritdoc />
    public bool IsAlreadyUsed(Game game, Player owner)
    {
        var usageKey = GetUsageKey(game, owner);
        return owner.Flags.ContainsKey(usageKey);
    }

    /// <inheritdoc />
    public void MarkAsUsed(Game game, Player owner)
    {
        var usageKey = GetUsageKey(game, owner);
        owner.Flags[usageKey] = true;
    }

    /// <summary>
    /// Gets the usage key for tracking skill usage.
    /// </summary>
    private string GetUsageKey(Game game, Player owner)
    {
        return $"rende_used_playphase_turn_{game.TurnNumber}_seat_{owner.Seat}";
    }

    /// <summary>
    /// Gets the usage key for tracking skill usage (static helper for nested resolver).
    /// </summary>
    private static string GetUsageKeyStatic(Game game, Player owner)
    {
        return $"rende_used_playphase_turn_{game.TurnNumber}_seat_{owner.Seat}";
    }

    /// <inheritdoc />
    public ActionDescriptor? GenerateAction(Game game, Player owner)
    {
        // Check conditions:
        // 1. Must be in play phase
        if (game.CurrentPhase != Phase.Play || game.CurrentPlayerSeat != owner.Seat)
            return null;

        // 2. Check if already used this play phase
        if (IsAlreadyUsed(game, owner))
            return null;

        // 3. Owner must have at least 1 hand card
        if (owner.HandZone.Cards.Count == 0)
            return null;

        // 4. At least one other alive player exists
        var otherAlivePlayers = game.Players
            .Where(p => p.IsAlive && p.Seat != owner.Seat)
            .ToList();

        if (otherAlivePlayers.Count == 0)
            return null;

        // Create action (requires card selection, no explicit target constraints in action descriptor)
        // The resolver will handle target selection for each card
        return new ActionDescriptor(
            ActionId: "UseRende",
            DisplayKey: "action.useRende",
            RequiresTargets: false, // Targets will be selected per card in resolver
            TargetConstraints: null,
            CardCandidates: owner.HandZone.Cards.ToList());
    }

    /// <summary>
    /// Creates the main resolver for Rende skill execution flow.
    /// </summary>
    /// <param name="owner">The player who owns the Rende skill.</param>
    /// <returns>A resolver that orchestrates the entire Rende skill execution flow.</returns>
    public static IResolver CreateMainResolver(Player owner)
    {
        if (owner is null)
            throw new ArgumentNullException(nameof(owner));

        return new RendeMainResolver(owner);
    }

    /// <summary>
    /// Main resolver for Rende skill execution.
    /// Handles the complete flow: select cards to give, assign recipients, move cards, heal if needed.
    /// </summary>
    private sealed class RendeMainResolver : IResolver
    {
        private readonly Player _owner;

        public RendeMainResolver(Player owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public ResolutionResult Resolve(ResolutionContext context)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));

            // Step 1: Validate required services
            var validationResult = ValidateServices(context);
            if (!validationResult.Success)
                return validationResult;

            var game = context.Game;
            var cardMoveService = context.CardMoveService!;

            // Step 2: Get and validate available cards
            var availableCardsResult = GetAndValidateAvailableCards(game);
            if (!availableCardsResult.Success)
                return availableCardsResult.Result!;

            var availableCards = availableCardsResult.Cards!;

            // Step 3: Ask player to select cards to give (at least 1)
            var selectionResult = SelectCardsToGive(context, availableCards);
            if (!selectionResult.Success)
                return selectionResult.Result!;

            var cardsToGive = selectionResult.Cards!;
            if (cardsToGive.Count == 0)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "resolution.rende.noCardsSelected");
            }

            // Step 4: Assign recipients for each card
            var assignmentResult = AssignRecipients(context, game, cardsToGive);
            if (!assignmentResult.Success)
                return assignmentResult.Result!;

            var cardRecipientMap = assignmentResult.CardRecipientMap!;

            // Step 5: Move cards to recipients
            var moveResult = MoveCardsToRecipients(game, cardMoveService, cardRecipientMap);
            if (!moveResult.Success)
                return moveResult.Result!;

            // Step 6: Heal owner if total cards given >= 2
            var givenCount = cardsToGive.Count;
            if (givenCount >= 2)
            {
                HealOwner(context, game);
            }

            // Step 7: Mark skill as used
            MarkSkillAsUsed(game);

            // Step 8: Publish events if available
            PublishEvents(context, game, cardsToGive, cardRecipientMap);

            return ResolutionResult.SuccessResult;
        }

        /// <summary>
        /// Validates that required services are available.
        /// </summary>
        private static ResolutionResult ValidateServices(ResolutionContext context)
        {
            if (context.CardMoveService is null)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "resolution.rende.missingCardMoveService");
            }

            return ResolutionResult.SuccessResult;
        }

        /// <summary>
        /// Gets available hand cards and validates that at least one exists.
        /// </summary>
        private (bool Success, ResolutionResult? Result, List<Card>? Cards) GetAndValidateAvailableCards(Game game)
        {
            var handCards = _owner.HandZone.Cards.ToList();
            if (handCards.Count == 0)
            {
                return (false, ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "resolution.rende.noHandCards"), null);
            }

            return (true, null, handCards);
        }

        /// <summary>
        /// Asks the player to select cards to give (at least 1 card).
        /// </summary>
        private (bool Success, ResolutionResult? Result, List<Card>? Cards) SelectCardsToGive(
            ResolutionContext context,
            List<Card> availableCards)
        {
            if (context.GetPlayerChoice is null)
            {
                return (false, ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "resolution.rende.noPlayerChoice"), null);
            }

            var selectRequest = new ChoiceRequest(
                RequestId: Guid.NewGuid().ToString(),
                PlayerSeat: _owner.Seat,
                ChoiceType: ChoiceType.SelectCards,
                TargetConstraints: null,
                AllowedCards: availableCards,
                ResponseWindowId: null,
                CanPass: false // Must select at least 1 card
            );

            try
            {
                var selectResult = context.GetPlayerChoice(selectRequest);
                if (selectResult?.SelectedCardIds is not null && selectResult.SelectedCardIds.Count > 0)
                {
                    var cardsToGive = availableCards
                        .Where(c => selectResult.SelectedCardIds.Contains(c.Id))
                        .ToList();

                    if (cardsToGive.Count > 0)
                    {
                        return (true, null, cardsToGive);
                    }
                }
            }
            catch (Exception ex)
            {
                return (false, ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "resolution.rende.cardSelectionFailed",
                    details: new { Exception = ex.Message }), null);
            }

            return (false, ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.rende.noCardsSelected"), null);
        }

        /// <summary>
        /// Assigns a recipient for each card by asking the player to select a target for each card.
        /// </summary>
        private (bool Success, ResolutionResult? Result, Dictionary<Card, Player>? CardRecipientMap) AssignRecipients(
            ResolutionContext context,
            Game game,
            List<Card> cardsToGive)
        {
            if (context.GetPlayerChoice is null)
            {
                return (false, ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "resolution.rende.noPlayerChoice"), null);
            }

            // Get valid recipients (other alive players)
            var validRecipients = game.Players
                .Where(p => p.IsAlive && p.Seat != _owner.Seat)
                .ToList();

            if (validRecipients.Count == 0)
            {
                return (false, ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "resolution.rende.noValidRecipients"), null);
            }

            var cardRecipientMap = new Dictionary<Card, Player>();

            // For each card, ask player to select a recipient
            foreach (var card in cardsToGive)
            {
                var targetRequest = new ChoiceRequest(
                    RequestId: Guid.NewGuid().ToString(),
                    PlayerSeat: _owner.Seat,
                    ChoiceType: ChoiceType.SelectTargets,
                    TargetConstraints: new TargetConstraints(
                        MinTargets: 1,
                        MaxTargets: 1,
                        FilterType: TargetFilterType.Any),
                    AllowedCards: null,
                    ResponseWindowId: null,
                    CanPass: false // Must select a target
                );

                try
                {
                    var targetResult = context.GetPlayerChoice(targetRequest);
                    if (targetResult?.SelectedTargetSeats is null || targetResult.SelectedTargetSeats.Count == 0)
                    {
                        return (false, ResolutionResult.Failure(
                            ResolutionErrorCode.InvalidState,
                            messageKey: "resolution.rende.noTargetSelected"), null);
                    }

                    var recipientSeat = targetResult.SelectedTargetSeats[0];
                    var recipient = validRecipients.FirstOrDefault(p => p.Seat == recipientSeat);

                    if (recipient is null || !recipient.IsAlive || recipient.Seat == _owner.Seat)
                    {
                        return (false, ResolutionResult.Failure(
                            ResolutionErrorCode.InvalidTarget,
                            messageKey: "resolution.rende.invalidRecipient"), null);
                    }

                    cardRecipientMap[card] = recipient;
                }
                catch (Exception ex)
                {
                    return (false, ResolutionResult.Failure(
                        ResolutionErrorCode.InvalidState,
                        messageKey: "resolution.rende.targetSelectionFailed",
                        details: new { Exception = ex.Message }), null);
                }
            }

            return (true, null, cardRecipientMap);
        }

        /// <summary>
        /// Moves cards to their assigned recipients' hand zones.
        /// </summary>
        private (bool Success, ResolutionResult? Result) MoveCardsToRecipients(
            Game game,
            ICardMoveService cardMoveService,
            Dictionary<Card, Player> cardRecipientMap)
        {
            if (_owner.HandZone is not Zone sourceHandZone)
            {
                return (false, ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "resolution.rende.invalidSourceZone"));
            }

            // Group cards by recipient for efficient batch moves
            var cardsByRecipient = cardRecipientMap
                .GroupBy(kvp => kvp.Value)
                .ToDictionary(g => g.Key, g => g.Select(kvp => kvp.Key).ToList());

            try
            {
                foreach (var (recipient, cards) in cardsByRecipient)
                {
                    if (recipient.HandZone is not Zone targetHandZone)
                    {
                        return (false, ResolutionResult.Failure(
                            ResolutionErrorCode.InvalidState,
                            messageKey: "resolution.rende.invalidTargetZone"));
                    }

                    // Verify recipient is still alive
                    if (!recipient.IsAlive)
                    {
                        // Skip this recipient if they died during the selection process
                        continue;
                    }

                    // Verify all cards are still in owner's hand
                    var validCards = cards.Where(c => sourceHandZone.Cards.Contains(c)).ToList();
                    if (validCards.Count == 0)
                        continue;

                    // Move cards to recipient's hand
                    var moveDescriptor = new CardMoveDescriptor(
                        SourceZone: sourceHandZone,
                        TargetZone: targetHandZone,
                        Cards: validCards,
                        Reason: CardMoveReason.Draw, // Using Draw reason for skill-obtained cards
                        Ordering: CardMoveOrdering.ToTop,
                        Game: game
                    );

                    cardMoveService.MoveMany(moveDescriptor);
                }
            }
            catch (Exception ex)
            {
                return (false, ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "resolution.rende.cardMoveFailed",
                    details: new { Exception = ex.Message }));
            }

            return (true, null);
        }

        /// <summary>
        /// Heals the owner by 1 health point if they are injured.
        /// </summary>
        private void HealOwner(ResolutionContext context, Game game)
        {
            var previousHealth = _owner.CurrentHealth;
            _owner.CurrentHealth = Math.Min(_owner.CurrentHealth + 1, _owner.MaxHealth);
            var actualHealAmount = _owner.CurrentHealth - previousHealth;

            // Log the heal if log sink is available
            if (context.LogSink is not null && actualHealAmount > 0)
            {
                var logEntry = new LogEntry
                {
                    EventType = "RendeHeal",
                    Level = "Info",
                    Message = $"Rende: Player {_owner.Seat} restored {actualHealAmount} HP",
                    Data = new
                    {
                        OwnerSeat = _owner.Seat,
                        PreviousHealth = previousHealth,
                        NewHealth = _owner.CurrentHealth,
                        ActualHealAmount = actualHealAmount
                    }
                };
                context.LogSink.Log(logEntry);
            }

            // Publish heal event if event bus is available
            if (context.EventBus is not null && actualHealAmount > 0)
            {
                // TODO: Create HealthRestoredEvent if needed
                // For now, we can use existing events or just log
            }
        }

        /// <summary>
        /// Marks the skill as used for the current play phase.
        /// </summary>
        private void MarkSkillAsUsed(Game game)
        {
            var usageKey = RendeSkill.GetUsageKeyStatic(game, _owner);
            _owner.Flags[usageKey] = true;
        }

        /// <summary>
        /// Publishes events related to Rende skill activation.
        /// </summary>
        private void PublishEvents(
            ResolutionContext context,
            Game game,
            List<Card> cardsGiven,
            Dictionary<Card, Player> cardRecipientMap)
        {
            if (context.EventBus is null)
                return;

            // Publish card moved events for each card given
            foreach (var (card, recipient) in cardRecipientMap)
            {
                var cardMovedEvent = new CardMovedEvent(
                    game,
                    new CardMoveEvent(
                        SourceZoneId: _owner.HandZone.ZoneId,
                        SourceOwnerSeat: _owner.Seat,
                        TargetZoneId: recipient.HandZone.ZoneId,
                        TargetOwnerSeat: recipient.Seat,
                        CardIds: new[] { card.Id },
                        Reason: CardMoveReason.Draw,
                        Ordering: CardMoveOrdering.ToTop,
                        Timing: CardMoveEventTiming.After
                    )
                );
                context.EventBus.Publish(cardMovedEvent);
            }
        }
    }
}

/// <summary>
/// Factory for creating RendeSkill instances.
/// </summary>
public sealed class RendeSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new RendeSkill();
    }
}

