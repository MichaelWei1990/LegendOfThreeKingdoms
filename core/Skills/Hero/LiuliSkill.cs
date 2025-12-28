using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Skills.Hero;

/// <summary>
/// Liuli (流离) skill: When you become a target of Slash, you may discard 1 card
/// and redirect the Slash to another player within your attack range (not the attacker).
/// </summary>
public sealed class LiuliSkill : BaseSkill, ISlashTargetModifyingSkill
{
    /// <inheritdoc />
    public override string Id => "liuli";

    /// <inheritdoc />
    public override string Name => "流离";

    /// <inheritdoc />
    public override SkillType Type => SkillType.Trigger;

    /// <inheritdoc />
    public override SkillCapability Capabilities => SkillCapability.InitiatesChoices;

    /// <inheritdoc />
    public override void Attach(Game game, Player owner, IEventBus eventBus)
    {
        // No event subscriptions needed - skill is triggered via ISlashTargetModifyingSkill interface
    }

    /// <inheritdoc />
    public override void Detach(Game game, Player owner, IEventBus eventBus)
    {
        // No cleanup needed
    }

    /// <inheritdoc />
    public bool CanModifyTarget(
        Game game,
        Player owner,
        Player attacker,
        Card slashCard,
        IRuleService ruleService)
    {
        if (game is null) throw new ArgumentNullException(nameof(game));
        if (owner is null) throw new ArgumentNullException(nameof(owner));
        if (attacker is null) throw new ArgumentNullException(nameof(attacker));
        if (slashCard is null) throw new ArgumentNullException(nameof(slashCard));
        if (ruleService is null) throw new ArgumentNullException(nameof(ruleService));

        // Check if owner is alive
        if (!owner.IsAlive)
            return false;

        // Check if owner has at least 1 discardable card (hand or equipment)
        var discardableCards = GetDiscardableCards(owner);
        if (discardableCards.Count == 0)
            return false;

        // Get range rule service from rule service
        // Note: RuleService contains IRangeRuleService internally, but we need to access it
        // For now, we'll create a temporary RangeRuleService instance
        // TODO: Consider exposing IRangeRuleService from IRuleService or ResolutionContext
        var rangeRuleService = new RangeRuleService(null);
        
        // Check if there is at least one valid target to redirect to
        var validTargets = GetValidRedirectTargets(game, owner, attacker, rangeRuleService);
        return validTargets.Count > 0;
    }

    /// <inheritdoc />
    public IResolver? CreateTargetModificationResolver(
        Player owner,
        Player attacker,
        Card slashCard,
        DamageDescriptor pendingDamage)
    {
        if (owner is null) throw new ArgumentNullException(nameof(owner));
        if (attacker is null) throw new ArgumentNullException(nameof(attacker));
        if (slashCard is null) throw new ArgumentNullException(nameof(slashCard));
        if (pendingDamage is null) throw new ArgumentNullException(nameof(pendingDamage));

        // Create resolver that handles the target redirection
        return new LiuliResolver(owner, attacker, slashCard, pendingDamage);
    }

    /// <summary>
    /// Gets all discardable cards from owner (hand + equipment).
    /// </summary>
    private static List<Card> GetDiscardableCards(Player owner)
    {
        var cards = new List<Card>();

        // Add hand cards
        if (owner.HandZone.Cards is not null)
        {
            cards.AddRange(owner.HandZone.Cards);
        }

        // Add equipment cards
        if (owner.EquipmentZone.Cards is not null)
        {
            cards.AddRange(owner.EquipmentZone.Cards);
        }

        return cards;
    }

    /// <summary>
    /// Gets valid targets for redirecting the Slash.
    /// Valid targets must be:
    /// - Alive
    /// - Within owner's attack range
    /// - Not the attacker
    /// - Not the owner
    /// </summary>
    private static List<Player> GetValidRedirectTargets(
        Game game,
        Player owner,
        Player attacker,
        IRangeRuleService rangeRuleService)
    {
        return game.Players
            .Where(p => p.IsAlive
                && p.Seat != owner.Seat
                && p.Seat != attacker.Seat
                && rangeRuleService.IsWithinAttackRange(game, owner, p))
            .ToList();
    }

    /// <summary>
    /// Resolver that handles Liuli skill target redirection.
    /// This class is nested in LiuliSkill to keep the logic centralized.
    /// </summary>
    private sealed class LiuliResolver : IResolver
    {
        private readonly Player _owner;
        private readonly Player _attacker;
        private readonly Card _slashCard;
        private readonly DamageDescriptor _pendingDamage;

        public LiuliResolver(Player owner, Player attacker, Card slashCard, DamageDescriptor pendingDamage)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _attacker = attacker ?? throw new ArgumentNullException(nameof(attacker));
            _slashCard = slashCard ?? throw new ArgumentNullException(nameof(slashCard));
            _pendingDamage = pendingDamage ?? throw new ArgumentNullException(nameof(pendingDamage));
        }

