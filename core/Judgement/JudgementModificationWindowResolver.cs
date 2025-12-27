using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Judgement;

/// <summary>
/// Resolver that handles the judgement modification window.
/// Allows skills with IJudgementModifier interface to modify the judgement card
/// after it is revealed but before the result is calculated.
/// </summary>
public sealed class JudgementModificationWindowResolver : IResolver
{
    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        // Extract JudgementContext from IntermediateResults
        if (context.IntermediateResults is null || !context.IntermediateResults.TryGetValue("JudgementContext", out var ctxObj))
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.judgement.modificationWindow.noContext",
                details: new { Message = "JudgementContext not found in IntermediateResults" });
        }

        if (ctxObj is not JudgementContext judgementContext)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.judgement.modificationWindow.invalidContext",
                details: new { Message = "JudgementContext has invalid type" });
        }

        // Check if modification is allowed
        if (!judgementContext.Request.AllowModify)
        {
            // Modification not allowed, skip modification window
            return ResolutionResult.SuccessResult;
        }

        // Get skill manager to find all skills with IJudgementModifier interface
        var skillManager = context.SkillManager;
        if (skillManager is null)
        {
            // No skill manager, skip modification
            return ResolutionResult.SuccessResult;
        }

        // Find all players with judgement modification skills
        var modifiers = FindJudgementModifiers(context.Game, skillManager, judgementContext);
        if (modifiers.Count == 0)
        {
            // No modifiers available, skip modification window
            return ResolutionResult.SuccessResult;
        }

        // Process modifications in seat order (from judge owner, clockwise)
        var judgeOwner = judgementContext.JudgeTarget;
        var playersInOrder = GetPlayersInSeatOrder(context.Game, judgeOwner);
        
        foreach (var player in playersInOrder)
        {
            if (!modifiers.TryGetValue(player.Seat, out var modifier))
                continue;

            // Check if this modifier can modify the judgement
            if (!modifier.CanModify(judgementContext, player))
                continue;

            // Get player's decision to modify
            var decision = modifier.GetDecision(judgementContext, player, context.GetPlayerChoice);
            if (decision is null)
                continue;

            // Apply the modification
            ApplyModification(context, judgementContext, player, decision);
        }

        return ResolutionResult.SuccessResult;
    }

    /// <summary>
    /// Finds all players with judgement modification skills.
    /// </summary>
    private static Dictionary<int, IJudgementModifier> FindJudgementModifiers(
        Game game,
        SkillManager skillManager,
        JudgementContext ctx)
    {
        var modifiers = new Dictionary<int, IJudgementModifier>();

        foreach (var player in game.Players)
        {
            if (!player.IsAlive)
                continue;

            // Get all skills for this player
            var skills = skillManager.GetAllSkills(player);
            foreach (var skill in skills)
            {
                if (skill is IJudgementModifier modifier)
                {
                    modifiers[player.Seat] = modifier;
                    break; // Only one modifier per player for now
                }
            }
        }

        return modifiers;
    }

    /// <summary>
    /// Gets players in seat order starting from the judge owner (clockwise).
    /// </summary>
    private static List<Player> GetPlayersInSeatOrder(Game game, Player startPlayer)
    {
        var players = new List<Player>();
        var currentSeat = startPlayer.Seat;
        var maxSeat = game.Players.Max(p => p.Seat);

        // Start from judge owner and go clockwise
        for (int i = 0; i < game.Players.Count; i++)
        {
            var player = game.Players.FirstOrDefault(p => p.Seat == currentSeat);
            if (player is not null && player.IsAlive)
            {
                players.Add(player);
            }

            // Move to next seat (clockwise)
            currentSeat++;
            if (currentSeat > maxSeat)
            {
                currentSeat = game.Players.Min(p => p.Seat);
            }
        }

        return players;
    }

    /// <summary>
    /// Applies a modification to the judgement.
    /// </summary>
    private static void ApplyModification(
        ResolutionContext context,
        JudgementContext judgementContext,
        Player modifier,
        JudgementModifyDecision decision)
    {
        var game = context.Game;
        var judgeOwner = judgementContext.JudgeTarget;
        var oldCard = judgementContext.CurrentJudgementCard;
        var newCard = decision.ReplacementCard;

        // Verify the replacement card is valid
        if (newCard is null)
            return;

        // Verify the replacement card is in the modifier's hand
        if (!modifier.HandZone.Cards.Contains(newCard))
        {
            // Card not in hand, skip modification
            return;
        }

        // Move old card from JudgementZone to discard pile
        try
        {
            var oldCardMoveDescriptor = new CardMoveDescriptor(
                SourceZone: judgeOwner.JudgementZone,
                TargetZone: game.DiscardPile,
                Cards: new[] { oldCard },
                Reason: CardMoveReason.Discard,
                Ordering: CardMoveOrdering.ToTop,
                Game: game);
            context.CardMoveService.MoveSingle(oldCardMoveDescriptor);
        }
        catch
        {
            // If moving fails, skip modification
            return;
        }

        // Move new card from modifier's hand to JudgementZone
        try
        {
            var newCardMoveDescriptor = new CardMoveDescriptor(
                SourceZone: modifier.HandZone,
                TargetZone: judgeOwner.JudgementZone,
                Cards: new[] { newCard },
                Reason: CardMoveReason.Judgement,
                Ordering: CardMoveOrdering.ToTop,
                Game: game);
            context.CardMoveService.MoveSingle(newCardMoveDescriptor);

            // Update current judgement card in context
            var movedCard = judgeOwner.JudgementZone.Cards.FirstOrDefault(c => c.Id == newCard.Id);
            if (movedCard is not null)
            {
                judgementContext.CurrentJudgementCard = movedCard;
            }

            // Record the modification
            var modificationRecord = new JudgementModificationRecord(
                ModifierSeat: modifier.Seat,
                ModifierSource: decision.ModifierSource,
                OriginalCard: oldCard,
                ModifiedCard: movedCard ?? newCard,
                Timestamp: DateTime.UtcNow);
            judgementContext.Modifications.Add(modificationRecord);

            // Publish event for modification (optional, for logging/UI)
            if (context.EventBus is not null)
            {
                // Could publish a JudgementModifiedEvent here if needed
            }
        }
        catch
        {
            // If moving fails, we've already moved the old card, so we need to handle this
            // For now, we'll just skip - in production, we might want to rollback
        }
    }
}

