using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// JieYin (结姻) skill: Active skill that allows discarding 2 hand cards to recover 1 HP for both owner and a wounded male target, once per play phase.
/// </summary>
public sealed class JieYinSkill : BaseSkill, IActionProvidingSkill
{
    /// <inheritdoc />
    public override string Id => "jieyin";

    /// <inheritdoc />
    public override string Name => "结姻";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Active;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.InitiatesChoices;

    /// <inheritdoc />
    public ActionDescriptor? GenerateAction(Game game, Player owner)
    {
        // Check conditions:
        // 1. Must be in play phase
        if (game.CurrentPhase != Phase.Play || game.CurrentPlayerSeat != owner.Seat)
            return null;

        // 2. Player has at least 2 hand cards
        if (owner.HandZone.Cards.Count < 2)
            return null;

        // 3. Check if already used this play phase
        var usageKey = $"jieyin_used_playphase_turn_{game.TurnNumber}_seat_{owner.Seat}";
        if (owner.Flags.ContainsKey(usageKey))
        {
            return null; // Already used this play phase
        }

        // 4. At least one valid target exists (wounded male player, excluding self)
        var validTargets = game.Players
            .Where(p => p.IsAlive 
                && p.Seat != owner.Seat 
                && p.Gender == Gender.Male 
                && p.CurrentHealth < p.MaxHealth)
            .ToList();

        if (validTargets.Count == 0)
            return null;

        // Create action with all hand cards as candidates (player will select 2)
        return new ActionDescriptor(
            ActionId: "UseJieYin",
            DisplayKey: "action.useJieYin",
            RequiresTargets: true,
            TargetConstraints: new TargetConstraints(
                MinTargets: 1,
                MaxTargets: 1,
                FilterType: TargetFilterType.Any),
            CardCandidates: owner.HandZone.Cards.ToList());
    }

    /// <summary>
    /// Creates the main resolver for JieYin skill execution flow.
    /// </summary>
    /// <param name="owner">The player who owns the JieYin skill.</param>
    /// <returns>A resolver that orchestrates the entire JieYin skill execution flow.</returns>
    public static IResolver CreateMainResolver(Player owner)
    {
        if (owner is null)
            throw new ArgumentNullException(nameof(owner));

        return new JieYinMainResolver(owner);
    }

    /// <summary>
    /// Main resolver for JieYin skill execution.
    /// Handles the complete flow: discard 2 cards, then recover 1 HP for both owner and target.
    /// </summary>
    private sealed class JieYinMainResolver : IResolver
    {
        private readonly Player _owner;

        public JieYinMainResolver(Player owner)
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

            // Validate selected cards
            var cardsValidationResult = ValidateSelectedCards(choice);
            if (!cardsValidationResult.Success)
                return cardsValidationResult;

            // Validate and get target
            var (targetSuccess, target) = ValidateAndGetTarget(game, choice);
            if (!targetSuccess.Success)
                return targetSuccess;

