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
/// ZhiHeng (制衡) skill: Active skill that allows the owner to discard any number of cards (at least 1),
/// then draw the same number of cards. Once per play phase.
/// </summary>
public sealed class ZhiHengSkill : BaseSkill, IPhaseLimitedActionProvidingSkill
{
    /// <inheritdoc />
    public override string Id => "zhiheng";

    /// <inheritdoc />
    public override string Name => "制衡";

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
        return $"zhiheng_used_playphase_turn_{game.TurnNumber}_seat_{owner.Seat}";
    }

    /// <summary>
    /// Gets the usage key for tracking skill usage (static helper for nested resolver).
    /// </summary>
    private static string GetUsageKeyStatic(Game game, Player owner)
    {
        return $"zhiheng_used_playphase_turn_{game.TurnNumber}_seat_{owner.Seat}";
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
        {
            return null; // Already used this play phase
        }

        // 3. Owner must have at least 1 discardable card (hand or equipment)
        var availableCards = GetAvailableCardsForDiscard(owner);
        if (availableCards.Count < 1)
            return null;

        // Create action (no targets required, but requires card selection)
        return new ActionDescriptor(
            ActionId: "UseZhiHeng",
            DisplayKey: "action.useZhiHeng",
            RequiresTargets: false,
            TargetConstraints: null,
            CardCandidates: null);
    }

    /// <summary>
    /// Gets available cards that can be discarded from owner (hand + equipment zones).
    /// </summary>
    private static List<Card> GetAvailableCardsForDiscard(Player owner)
    {
        var availableCards = new List<Card>();

        // Add hand cards
        if (owner.HandZone.Cards is not null)
        {
            availableCards.AddRange(owner.HandZone.Cards);
        }

        // Add equipment cards
        if (owner.EquipmentZone.Cards is not null)
        {
            availableCards.AddRange(owner.EquipmentZone.Cards);
        }

        // Exclude judgement zone cards (not discardable for ZhiHeng)

        return availableCards;
    }

    /// <summary>
    /// Creates the main resolver for ZhiHeng skill execution flow.
    /// </summary>
    /// <param name="owner">The player who owns the ZhiHeng skill.</param>
    /// <returns>A resolver that orchestrates the entire ZhiHeng skill execution flow.</returns>
    public static IResolver CreateMainResolver(Player owner)
    {
        if (owner is null)
            throw new ArgumentNullException(nameof(owner));

        return new ZhiHengMainResolver(owner);
    }

    /// <summary>
    /// Main resolver for ZhiHeng skill execution.
    /// Handles the complete flow: select cards to discard, discard them, then draw equal number of cards.
    /// </summary>
    private sealed class ZhiHengMainResolver : IResolver
    {
        private readonly Player _owner;

        public ZhiHengMainResolver(Player owner)
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

            // Step 3: Ask player to select cards to discard
            var selectionResult = SelectCardsToDiscard(context, availableCards);
            if (!selectionResult.Success)
                return selectionResult.Result!;

            var cardsToDiscard = selectionResult.Cards!;

            // Step 4: Separate cards by zone and discard them
            var discardResult = DiscardSelectedCards(game, cardMoveService, cardsToDiscard);
            if (!discardResult.Success)
                return discardResult.Result!;

            var discardedCount = discardResult.Count;

            // Step 5: Draw cards equal to discarded count
            DrawCardsForZhiHeng(context, game, cardMoveService, discardedCount);

            // Step 6: Mark skill as used
            MarkSkillAsUsed(game);

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
                    messageKey: "resolution.zhiheng.missingCardMoveService");
            }

            return ResolutionResult.SuccessResult;
        }

        /// <summary>
        /// Gets available cards for discard and validates that at least one exists.
        /// </summary>
        private (bool Success, ResolutionResult? Result, List<Card>? Cards) GetAndValidateAvailableCards(Game game)
        {
            var availableCards = GetAvailableCardsForDiscard(game, _owner);
            if (availableCards.Count == 0)
            {
                return (false, ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "resolution.zhiheng.noDiscardableCards"), null);
            }

            return (true, null, availableCards);
        }

        /// <summary>
        /// Asks the player to select cards to discard.
        /// </summary>
        private (bool Success, ResolutionResult? Result, List<Card>? Cards) SelectCardsToDiscard(
            ResolutionContext context,
            List<Card> availableCards)
        {
            if (context.GetPlayerChoice is null)
            {
                return (false, ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "resolution.zhiheng.noCardsSelected"), null);
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
                    var cardsToDiscard = availableCards
                        .Where(c => selectResult.SelectedCardIds.Contains(c.Id))
                        .ToList();

                    if (cardsToDiscard.Count > 0)
                    {
                        return (true, null, cardsToDiscard);
                    }
                }
            }
            catch (Exception ex)
            {
                return (false, ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "resolution.zhiheng.cardSelectionFailed",
                    details: new { Exception = ex.Message }), null);
            }

            return (false, ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.zhiheng.noCardsSelected"), null);
        }

        /// <summary>
        /// Separates cards by zone (hand vs equipment).
        /// </summary>
        private (List<Card> HandCards, List<Card> EquipmentCards) SeparateCardsByZone(List<Card> cards)
        {
            var handCards = cards.Where(c => _owner.HandZone.Cards.Contains(c)).ToList();
            var equipmentCards = cards.Where(c => _owner.EquipmentZone.Cards.Contains(c)).ToList();
            return (handCards, equipmentCards);
        }

        /// <summary>
        /// Discards the selected cards and returns the count of successfully discarded cards.
        /// </summary>
        private (bool Success, ResolutionResult? Result, int Count) DiscardSelectedCards(
            Game game,
            ICardMoveService cardMoveService,
            List<Card> cardsToDiscard)
        {
            var (handCards, equipmentCards) = SeparateCardsByZone(cardsToDiscard);
            int discardedCount = 0;

            try
            {
                // Discard hand cards
                if (handCards.Count > 0)
                {
                    cardMoveService.DiscardFromHand(game, _owner, handCards);
                    discardedCount += handCards.Count;
                }

                // Discard equipment cards
                if (equipmentCards.Count > 0 && _owner.EquipmentZone is Zone equipmentZone)
                {
                    var moveDescriptor = new CardMoveDescriptor(
                        SourceZone: equipmentZone,
                        TargetZone: game.DiscardPile,
                        Cards: equipmentCards,
                        Reason: CardMoveReason.Discard,
                        Ordering: CardMoveOrdering.ToTop,
                        Game: game
                    );
                    cardMoveService.MoveMany(moveDescriptor);
                    discardedCount += equipmentCards.Count;
                }
            }
            catch (Exception ex)
            {
                return (false, ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "resolution.zhiheng.discardFailed",
                    details: new { Exception = ex.Message }), 0);
            }

            return (true, null, discardedCount);
        }

        /// <summary>
        /// Draws cards equal to the discarded count. Logs warning if drawing fails but doesn't fail the skill.
        /// </summary>
        private void DrawCardsForZhiHeng(
            ResolutionContext context,
            Game game,
            ICardMoveService cardMoveService,
            int discardedCount)
        {
            if (discardedCount <= 0)
                return;

            try
            {
                cardMoveService.DrawCards(game, _owner, discardedCount);
            }
            catch (Exception ex)
            {
                // If drawing fails (e.g., draw pile empty), log but don't fail the skill
                // The discard has already happened, so we should still mark the skill as used
                if (context.LogSink is not null)
                {
                    var logEntry = new LogEntry
                    {
                        EventType = "ZhiHengDrawFailed",
                        Level = "Warning",
                        Message = $"ZhiHeng draw failed after discarding {discardedCount} cards: {ex.Message}",
                        Data = new
                        {
                            OwnerSeat = _owner.Seat,
                            DiscardedCount = discardedCount,
                            Exception = ex.Message
                        }
                    };
                    context.LogSink.Log(logEntry);
                }
            }
        }

        /// <summary>
        /// Gets available cards that can be discarded from owner (hand + equipment zones).
        /// </summary>
        private static List<Card> GetAvailableCardsForDiscard(Game game, Player owner)
        {
            var availableCards = new List<Card>();

            // Add hand cards
            if (owner.HandZone.Cards is not null)
            {
                availableCards.AddRange(owner.HandZone.Cards);
            }

            // Add equipment cards
            if (owner.EquipmentZone.Cards is not null)
            {
                availableCards.AddRange(owner.EquipmentZone.Cards);
            }

            return availableCards;
        }

        /// <summary>
        /// Marks the skill as used for the current play phase.
        /// </summary>
        private void MarkSkillAsUsed(Game game)
        {
            // Directly set the flag using the static helper method
            var usageKey = ZhiHengSkill.GetUsageKeyStatic(game, _owner);
            _owner.Flags[usageKey] = true;
        }
    }
}

/// <summary>
/// Factory for creating ZhiHengSkill instances.
/// </summary>
public sealed class ZhiHengSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new ZhiHengSkill();
    }
}

