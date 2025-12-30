using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Lijian (离间) skill: Active skill that allows discarding 1 card to make one male character
/// use a Duel against another male character, once per play phase.
/// </summary>
public sealed class LijianSkill : BaseSkill, IPhaseLimitedActionProvidingSkill
{
    /// <inheritdoc />
    public override string Id => "lijian";

    /// <inheritdoc />
    public override string Name => "离间";

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
        return $"lijian_used_playphase_turn_{game.TurnNumber}_seat_{owner.Seat}";
    }

    /// <summary>
    /// Gets the usage key for tracking skill usage (static helper for nested resolver).
    /// </summary>
    private static string GetUsageKeyStatic(Game game, Player owner)
    {
        return $"lijian_used_playphase_turn_{game.TurnNumber}_seat_{owner.Seat}";
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

        // 3. Owner must have at least 1 discardable card (hand or equipment)
        var availableCards = GetAvailableCardsForDiscard(owner);
        if (availableCards.Count < 1)
            return null;

        // 4. At least two valid male targets exist (alive male players)
        var malePlayers = game.Players
            .Where(p => p.IsAlive && p.Gender == Gender.Male)
            .ToList();

        if (malePlayers.Count < 2)
            return null;

        // Create action (requires card selection and target selection)
        return new ActionDescriptor(
            ActionId: "UseLijian",
            DisplayKey: "action.useLijian",
            RequiresTargets: true,
            TargetConstraints: new TargetConstraints(
                MinTargets: 2,
                MaxTargets: 2,
                FilterType: TargetFilterType.Any),
            CardCandidates: availableCards);
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

        return availableCards;
    }

    /// <summary>
    /// Creates the main resolver for Lijian skill execution flow.
    /// </summary>
    /// <param name="owner">The player who owns the Lijian skill.</param>
    /// <returns>A resolver that orchestrates the entire Lijian skill execution flow.</returns>
    public static IResolver CreateMainResolver(Player owner)
    {
        if (owner is null)
            throw new ArgumentNullException(nameof(owner));

        return new LijianMainResolver(owner);
    }

    /// <summary>
    /// Main resolver for Lijian skill execution.
    /// Handles the complete flow: select card to discard, select two male targets, discard card, create virtual Duel, execute Duel.
    /// </summary>
    private sealed class LijianMainResolver : IResolver
    {
        private readonly Player _owner;

        public LijianMainResolver(Player owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public ResolutionResult Resolve(ResolutionContext context)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));

            var game = context.Game;
            var choice = context.Choice;

            // Step 1: Validate context and choice
            var validationResult = ValidateContext(context);
            if (!validationResult.Success)
                return validationResult;

            // Step 2: Validate selected card
            var cardValidationResult = ValidateSelectedCard(choice!);
            if (!cardValidationResult.Success)
                return cardValidationResult.Result!;

            var cardToDiscard = cardValidationResult.Card!;

            // Step 3: Validate and get targets
            var (targetSuccess, duelSource, duelTarget) = ValidateAndGetTargets(game, choice!);
            if (!targetSuccess.Success)
                return targetSuccess;

            // Step 4: Validate that DuelSource can use Duel on DuelTarget
            var duelValidationResult = ValidateDuelUsage(context, duelSource!, duelTarget!);
            if (!duelValidationResult.Success)
                return duelValidationResult;

            // Step 5: Discard the card (pay cost)
            var discardResult = DiscardCard(context, game, cardToDiscard);
            if (!discardResult.Success)
                return discardResult.Result!;

            // Step 6: Create virtual Duel card and execute Duel
            var duelResult = ExecuteDuel(context, game, duelSource!, duelTarget!);
            if (!duelResult.Success)
                return duelResult;

            // Step 7: Mark skill as used
            MarkSkillAsUsed(game);

            return ResolutionResult.SuccessResult;
        }

        /// <summary>
        /// Validates that the context and choice are valid.
        /// </summary>
        private ResolutionResult ValidateContext(ResolutionContext context)
        {
            if (context.Choice is null)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "resolution.lijian.noChoice");
            }

            if (context.CardMoveService is null)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "resolution.lijian.missingCardMoveService");
            }

            if (context.RuleService is null)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "resolution.lijian.missingRuleService");
            }

            return ResolutionResult.SuccessResult;
        }

        /// <summary>
        /// Validates the selected card to discard.
        /// </summary>
        private (bool Success, ResolutionResult? Result, Card? Card) ValidateSelectedCard(ChoiceResult choice)
        {
            var selectedCardIds = choice.SelectedCardIds;
            if (selectedCardIds is null || selectedCardIds.Count != 1)
            {
                return (false, ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "resolution.lijian.invalidCardSelection"), null);
            }

            var cardId = selectedCardIds[0];
            
            // Get card directly from zones to ensure we have the actual object reference
            var handCard = _owner.HandZone.Cards?.FirstOrDefault(c => c.Id == cardId);
            if (handCard is not null)
            {
                return (true, null, handCard);
            }
            
            var equipmentCard = _owner.EquipmentZone.Cards?.FirstOrDefault(c => c.Id == cardId);
            if (equipmentCard is not null)
            {
                return (true, null, equipmentCard);
            }

            return (false, ResolutionResult.Failure(
                ResolutionErrorCode.CardNotFound,
                messageKey: "resolution.lijian.cardNotFound",
                details: new { CardId = cardId }), null);
        }

        /// <summary>
        /// Validates and extracts the two male targets (DuelSource and DuelTarget).
        /// </summary>
        private (ResolutionResult Success, Player? DuelSource, Player? DuelTarget) ValidateAndGetTargets(
            Game game,
            ChoiceResult choice)
        {
            var selectedTargetSeats = choice.SelectedTargetSeats;
            if (selectedTargetSeats is null || selectedTargetSeats.Count != 2)
            {
                return (ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidTarget,
                    messageKey: "resolution.lijian.invalidTargetSelection"), null, null);
            }

            var seat1 = selectedTargetSeats[0];
            var seat2 = selectedTargetSeats[1];

            // Check that targets are different
            if (seat1 == seat2)
            {
                return (ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidTarget,
                    messageKey: "resolution.lijian.sameTarget"), null, null);
            }

            var player1 = game.Players.FirstOrDefault(p => p.Seat == seat1);
            var player2 = game.Players.FirstOrDefault(p => p.Seat == seat2);

            if (player1 is null || !player1.IsAlive || player1.Gender != Gender.Male)
            {
                return (ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidTarget,
                    messageKey: "resolution.lijian.invalidTarget1"), null, null);
            }

            if (player2 is null || !player2.IsAlive || player2.Gender != Gender.Male)
            {
                return (ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidTarget,
                    messageKey: "resolution.lijian.invalidTarget2"), null, null);
            }

            // DuelSource is the first target, DuelTarget is the second target
            return (ResolutionResult.SuccessResult, player1, player2);
        }

        /// <summary>
        /// Validates that DuelSource can use Duel on DuelTarget.
        /// For virtual cards generated by skills, we only check target validity,
        /// not phase restrictions (since skill-generated cards can be used outside normal phases).
        /// </summary>
        private ResolutionResult ValidateDuelUsage(
            ResolutionContext context,
            Player duelSource,
            Player duelTarget)
        {
            // Basic target validation (same as DuelResolver checks)
            if (!duelSource.IsAlive)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "resolution.lijian.duelSourceNotAlive");
            }

            if (!duelTarget.IsAlive)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.TargetNotAlive,
                    messageKey: "resolution.lijian.duelTargetNotAlive");
            }

            if (duelSource.Seat == duelTarget.Seat)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidTarget,
                    messageKey: "resolution.lijian.cannotDuelSelf");
            }

            // Check if target is within legal targets using GetLegalTargetsForUse
            // This validates range and other target constraints without checking phase
            var virtualDuel = CreateVirtualDuelCard();
            var usageContext = new CardUsageContext(
                context.Game,
                duelSource,
                virtualDuel,
                context.Game.Players,
                IsExtraAction: false,
                UsageCountThisTurn: 0);

            var legalTargets = context.RuleService!.GetLegalTargetsForUse(usageContext);
            if (!legalTargets.HasAny || !legalTargets.Items.Any(p => p.Seat == duelTarget.Seat))
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidTarget,
                    messageKey: "resolution.lijian.duelTargetNotLegal",
                    details: new { DuelTargetSeat = duelTarget.Seat });
            }

            return ResolutionResult.SuccessResult;
        }

        /// <summary>
        /// Discards the selected card.
        /// </summary>
        private (bool Success, ResolutionResult? Result) DiscardCard(
            ResolutionContext context,
            Game game,
            Card card)
        {
            try
            {
                // Use the card object directly from ValidateSelectedCard (which gets it from zones)
                // This ensures we have the actual object reference needed by DiscardFromHand
                if (_owner.HandZone.Cards?.Contains(card) == true)
                {
                    context.CardMoveService!.DiscardFromHand(game, _owner, new[] { card });
                }
                else if (_owner.EquipmentZone.Cards?.Contains(card) == true && _owner.EquipmentZone is Zone equipmentZone)
                {
                    var moveDescriptor = new CardMoveDescriptor(
                        SourceZone: equipmentZone,
                        TargetZone: game.DiscardPile,
                        Cards: new[] { card },
                        Reason: CardMoveReason.Discard,
                        Ordering: CardMoveOrdering.ToTop,
                        Game: game);
                    context.CardMoveService.MoveMany(moveDescriptor);
                }
                else
                {
                    return (false, ResolutionResult.Failure(
                        ResolutionErrorCode.CardNotFound,
                        messageKey: "resolution.lijian.cardNotInZone"));
                }
            }
            catch (Exception ex)
            {
                return (false, ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "resolution.lijian.discardFailed",
                    details: new { Exception = ex.Message }));
            }

            return (true, null);
        }

        /// <summary>
        /// Creates a virtual Duel card and executes the Duel with DuelSource as the user.
        /// </summary>
        private ResolutionResult ExecuteDuel(
            ResolutionContext context,
            Game game,
            Player duelSource,
            Player duelTarget)
        {
            // Create virtual Duel card
            var virtualDuel = CreateVirtualDuelCard();

            // Create ActionDescriptor for Duel
            var duelAction = new ActionDescriptor(
                ActionId: "UseDuel",
                DisplayKey: "action.useDuel",
                RequiresTargets: true,
                TargetConstraints: null,
                CardCandidates: new[] { virtualDuel });

            // Create ChoiceResult with target
            var duelChoice = new ChoiceResult(
                RequestId: Guid.NewGuid().ToString(),
                PlayerSeat: duelSource.Seat,
                SelectedTargetSeats: new[] { duelTarget.Seat },
                SelectedCardIds: new[] { virtualDuel.Id },
                SelectedOptionId: null,
                Confirmed: null);

            // Prepare IntermediateResults with virtual card information
            var intermediateResults = new Dictionary<string, object>
            {
                ["CausingCard"] = virtualDuel,
                ["IsVirtualCard"] = true,
                ["GeneratedBySkill"] = "lijian",
                ["SkillOwnerSeat"] = _owner.Seat
            };

            // Create ResolutionContext with DuelSource as SourcePlayer
            var duelContext = new ResolutionContext(
                game,
                duelSource, // DuelSource is the user, not the skill owner
                duelAction,
                duelChoice,
                context.Stack,
                context.CardMoveService,
                context.RuleService,
                PendingDamage: null,
                LogSink: context.LogSink,
                context.GetPlayerChoice,
                intermediateResults,
                context.EventBus,
                context.LogCollector,
                context.SkillManager,
                context.EquipmentSkillRegistry,
                context.JudgementService);

            // Push UseCardResolver to execute the Duel through standard card usage flow
            // This ensures CardUsedEvent is published with DuelSource as the user
            var useCardResolver = new UseCardResolver();
            context.Stack.Push(useCardResolver, duelContext);

            return ResolutionResult.SuccessResult;
        }

        /// <summary>
        /// Creates a virtual Duel card.
        /// </summary>
        private static Card CreateVirtualDuelCard()
        {
            // Use a unique negative ID for virtual cards to avoid conflicts
            var virtualCardId = -Guid.NewGuid().GetHashCode();
            if (virtualCardId > 0)
                virtualCardId = -virtualCardId;

            return new Card
            {
                Id = virtualCardId,
                DefinitionId = "duel",
                Name = "决斗",
                CardType = CardType.Trick,
                CardSubType = CardSubType.Duel,
                Suit = Suit.Spade, // Default suit for virtual card
                Rank = 1 // Default rank for virtual card
            };
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

            return availableCards;
        }

        /// <summary>
        /// Marks the skill as used for the current play phase.
        /// </summary>
        private void MarkSkillAsUsed(Game game)
        {
            var usageKey = LijianSkill.GetUsageKeyStatic(game, _owner);
            _owner.Flags[usageKey] = true;
        }
    }
}

/// <summary>
/// Factory for creating LijianSkill instances.
/// </summary>
public sealed class LijianSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new LijianSkill();
    }
}

