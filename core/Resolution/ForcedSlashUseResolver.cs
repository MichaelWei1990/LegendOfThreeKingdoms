using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Resolver for forced slash use in Jie Dao Sha Ren effect.
/// Handles the decision of whether player A uses Slash on B or transfers weapon.
/// </summary>
internal sealed class ForcedSlashUseResolver : IResolver
{
    private readonly Player _actor; // Player A who must use slash
    private readonly Player _target; // Player B who is the slash target
    private readonly Player _requester; // The user of Jie Dao Sha Ren

    public ForcedSlashUseResolver(Player actor, Player target, Player requester)
    {
        _actor = actor ?? throw new ArgumentNullException(nameof(actor));
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _requester = requester ?? throw new ArgumentNullException(nameof(requester));
    }

    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var game = context.Game;

        // Re-validate that actor and target are still alive
        if (!_actor.IsAlive)
        {
            // Actor died, transfer weapon if available
            return TransferWeapon(context, game);
        }

        if (!_target.IsAlive)
        {
            // Target died, transfer weapon if available
            return TransferWeapon(context, game);
        }

        // Re-validate slash legality (third check, just before use)
        if (!IsSlashLegal(context, _actor, _target))
        {
            // No longer legal, transfer weapon
            return TransferWeapon(context, game);
        }

        // Publish ForcedSlashUseRequestedEvent
        if (context.EventBus is not null)
        {
            var requestedEvent = new Events.ForcedSlashUseRequestedEvent(
                context.Game,
                _actor.Seat,
                _target.Seat,
                _requester.Seat);
            context.EventBus.Publish(requestedEvent);
        }

        // Ask actor if they want to use Slash on target
        var decisionResult = RequestSlashUseDecision(context);
        if (decisionResult.FailureResult is not null)
        {
            return decisionResult.FailureResult;
        }

        if (!decisionResult.WillUseSlash)
        {
            // Actor chose not to use slash, transfer weapon
            if (context.EventBus is not null)
            {
                var resolvedEvent = new Events.ForcedSlashUseResolvedEvent(
                    context.Game,
                    _actor.Seat,
                    _target.Seat,
                    _requester.Seat,
                    SlashUsed: false);
                context.EventBus.Publish(resolvedEvent);
            }
            return TransferWeapon(context, game);
        }