            if (target is null)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidTarget,
                    messageKey: "Target validation failed");
            }

            // Get selected cards from hand
            var (cardsSuccess, selectedCards) = GetSelectedCardsFromHand(choice);
            if (!cardsSuccess.Success)
                return cardsSuccess;

            if (selectedCards is null)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.CardNotFound,
                    messageKey: "Failed to get selected cards");
            }

            // Validate card move service
            if (context.CardMoveService is null)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "CardMoveService is required for JieYin");
            }

            // Discard cards
            var discardResult = DiscardCards(context, game, selectedCards);
            if (!discardResult.Success)
                return discardResult;

            // Mark skill as used
            MarkSkillAsUsed(game);

            // Recover health for both owner and target
            var ownerRecover = RecoverHealth(_owner, 1);
            var targetRecover = RecoverHealth(target, 1);

            // Log effect
            LogEffect(context, selectedCards, ownerRecover, targetRecover, target);

            return ResolutionResult.SuccessResult;
        }

        private ResolutionResult ValidateContext(ResolutionContext context)
        {
            if (context.Choice is null)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "JieYin requires a choice with selected cards and target");
            }

            return ResolutionResult.SuccessResult;
        }

        private ResolutionResult ValidateSelectedCards(ChoiceResult choice)
        {
            if (choice.SelectedCardIds is null || choice.SelectedCardIds.Count != 2)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "JieYin requires exactly 2 selected cards");
            }

            return ResolutionResult.SuccessResult;
        }

        private (ResolutionResult Success, Player? Target) ValidateAndGetTarget(Game game, ChoiceResult choice)
        {
            if (choice.SelectedTargetSeats is null || choice.SelectedTargetSeats.Count != 1)
            {
                return (ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "JieYin requires exactly 1 selected target"), null);
            }

            var targetSeat = choice.SelectedTargetSeats[0];
            var target = game.Players.FirstOrDefault(p => p.Seat == targetSeat);
            if (target is null)
            {
                return (ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidTarget,
                    messageKey: $"Target player at seat {targetSeat} not found"), null);
            }

            // Validate target conditions
            var validationResult = ValidateTargetConditions(target);
            if (!validationResult.Success)
                return (validationResult, null);

            return (ResolutionResult.SuccessResult, target);
        }

        private ResolutionResult ValidateTargetConditions(Player target)
        {
            if (!target.IsAlive)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.TargetNotAlive,
                    messageKey: "Target player must be alive");
            }

            if (target.Gender != Gender.Male)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidTarget,
                    messageKey: "Target must be male");
            }

            if (target.CurrentHealth >= target.MaxHealth)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidTarget,
                    messageKey: "Target must be wounded");
            }

            if (target.Seat == _owner.Seat)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidTarget,
                    messageKey: "Target cannot be self");
            }

            return ResolutionResult.SuccessResult;
        }

        private (ResolutionResult Success, List<Card>? Cards) GetSelectedCardsFromHand(ChoiceResult choice)
        {
            var selectedCards = new List<Card>();
            foreach (var cardId in choice.SelectedCardIds!)
            {
                var card = _owner.HandZone.Cards.FirstOrDefault(c => c.Id == cardId);
                if (card is null)
                {
                    return (ResolutionResult.Failure(
                        ResolutionErrorCode.CardNotFound,
                        messageKey: $"Selected card {cardId} not found in player's hand"), null);
                }
                selectedCards.Add(card);
            }

            return (ResolutionResult.SuccessResult, selectedCards);
        }

        private ResolutionResult DiscardCards(ResolutionContext context, Game game, List<Card> selectedCards)
        {
            try
            {
                context.CardMoveService!.DiscardFromHand(game, _owner, selectedCards);
                return ResolutionResult.SuccessResult;
            }
            catch (Exception ex)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: $"Failed to discard cards: {ex.Message}");
            }
        }

        private void MarkSkillAsUsed(Game game)
        {
            var usageKey = $"jieyin_used_playphase_turn_{game.TurnNumber}_seat_{_owner.Seat}";
            _owner.Flags[usageKey] = true;
        }

        private (int PreviousHealth, int NewHealth, int ActualRecover) RecoverHealth(Player player, int amount)
        {
            var previousHealth = player.CurrentHealth;
            player.CurrentHealth = Math.Min(player.CurrentHealth + amount, player.MaxHealth);
            var actualRecover = player.CurrentHealth - previousHealth;
            return (previousHealth, player.CurrentHealth, actualRecover);
        }

        private void LogEffect(
            ResolutionContext context,
            List<Card> selectedCards,
            (int PreviousHealth, int NewHealth, int ActualRecover) ownerRecover,
            (int PreviousHealth, int NewHealth, int ActualRecover) targetRecover,
            Player target)
        {
            if (context.LogSink is null)
                return;

            var logEntry = new LogEntry
            {
                EventType = "JieYinEffect",
                Level = "Info",
                Message = $"JieYin: Player {_owner.Seat} discarded 2 cards, recovered {ownerRecover.ActualRecover} HP, and player {target.Seat} recovered {targetRecover.ActualRecover} HP",
                Data = new
                {
                    OwnerSeat = _owner.Seat,
                    TargetSeat = target.Seat,
                    DiscardedCardIds = selectedCards.Select(c => c.Id).ToArray(),
                    OwnerPreviousHealth = ownerRecover.PreviousHealth,
                    OwnerNewHealth = ownerRecover.NewHealth,
                    OwnerActualRecover = ownerRecover.ActualRecover,
                    TargetPreviousHealth = targetRecover.PreviousHealth,
                    TargetNewHealth = targetRecover.NewHealth,
                    TargetActualRecover = targetRecover.ActualRecover
                }
            };
            context.LogSink.Log(logEntry);
        }
    }
}

/// <summary>
/// Factory for creating JieYinSkill instances.
/// </summary>
public sealed class JieYinSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new JieYinSkill();
    }
}