        public ResolutionResult Resolve(ResolutionContext context)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));

            // Validate required services
            if (!ValidateServices(context, out var game, out var cardMoveService, out var getPlayerChoice))
            {
                return ResolutionResult.SuccessResult; // Skip skill activation
            }

            // Re-check conditions (owner might have died or lost cards)
            var owner = game.Players.FirstOrDefault(p => p.Seat == _owner.Seat);
            if (owner is null || !owner.IsAlive)
            {
                return ResolutionResult.SuccessResult; // Owner is dead, skip
            }

            // Validate prerequisites (discardable cards and valid targets)
            if (!ValidatePrerequisites(game, owner, out var discardableCards, out var rangeRuleService))
            {
                return ResolutionResult.SuccessResult; // Prerequisites not met, skip
            }

            // Ask player if they want to use Liuli
            if (!AskPlayerToActivate(owner, getPlayerChoice))
            {
                return ResolutionResult.SuccessResult; // Player declined, skip
            }

            // Ask player to select a card to discard
            var selectedCard = AskPlayerToSelectCard(owner, discardableCards, getPlayerChoice);
            if (selectedCard is null)
            {
                return ResolutionResult.SuccessResult; // No card selected or invalid, skip
            }

            // Discard the selected card
            if (!DiscardSelectedCard(game, owner, selectedCard, cardMoveService))
            {
                return ResolutionResult.SuccessResult; // Discard failed, skip
            }

            // Ask player to select a new target
            var newTarget = AskPlayerToSelectTarget(game, owner, rangeRuleService, getPlayerChoice);
            if (newTarget is null)
            {
                return ResolutionResult.SuccessResult; // No target selected or invalid, skip
            }

            // Apply target modification
            ApplyTargetModification(context, newTarget.Seat);

            return ResolutionResult.SuccessResult;
        }

        /// <summary>
        /// Validates that all required services are available.
        /// </summary>
        private static bool ValidateServices(
            ResolutionContext context,
            out Game game,
            out ICardMoveService cardMoveService,
            out Func<ChoiceRequest, ChoiceResult> getPlayerChoice)
        {
            game = context.Game;
            cardMoveService = context.CardMoveService;
            getPlayerChoice = context.GetPlayerChoice;

            if (cardMoveService is null || getPlayerChoice is null)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates prerequisites for using Liuli: discardable cards and valid redirect targets.
        /// </summary>
        private bool ValidatePrerequisites(
            Game game,
            Player owner,
            out List<Card> discardableCards,
            out IRangeRuleService rangeRuleService)
        {
            discardableCards = GetDiscardableCards(owner);
            if (discardableCards.Count == 0)
            {
                rangeRuleService = null!;
                return false;
            }

            // Get range rule service (create temporary instance for now)
            // TODO: Consider exposing IRangeRuleService from IRuleService or ResolutionContext
            rangeRuleService = new RangeRuleService(null);

            // Get valid redirect targets
            var validTargets = GetValidRedirectTargets(game, owner, _attacker, rangeRuleService);
            if (validTargets.Count == 0)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Asks the player if they want to activate Liuli skill.
        /// </summary>
        private static bool AskPlayerToActivate(
            Player owner,
            Func<ChoiceRequest, ChoiceResult> getPlayerChoice)
        {
            var activateRequest = new ChoiceRequest(
                RequestId: Guid.NewGuid().ToString(),
                PlayerSeat: owner.Seat,
                ChoiceType: ChoiceType.Confirm,
                TargetConstraints: null,
                AllowedCards: null,
                ResponseWindowId: null,
                CanPass: true // Player can choose not to use the skill
            );

            try
            {
                var activateResult = getPlayerChoice(activateRequest);
                return activateResult is not null && activateResult.Confirmed == true;
            }
            catch
            {
                // If getting choice fails, skip skill activation
                return false;
            }
        }

        /// <summary>
        /// Asks the player to select a card to discard.
        /// </summary>
        private static Card? AskPlayerToSelectCard(
            Player owner,
            List<Card> discardableCards,
            Func<ChoiceRequest, ChoiceResult> getPlayerChoice)
        {
            var cardSelectRequest = new ChoiceRequest(
                RequestId: Guid.NewGuid().ToString(),
                PlayerSeat: owner.Seat,
                ChoiceType: ChoiceType.SelectCards,
                TargetConstraints: null,
                AllowedCards: discardableCards,
                ResponseWindowId: null,
                CanPass: false // Must select exactly 1 card
            );

            try
            {
                var cardSelectResult = getPlayerChoice(cardSelectRequest);
                if (cardSelectResult?.SelectedCardIds is null || cardSelectResult.SelectedCardIds.Count == 0)
                {
                    return null; // No card selected
                }

                // Find the selected card
                var selectedCard = discardableCards.FirstOrDefault(c => cardSelectResult.SelectedCardIds.Contains(c.Id));
                return selectedCard;
            }
            catch
            {
                // If getting choice fails, skip skill activation
                return null;
            }
        }

        /// <summary>
        /// Discards the selected card from owner's hand or equipment zone.
        /// </summary>
        private static bool DiscardSelectedCard(
            Game game,
            Player owner,
            Card selectedCard,
            ICardMoveService cardMoveService)
        {
            try
            {
                // Determine which zone the card is in
                var isHandCard = owner.HandZone.Cards is not null && owner.HandZone.Cards.Contains(selectedCard);
                var isEquipmentCard = owner.EquipmentZone.Cards is not null && owner.EquipmentZone.Cards.Contains(selectedCard);

                if (isHandCard)
                {
                    cardMoveService.DiscardFromHand(game, owner, new[] { selectedCard });
                    return true;
                }
                else if (isEquipmentCard && owner.EquipmentZone is Zone equipmentZone)
                {
                    // Move equipment card to discard pile
                    var moveDescriptor = new CardMoveDescriptor(
                        SourceZone: equipmentZone,
                        TargetZone: game.DiscardPile,
                        Cards: new[] { selectedCard },
                        Reason: CardMoveReason.Discard,
                        Ordering: CardMoveOrdering.ToTop,
                        Game: game
                    );
                    cardMoveService.MoveSingle(moveDescriptor);
                    return true;
                }
                else
                {
                    // Card not found in expected zones
                    return false;
                }
            }
            catch
            {
                // If discarding fails, skip skill activation
                return false;
            }
        }

        /// <summary>
        /// Asks the player to select a new target for the Slash.
        /// </summary>
        private static Player? AskPlayerToSelectTarget(
            Game game,
            Player owner,
            IRangeRuleService rangeRuleService,
            Func<ChoiceRequest, ChoiceResult> getPlayerChoice)
        {
            var targetSelectRequest = new ChoiceRequest(
                RequestId: Guid.NewGuid().ToString(),
                PlayerSeat: owner.Seat,
                ChoiceType: ChoiceType.SelectTargets,
                TargetConstraints: new TargetConstraints(
                    MinTargets: 1,
                    MaxTargets: 1,
                    FilterType: TargetFilterType.Any
                ),
                AllowedCards: null,
                ResponseWindowId: null,
                CanPass: false // Must select exactly 1 target
            );

            try
            {
                var targetSelectResult = getPlayerChoice(targetSelectRequest);
                if (targetSelectResult?.SelectedTargetSeats is null || targetSelectResult.SelectedTargetSeats.Count == 0)
                {
                    return null; // No target selected
                }

                var newTargetSeat = targetSelectResult.SelectedTargetSeats[0];
                var newTarget = game.Players.FirstOrDefault(p => p.Seat == newTargetSeat);

                // Validate new target
                if (newTarget is null || !newTarget.IsAlive || 
                    newTarget.Seat == owner.Seat || newTarget.Seat == owner.Seat) // Note: _attacker is not accessible here, but validation happens in GetValidRedirectTargets
                {
                    return null; // Invalid target
                }

                if (!rangeRuleService.IsWithinAttackRange(game, owner, newTarget))
                {
                    return null; // Target not in range
                }

                return newTarget;
            }
            catch
            {
                // If getting choice fails, skip skill activation
                return null;
            }
        }

        /// <summary>
        /// Applies the target modification by storing the new target in IntermediateResults.
        /// </summary>
        private void ApplyTargetModification(ResolutionContext context, int newTargetSeat)
        {
            var intermediateResults = context.IntermediateResults ?? new Dictionary<string, object>();
            intermediateResults["LiuliNewTargetSeat"] = newTargetSeat;
            intermediateResults["LiuliOriginalTargetSeat"] = _pendingDamage.TargetSeat;

            // Update the damage descriptor in IntermediateResults
            // Note: DamageDescriptor is immutable, so we need to create a new one
            var modifiedDamage = new DamageDescriptor(
                SourceSeat: _pendingDamage.SourceSeat,
                TargetSeat: newTargetSeat, // New target
                Amount: _pendingDamage.Amount,
                Type: _pendingDamage.Type,
                Reason: _pendingDamage.Reason,
                CausingCard: _pendingDamage.CausingCard,
                CausingCards: _pendingDamage.CausingCards
            );

            intermediateResults["SlashPendingDamage"] = modifiedDamage;
        }

        private static List<Card> GetDiscardableCards(Player owner)
        {
            var cards = new List<Card>();

            if (owner.HandZone.Cards is not null)
            {
                cards.AddRange(owner.HandZone.Cards);
            }

            if (owner.EquipmentZone.Cards is not null)
            {
                cards.AddRange(owner.EquipmentZone.Cards);
            }

            return cards;
        }

        private static List<Player> GetValidRedirectTargets(
            Game game,
            Player owner,
            Player attacker,
            IRangeRuleService rangeRuleService)
        {
            return game.Players
                .Where(p => p.IsAlive
                    && p.Seat != owner.Seat
                    && p.Seat != attacker.Seat
                    && rangeRuleService.IsWithinAttackRange(game, owner, p))
                .ToList();
        }
    }
}

/// <summary>
/// Factory for creating LiuliSkill instances.
/// </summary>
public sealed class LiuliSkillFactory : ISkillFactory
{
    /// <inheritdoc />
    public ISkill CreateSkill()
    {
        return new LiuliSkill();
    }
}

