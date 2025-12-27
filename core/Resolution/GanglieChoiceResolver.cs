using System;
using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Resolver for Ganglie (刚烈) skill choice: discard 2 hand cards or take 1 damage.
/// </summary>
public sealed class GanglieChoiceResolver : IResolver
{
    private readonly int _ganglieOwnerSeat;
    private readonly int _damageSourceSeat;

    /// <summary>
    /// Creates a new GanglieChoiceResolver.
    /// </summary>
    /// <param name="ganglieOwnerSeat">The seat of the player who owns Ganglie skill.</param>
    /// <param name="damageSourceSeat">The seat of the damage source who needs to choose.</param>
    public GanglieChoiceResolver(int ganglieOwnerSeat, int damageSourceSeat)
    {
        _ganglieOwnerSeat = ganglieOwnerSeat;
        _damageSourceSeat = damageSourceSeat;
    }

    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var game = context.Game;
        var choice = context.Choice;

        // Find players
        var ganglieOwner = game.Players.FirstOrDefault(p => p.Seat == _ganglieOwnerSeat);
        var damageSource = game.Players.FirstOrDefault(p => p.Seat == _damageSourceSeat);

        if (ganglieOwner is null || damageSource is null || !damageSource.IsAlive)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidTarget,
                messageKey: "Ganglie: Invalid players");
        }

        // Check if damage source has less than 2 hand cards
        // If so, automatically choose "damage" option (no choice needed)
        if (damageSource.HandZone.Cards.Count < 2)
        {
            return ExecuteDamageOption(context, game, ganglieOwner, damageSource);
        }

        // Get choice result
        // If choice is null, default to damage option
        if (choice is null)
        {
            return ExecuteDamageOption(context, game, ganglieOwner, damageSource);
        }

        // Determine which option was selected based on choice result
        // If player selected 2 cards, they chose "discard" option
        // If player passed or selected less than 2 cards, they chose "damage" option
        
        // Check if player selected 2 cards (discard option)
        if (choice.SelectedCardIds is not null && choice.SelectedCardIds.Count == 2)
        {
            return ExecuteDiscardOption(context, game, damageSource, ganglieOwner);
        }
        else
        {
            // Player passed or didn't select 2 cards, choose damage option
            return ExecuteDamageOption(context, game, ganglieOwner, damageSource);
        }
    }

    /// <summary>
    /// Executes the "discard 2 hand cards" option.
    /// </summary>
    private ResolutionResult ExecuteDiscardOption(
        ResolutionContext context,
        Game game,
        Player damageSource,
        Player ganglieOwner)
    {
        if (context.CardMoveService is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "Ganglie: CardMoveService is required");
        }

        // Check if damage source has at least 2 hand cards
        if (damageSource.HandZone.Cards.Count < 2)
        {
            // Cannot discard, automatically take damage instead
            return ExecuteDamageOption(context, game, ganglieOwner, damageSource);
        }

        // Get choice result to see which cards were selected
        var choice = context.Choice;
        if (choice?.SelectedCardIds is not null && choice.SelectedCardIds.Count == 2)
        {
            // Player selected 2 cards to discard
            var cardsToDiscard = damageSource.HandZone.Cards
                .Where(c => choice.SelectedCardIds.Contains(c.Id))
                .ToList();

            if (cardsToDiscard.Count == 2)
            {
                try
                {
                    context.CardMoveService.DiscardFromHand(game, damageSource, cardsToDiscard);
                    return ResolutionResult.SuccessResult;
                }
                catch (Exception ex)
                {
                    return ResolutionResult.Failure(
                        ResolutionErrorCode.InvalidState,
                        messageKey: $"Ganglie: Failed to discard cards: {ex.Message}");
                }
            }
        }

        // If no cards selected or invalid selection, discard first 2 cards
        var cardsToDiscardDefault = damageSource.HandZone.Cards.Take(2).ToList();
        try
        {
            context.CardMoveService.DiscardFromHand(game, damageSource, cardsToDiscardDefault);
            return ResolutionResult.SuccessResult;
        }
        catch (Exception ex)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: $"Ganglie: Failed to discard cards: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes the "take 1 damage" option.
    /// </summary>
    private ResolutionResult ExecuteDamageOption(
        ResolutionContext context,
        Game game,
        Player ganglieOwner,
        Player damageSource)
    {
        // Create damage descriptor for 1 damage from ganglieOwner to damageSource
        var damage = new DamageDescriptor(
            SourceSeat: ganglieOwner.Seat,
            TargetSeat: damageSource.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Ganglie",
            CausingCard: null, // Skill damage, no causing card
            IsPreventable: true,
            TransferredToSeat: null,
            TriggersDying: true
        );

        // Create context for DamageResolver
        var damageContext = new ResolutionContext(
            game,
            ganglieOwner,
            null,
            null,
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
            JudgementService: context.JudgementService
        );

        // Push DamageResolver to apply the damage
        context.Stack.Push(new DamageResolver(), damageContext);

        return ResolutionResult.SuccessResult;
    }
}
