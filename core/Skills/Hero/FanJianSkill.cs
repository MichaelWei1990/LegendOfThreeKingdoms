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
/// FanJian (反间) skill: Active skill that allows the owner to make another player guess a suit,
/// then that player draws a random card from owner's hand and reveals it. If the guess is wrong, the target takes 1 damage.
/// Once per play phase.
/// </summary>
public sealed class FanJianSkill : BaseSkill, IPhaseLimitedActionProvidingSkill
{
    /// <inheritdoc />
    public override string Id => "fanjian";

    /// <inheritdoc />
    public override string Name => "反间";

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
        return $"fanjian_used_playphase_turn_{game.TurnNumber}_seat_{owner.Seat}";
    }

    /// <summary>
    /// Gets the usage key for tracking skill usage (static helper for nested resolver).
    /// </summary>
    private static string GetUsageKeyStatic(Game game, Player owner)
    {
        return $"fanjian_used_playphase_turn_{game.TurnNumber}_seat_{owner.Seat}";
    }

    /// <inheritdoc />
    public ActionDescriptor? GenerateAction(Game game, Player owner)
    {
        // Check conditions:
        // 1. Must be in play phase
        if (game.CurrentPhase != Phase.Play || game.CurrentPlayerSeat != owner.Seat)
            return null;

        // 2. Owner has at least 1 hand card
        if (owner.HandZone.Cards.Count < 1)
            return null;

        // 3. Check if already used this play phase
        if (IsAlreadyUsed(game, owner))
        {
            return null; // Already used this play phase
        }

        // 4. At least one valid target exists (other alive player, excluding self)
        var validTargets = game.Players
            .Where(p => p.IsAlive && p.Seat != owner.Seat)
            .ToList();

        if (validTargets.Count == 0)
            return null;

        // Create action with target selection
        return new ActionDescriptor(
            ActionId: "UseFanJian",
            DisplayKey: "action.useFanJian",
            RequiresTargets: true,
            TargetConstraints: new TargetConstraints(
                MinTargets: 1,
                MaxTargets: 1,
                FilterType: TargetFilterType.Any),
            CardCandidates: null);
    }

    /// <summary>
    /// Creates the main resolver for FanJian skill execution flow.
    /// </summary>
    /// <param name="owner">The player who owns the FanJian skill.</param>
    /// <returns>A resolver that orchestrates the entire FanJian skill execution flow.</returns>
    public static IResolver CreateMainResolver(Player owner)
    {
        if (owner is null)
            throw new ArgumentNullException(nameof(owner));

        return new FanJianMainResolver(owner);
    }

    /// <summary>
    /// Main resolver for FanJian skill execution.
    /// Handles the complete flow: target guesses suit, draws card, reveals it, and takes damage if wrong.
    /// </summary>
    private sealed class FanJianMainResolver : IResolver
    {
        private readonly Player _owner;

        public FanJianMainResolver(Player owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public ResolutionResult Resolve(ResolutionContext context)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));

            // Validate context and choice
            var validationResult = ValidateContext(context);
            if (!validationResult.Success)
                return validationResult;

            var game = context.Game;
            var choice = context.Choice!;

            // Validate and get target
            var (targetSuccess, target) = ValidateAndGetTarget(game, choice);
            if (!targetSuccess.Success)
                return targetSuccess;

            if (target is null)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidTarget,
                    messageKey: "fanjian.targetNotFound");
            }

            // Validate owner has hand cards
            if (_owner.HandZone.Cards.Count == 0)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "fanjian.noHandCards");
            }

            // Step 1: Ask target to guess a suit
            var (suitSuccess, guessedSuit) = AskTargetToGuessSuit(context, target);
            if (!suitSuccess.Success)
                return suitSuccess;

            if (guessedSuit is null)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "fanjian.suitNotSelected");
            }

            // Step 2: Randomly extract one card from owner's hand
            var (cardSuccess, extractedCard) = ExtractRandomCardFromHand(game, _owner);
            if (!cardSuccess.Success)
                return cardSuccess;

            if (extractedCard is null)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.CardNotFound,
                    messageKey: "fanjian.cardNotFound");
            }

            // Step 3: Move card from owner's hand to target's hand
            if (context.CardMoveService is null)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "CardMoveService is required for FanJian");
            }

            var moveResult = MoveCardToTargetHand(context, game, extractedCard, target);
            if (!moveResult.Success)
                return moveResult;

            // Step 4: Reveal the card (to all players)
            RevealCard(context, game, extractedCard, target);

            // Step 5: Check if guess is wrong and deal damage
            if (extractedCard.Suit != guessedSuit)
            {
                var damageResult = DealDamage(context, game, target);
                if (!damageResult.Success)
                    return damageResult;
            }

            // Step 6: Mark skill as used
            MarkSkillAsUsed(game);

            return ResolutionResult.SuccessResult;
        }

        private ResolutionResult ValidateContext(ResolutionContext context)
        {
            if (context.Choice is null)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "fanjian.choiceRequired");
            }

            return ResolutionResult.SuccessResult;
        }

        private (ResolutionResult Success, Player? Target) ValidateAndGetTarget(Game game, ChoiceResult choice)
        {
            if (choice.SelectedTargetSeats is null || choice.SelectedTargetSeats.Count == 0)
            {
                return (ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidTarget,
                    messageKey: "fanjian.targetRequired"), null);
            }

            var targetSeat = choice.SelectedTargetSeats[0];
            var target = game.Players.FirstOrDefault(p => p.Seat == targetSeat);

            if (target is null)
            {
                return (ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidTarget,
                    messageKey: "fanjian.targetNotFound"), null);
            }

            if (!target.IsAlive)
            {
                return (ResolutionResult.Failure(
                    ResolutionErrorCode.TargetNotAlive,
                    messageKey: "fanjian.targetNotAlive"), null);
            }

            if (target.Seat == _owner.Seat)
            {
                return (ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidTarget,
                    messageKey: "fanjian.cannotTargetSelf"), null);
            }

            return (ResolutionResult.SuccessResult, target);
        }

        private (ResolutionResult Success, Suit? Suit) AskTargetToGuessSuit(ResolutionContext context, Player target)
        {
            if (context.GetPlayerChoice is null)
            {
                // If no choice provider, default to Spade (for testing)
                return (ResolutionResult.SuccessResult, Suit.Spade);
            }

            // Create a choice request for selecting a suit
            // Use SelectOption for suit selection
            // The UI layer should display the four suits as options
            var choiceRequest = new ChoiceRequest(
                RequestId: Guid.NewGuid().ToString(),
                PlayerSeat: target.Seat,
                ChoiceType: ChoiceType.SelectOption,
                TargetConstraints: null,
                AllowedCards: null,
                ResponseWindowId: null,
                CanPass: false);

            try
            {
                var choiceResult = context.GetPlayerChoice(choiceRequest);
                if (choiceResult is null)
                {
                    return (ResolutionResult.Failure(
                        ResolutionErrorCode.InvalidState,
                        messageKey: "fanjian.suitChoiceRequired"), null);
                }

                // Parse the selected option ID as a suit
                // The UI should return the suit name in SelectedOptionId (e.g., "Spade", "Heart", "Club", "Diamond")
                if (string.IsNullOrWhiteSpace(choiceResult.SelectedOptionId))
                {
                    return (ResolutionResult.Failure(
                        ResolutionErrorCode.InvalidState,
                        messageKey: "fanjian.suitNotSelected"), null);
                }

                // Parse the suit from the option ID
                var suitString = choiceResult.SelectedOptionId;
                if (!Enum.TryParse<Suit>(suitString, ignoreCase: true, out var guessedSuit))
                {
                    return (ResolutionResult.Failure(
                        ResolutionErrorCode.InvalidState,
                        messageKey: "fanjian.invalidSuit",
                        details: new { SuitString = suitString }), null);
                }

                return (ResolutionResult.SuccessResult, guessedSuit);
            }
            catch (Exception ex)
            {
                return (ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "fanjian.suitChoiceFailed",
                    details: new { Exception = ex.Message }), null);
            }
        }

        private (ResolutionResult Success, Card? Card) ExtractRandomCardFromHand(Game game, Player owner)
        {
            var handCards = owner.HandZone.Cards.ToList();
            if (handCards.Count == 0)
            {
                return (ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "fanjian.noHandCards"), null);
            }

            // Randomly select one card
            var random = new Random();
            var randomIndex = random.Next(handCards.Count);
            var selectedCard = handCards[randomIndex];

            return (ResolutionResult.SuccessResult, selectedCard);
        }

        private ResolutionResult MoveCardToTargetHand(
            ResolutionContext context,
            Game game,
            Card card,
            Player target)
        {
            if (context.CardMoveService is null)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "CardMoveService is required");
            }

            try
            {
                // Find source zone (owner's hand)
                var sourceZone = _owner.HandZone;
                if (sourceZone is not Zone sourceZoneTyped)
                {
                    return ResolutionResult.Failure(
                        ResolutionErrorCode.InvalidState,
                        messageKey: "fanjian.invalidSourceZone");
                }

                // Find target zone (target's hand)
                var targetZone = target.HandZone;
                if (targetZone is not Zone targetZoneTyped)
                {
                    return ResolutionResult.Failure(
                        ResolutionErrorCode.InvalidState,
                        messageKey: "fanjian.invalidTargetZone");
                }

                // Move card
                var moveDescriptor = new CardMoveDescriptor(
                    SourceZone: sourceZoneTyped,
                    TargetZone: targetZoneTyped,
                    Cards: new[] { card },
                    Reason: CardMoveReason.Draw, // Using Draw reason for skill-obtained cards
                    Ordering: CardMoveOrdering.ToTop,
                    Game: game);

                context.CardMoveService.MoveMany(moveDescriptor);

                return ResolutionResult.SuccessResult;
            }
            catch (Exception ex)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "fanjian.moveCardFailed",
                    details: new { Exception = ex.Message });
            }
        }

        private void RevealCard(ResolutionContext context, Game game, Card card, Player revealer)
        {
            // Publish event to notify all players that the card is now revealed
            // This is important for skills that need to react to card reveals
            if (context.EventBus is not null)
            {
                // Note: We could create a CardRevealedEvent, but for now we rely on
                // the card being in the target's hand (which is public knowledge after movement)
                // and the CardMovedEvent that was already published by CardMoveService
            }

            // Log the reveal through LogCollector if available
            if (context.LogCollector is not null)
            {
                // The LogCollector should handle the reveal logging for replay/UI purposes
                // The card is now visible to all players after being moved to target's hand
            }
        }

        private ResolutionResult DealDamage(ResolutionContext context, Game game, Player target)
        {
            // Create damage descriptor
            var damage = new DamageDescriptor(
                SourceSeat: _owner.Seat,
                TargetSeat: target.Seat,
                Amount: 1,
                Type: DamageType.Normal,
                Reason: "反间");

            // Push damage resolver onto the stack
            var damageResolver = new DamageResolver();
            var damageContext = new ResolutionContext(
                game,
                _owner,
                context.Action,
                context.Choice,
                context.Stack,
                context.CardMoveService,
                context.RuleService,
                PendingDamage: damage,
                LogSink: context.LogSink,
                GetPlayerChoice: context.GetPlayerChoice,
                IntermediateResults: context.IntermediateResults,
                EventBus: context.EventBus,
                LogCollector: context.LogCollector,
                SkillManager: context.SkillManager,
                EquipmentSkillRegistry: context.EquipmentSkillRegistry,
                JudgementService: context.JudgementService);

            context.Stack.Push(damageResolver, damageContext);

            return ResolutionResult.SuccessResult;
        }

        private void MarkSkillAsUsed(Game game)
        {
            // Directly set the flag using the static helper method
            var usageKey = FanJianSkill.GetUsageKeyStatic(game, _owner);
            _owner.Flags[usageKey] = true;
        }
    }
}

/// <summary>
/// Factory for creating FanJianSkill instances.
/// </summary>
public sealed class FanJianSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new FanJianSkill();
    }
}