        // Actor chose to use slash, push SlashResolver
        if (context.EventBus is not null)
        {
            var resolvedEvent = new Events.ForcedSlashUseResolvedEvent(
                context.Game,
                _actor.Seat,
                _target.Seat,
                _requester.Seat,
                SlashUsed: true);
            context.EventBus.Publish(resolvedEvent);
        }
        return PushSlashResolver(context, game);
    }

    /// <summary>
    /// Requests the actor to decide whether to use Slash on the target.
    /// </summary>
    private DecisionResult RequestSlashUseDecision(ResolutionContext context)
    {
        if (context.GetPlayerChoice is null)
        {
            // No player choice available, assume actor cannot/will not use slash
            return DecisionResult.DoNotUseSlash();
        }

        // Check if actor has any way to use slash (hand cards or skills)
        var canUseSlash = CanActorUseSlash(context);
        if (!canUseSlash)
        {
            // Actor cannot use slash, transfer weapon
            return DecisionResult.DoNotUseSlash();
        }

        // Find available slash cards (hand or equipment)
        var availableSlashCards = new List<Card>();
        if (_actor.HandZone.Cards is not null)
        {
            availableSlashCards.AddRange(_actor.HandZone.Cards.Where(c => c.CardSubType == CardSubType.Slash));
        }
        if (_actor.EquipmentZone.Cards is not null)
        {
            availableSlashCards.AddRange(_actor.EquipmentZone.Cards.Where(c => c.CardSubType == CardSubType.Slash));
        }

        // If no slash cards but skills can provide, proceed with slash use
        if (availableSlashCards.Count == 0)
        {
            if (HasSlashProvidingSkills(context))
            {
                // Skills can provide slash, proceed with slash use
                return DecisionResult.UseSlash();
            }
            else
            {
                // No way to use slash, transfer weapon
                return DecisionResult.DoNotUseSlash();
            }
        }

        // Actor has slash cards, ask if they want to use one
        var choiceRequest = new ChoiceRequest(
            RequestId: Guid.NewGuid().ToString("N"),
            PlayerSeat: _actor.Seat,
            ChoiceType: ChoiceType.SelectCards,
            TargetConstraints: null,
            AllowedCards: availableSlashCards,
            ResponseWindowId: null,
            CanPass: true); // Actor can choose not to use slash

        var playerChoice = context.GetPlayerChoice(choiceRequest);

        if (playerChoice is null)
        {
            // No choice returned, assume actor refuses
            return DecisionResult.DoNotUseSlash();
        }

        var selectedCardIds = playerChoice.SelectedCardIds;
        if (selectedCardIds is null || selectedCardIds.Count == 0)
        {
            // Actor chose not to use slash (passed)
            return DecisionResult.DoNotUseSlash();
        }

        // Store selected slash card in intermediate results for PushSlashResolver to use
        var intermediateResults = context.IntermediateResults ?? new Dictionary<string, object>();
        var selectedSlashCard = availableSlashCards.FirstOrDefault(c => selectedCardIds.Contains(c.Id));
        if (selectedSlashCard is not null)
        {
            intermediateResults["JieDaoShaRenSelectedSlashCard"] = selectedSlashCard;
        }

        // Actor chose to use slash
        return DecisionResult.UseSlash();
    }

    /// <summary>
    /// Checks if the actor can use slash (has slash card or skills that provide slash).
    /// </summary>
    private bool CanActorUseSlash(ResolutionContext context)
    {
        // Check for slash card in hand
        var slashCard = FindSlashCard(context);
        if (slashCard is not null)
        {
            return true;
        }

        // Check for skills that can provide slash
        return HasSlashProvidingSkills(context);
    }

    /// <summary>
    /// Finds a Slash card in the actor's hand or equipment zone.
    /// </summary>
    private Card? FindSlashCard(ResolutionContext context)
    {
        if (_actor.HandZone.Cards is not null)
        {
            var handSlash = _actor.HandZone.Cards.FirstOrDefault(c => c.CardSubType == CardSubType.Slash);
            if (handSlash is not null)
            {
                return handSlash;
            }
        }

        if (_actor.EquipmentZone.Cards is not null)
        {
            var equipSlash = _actor.EquipmentZone.Cards.FirstOrDefault(c => c.CardSubType == CardSubType.Slash);
            if (equipSlash is not null)
            {
                return equipSlash;
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if the actor has skills that can provide slash (e.g., Wusheng, Jijiang).
    /// </summary>
    private bool HasSlashProvidingSkills(ResolutionContext context)
    {
        if (context.SkillManager is null)
        {
            return false;
        }

        // Check for skills that can provide slash for use
        // This is a simplified check - in reality, we'd need to check each skill's capabilities
        var skills = context.SkillManager.GetActiveSkills(context.Game, _actor);
        
        // Check for Wusheng (can use red cards as Slash)
        var hasWusheng = skills.Any(s => s.Id == "wusheng");
        if (hasWusheng)
        {
            // Check if actor has red cards
            var hasRedCards = _actor.HandZone.Cards?.Any(c => 
                c.Suit == Suit.Heart || c.Suit == Suit.Diamond) == true;
            if (hasRedCards)
            {
                return true;
            }
        }

        // Check for Jijiang (can request other players to provide Slash)
        var hasJijiang = skills.Any(s => s.Id == "jijiang");
        if (hasJijiang)
        {
            // Jijiang can potentially provide slash through assistance
            // For now, we'll assume it can (the actual check would be more complex)
            return true;
        }

        return false;
    }

    /// <summary>
    /// Pushes SlashResolver to execute the slash use.
    /// </summary>
    private ResolutionResult PushSlashResolver(ResolutionContext context, Game game)
    {
        // Get selected slash card from intermediate results (set by RequestSlashUseDecision)
        Card? slashCard = null;
        var intermediateResults = context.IntermediateResults ?? new Dictionary<string, object>();
        if (intermediateResults.TryGetValue("JieDaoShaRenSelectedSlashCard", out var selectedCardObj) &&
            selectedCardObj is Card selectedCard)
        {
            slashCard = selectedCard;
        }
        
        // If no selected card, try to find one (fallback for skill-based slash)
        if (slashCard is null)
        {
            slashCard = FindSlashCard(context);
        }
        
        // If no slash card found, we'll let SlashResolver handle skill-based slash (e.g., Wusheng, Jijiang)
        // by not providing a card in the choice

        // Create action descriptor for using slash
        var action = new ActionDescriptor(
            ActionId: Guid.NewGuid().ToString("N"),
            DisplayKey: "UseSlash",
            RequiresTargets: true,
            TargetConstraints: new TargetConstraints(
                MinTargets: 1,
                MaxTargets: 1,
                FilterType: TargetFilterType.Any),
            CardCandidates: slashCard is not null ? new[] { slashCard } : null);

        // Create choice result
        var choice = new ChoiceResult(
            RequestId: Guid.NewGuid().ToString("N"),
            PlayerSeat: _actor.Seat,
            SelectedTargetSeats: new[] { _target.Seat },
            SelectedCardIds: slashCard is not null ? new[] { slashCard.Id } : null,
            SelectedOptionId: null,
            Confirmed: null);

        // Mark this as a forced slash from Jie Dao Sha Ren in intermediate results
        intermediateResults["ForcedSlashFromJieDaoShaRen"] = true;
        intermediateResults["JieDaoShaRenRequester"] = _requester.Seat;

        // Push UseCardResolver first to handle card movement to discard pile
        // UseCardResolver will automatically push SlashResolver after moving the card
        var useCardContext = new ResolutionContext(
            game,
            _actor, // Source player is the actor
            action,
            choice,
            context.Stack,
            context.CardMoveService,
            context.RuleService,
            context.PendingDamage,
            context.LogSink,
            context.GetPlayerChoice,
            intermediateResults,
            context.EventBus,
            context.LogCollector,
            context.SkillManager,
            context.EquipmentSkillRegistry,
            context.JudgementService);

        var useCardResolver = new UseCardResolver();
        context.Stack.Push(useCardResolver, useCardContext);

        // Note: UseCardResolver will push SlashResolver automatically based on card type, so we don't need to push it here

        // Log if available
        if (context.LogSink is not null)
        {
            context.LogSink.Log(new LogEntry
            {
                EventType = "JieDaoShaRenForcedSlash",
                Level = "Info",
                Message = $"Player {_actor.Seat} is forced to use Slash on player {_target.Seat}",
                Data = new
                {
                    ActorSeat = _actor.Seat,
                    TargetSeat = _target.Seat,
                    RequesterSeat = _requester.Seat,
                    SlashCardId = slashCard?.Id
                }
            });
        }

        return ResolutionResult.SuccessResult;
    }

    /// <summary>
    /// Checks if a slash from actor to target is legal.
    /// </summary>
    private static bool IsSlashLegal(
        ResolutionContext context,
        Player actor,
        Player target)
    {
        if (context.RuleService is null)
        {
            return false;
        }

        if (!target.IsAlive)
        {
            return false;
        }

        var virtualSlash = new Card
        {
            Id = -1,
            DefinitionId = "virtual_slash",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = Suit.Spade,
            Rank = 1
        };

        var usageContext = new CardUsageContext(
            Game: context.Game,
            SourcePlayer: actor,
            Card: virtualSlash,
            CandidateTargets: context.Game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 0);

        var legalTargets = context.RuleService.GetLegalTargetsForUse(usageContext);
        return legalTargets.HasAny && legalTargets.Items.Contains(target);
    }

    /// <summary>
    /// Transfers weapon from actor's equipment zone to requester's hand zone.
    /// </summary>
    private ResolutionResult TransferWeapon(ResolutionContext context, Game game)
    {
        var weapon = _actor.EquipmentZone.Cards?.FirstOrDefault(c => c.CardSubType == CardSubType.Weapon);
        if (weapon is null)
        {
            // No weapon to transfer
            return ResolutionResult.SuccessResult;
        }

        try
        {
            // Remove equipment skill if applicable
            if (context.SkillManager is not null && context.EquipmentSkillRegistry is not null)
            {
                var equipmentSkill = context.EquipmentSkillRegistry.GetSkillForEquipment(weapon.DefinitionId);
                if (equipmentSkill is null)
                {
                    equipmentSkill = context.EquipmentSkillRegistry.GetSkillForEquipmentBySubType(weapon.CardSubType);
                }
                if (equipmentSkill is not null)
                {
                    context.SkillManager.RemoveEquipmentSkill(game, _actor, equipmentSkill.Id);
                }
            }

            // Move weapon to requester's hand
            if (_requester.HandZone is not Zone targetHandZone)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "resolution.jiedaosharen.invalidRequesterHandZone");
            }

            var moveDescriptor = new CardMoveDescriptor(
                SourceZone: _actor.EquipmentZone,
                TargetZone: targetHandZone,
                Cards: new[] { weapon },
                Reason: CardMoveReason.Play,
                Ordering: CardMoveOrdering.ToTop,
                Game: game);

            context.CardMoveService.MoveSingle(moveDescriptor);

            // Publish WeaponTransferredEvent
            if (context.EventBus is not null)
            {
                var weaponTransferredEvent = new Events.WeaponTransferredEvent(
                    game,
                    _actor.Seat,
                    _requester.Seat,
                    weapon.Id,
                    weapon.CardSubType,
                    "JieDaoShaRen");
                context.EventBus.Publish(weaponTransferredEvent);
            }

            // Log if available
            if (context.LogSink is not null)
            {
                context.LogSink.Log(new LogEntry
                {
                    EventType = "JieDaoShaRenWeaponTransferred",
                    Level = "Info",
                    Message = $"Player {_requester.Seat} obtained weapon {weapon.Id} from player {_actor.Seat}",
                    Data = new
                    {
                        RequesterSeat = _requester.Seat,
                        ActorSeat = _actor.Seat,
                        WeaponId = weapon.Id
                    }
                });
            }

            return ResolutionResult.SuccessResult;
        }
        catch (Exception ex)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.jiedaosharen.weaponTransferFailed",
                details: new { Exception = ex.Message });
        }
    }

    /// <summary>
    /// Result of slash use decision.
    /// </summary>
    private sealed record DecisionResult(
        bool WillUseSlash,
        ResolutionResult? FailureResult)
    {
        public static DecisionResult UseSlash() => new(true, null);
        public static DecisionResult DoNotUseSlash() => new(false, null);
        public static DecisionResult CreateFailure(ResolutionResult failureResult) => new(false, failureResult);
    }
}
