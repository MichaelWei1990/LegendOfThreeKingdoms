using System;
using System.Linq;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Resolver for equipment card usage.
/// Handles equipping and unequipping equipment cards.
/// </summary>
public sealed class EquipResolver : IResolver
{
    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        // Validate input and extract card
        var validationResult = ValidateAndExtractCard(context, out var card);
        if (validationResult is not null)
            return validationResult;

        // At this point, card is guaranteed to be non-null (ValidateAndExtractCard ensures this)
        if (card is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.equip.cardExtractionFailed");
        }

        // Unequip existing equipment of the same subtype if any
        var unequipResult = UnequipExistingEquipment(context, card);
        if (unequipResult is not null)
            return unequipResult;

        // Equip the new card
        var equipResult = EquipNewCard(context, card);
        if (equipResult is not null)
            return equipResult;

        // Load equipment skill if applicable
        LoadEquipmentSkill(context, card);

        return ResolutionResult.SuccessResult;
    }

    /// <summary>
    /// Validates the input context and extracts the equipment card to be equipped.
    /// </summary>
    /// <param name="context">The resolution context containing the game state and player choice.</param>
    /// <param name="card">When the method returns successfully, contains the card to be equipped; otherwise, null.</param>
    /// <returns>
    /// A failure result if validation fails (null choice, no card selected, card not found, or card is not equipment type);
    /// null if validation succeeds and the card is extracted.
    /// </returns>
    private static ResolutionResult? ValidateAndExtractCard(ResolutionContext context, out Card? card)
    {
        card = null;

        if (context.Choice is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.equip.noChoice");
        }

        var selectedCardIds = context.Choice.SelectedCardIds;
        if (selectedCardIds is null || selectedCardIds.Count == 0)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.CardNotFound,
                messageKey: "resolution.equip.noCardSelected");
        }

        var cardId = selectedCardIds[0];
        card = context.SourcePlayer.HandZone.Cards.FirstOrDefault(c => c.Id == cardId);
        if (card is null)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.CardNotFound,
                messageKey: "resolution.equip.cardNotFound",
                details: new { CardId = cardId });
        }

        if (card.CardType != CardType.Equip)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.equip.notEquipment",
                details: new { CardType = card.CardType });
        }

        return null;
    }

    /// <summary>
    /// Unequips any existing equipment of the same subtype as the new card being equipped.
    /// If a player already has an equipment of the same subtype (e.g., already has a defensive horse),
    /// it must be moved to the discard pile before the new equipment can be equipped.
    /// </summary>
    /// <param name="context">The resolution context containing the game state and services.</param>
    /// <param name="newCard">The new equipment card being equipped, used to find existing equipment of the same subtype.</param>
    /// <returns>
    /// A failure result if unequipping fails (e.g., card move service throws an exception);
    /// null if there is no existing equipment to unequip, or if unequipping succeeds.
    /// </returns>
    private static ResolutionResult? UnequipExistingEquipment(ResolutionContext context, Card newCard)
    {
        var game = context.Game;
        var sourcePlayer = context.SourcePlayer;

        if (sourcePlayer.EquipmentZone is not Zone equipmentZone)
            return null;

        var existingEquipment = equipmentZone.Cards.FirstOrDefault(e => e.CardSubType == newCard.CardSubType);
        if (existingEquipment is null)
            return null;

        try
        {
            // Move existing equipment to discard pile
            var unequipDescriptor = new CardMoveDescriptor(
                SourceZone: equipmentZone,
                TargetZone: game.DiscardPile,
                Cards: new[] { existingEquipment },
                Reason: CardMoveReason.Discard,
                Ordering: CardMoveOrdering.ToTop,
                Game: game
            );
            context.CardMoveService.MoveMany(unequipDescriptor);

            // Remove equipment skill if applicable
            RemoveEquipmentSkill(context, existingEquipment);
        }
        catch (Exception ex)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.equip.unequipFailed",
                details: new { Exception = ex.Message });
        }

        return null;
    }

    /// <summary>
    /// Moves the equipment card from the player's hand zone to their equipment zone.
    /// This physically equips the card, making it available for use.
    /// </summary>
    /// <param name="context">The resolution context containing the game state and services.</param>
    /// <param name="card">The equipment card to be equipped, which should already be validated and extracted from the hand.</param>
    /// <returns>
    /// A failure result if equipping fails (invalid zones or card move service throws an exception);
    /// null if equipping succeeds.
    /// </returns>
    private static ResolutionResult? EquipNewCard(ResolutionContext context, Card card)
    {
        var game = context.Game;
        var sourcePlayer = context.SourcePlayer;

        if (sourcePlayer.HandZone is not Zone handZone)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.equip.invalidHandZone");
        }

        if (sourcePlayer.EquipmentZone is not Zone targetEquipmentZone)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.equip.invalidEquipmentZone");
        }

        try
        {
            var equipDescriptor = new CardMoveDescriptor(
                SourceZone: handZone,
                TargetZone: targetEquipmentZone,
                Cards: new[] { card },
                Reason: CardMoveReason.Equip,
                Ordering: CardMoveOrdering.ToTop,
                Game: game
            );
            context.CardMoveService.MoveMany(equipDescriptor);
        }
        catch (Exception ex)
        {
            return ResolutionResult.Failure(
                ResolutionErrorCode.InvalidState,
                messageKey: "resolution.equip.equipFailed",
                details: new { Exception = ex.Message });
        }

        return null;
    }

    private static void LoadEquipmentSkill(ResolutionContext context, Card card)
    {
        if (context.SkillManager is null || context.EquipmentSkillRegistry is null)
            return;

        // Priority 1: Try to find skill by DefinitionId (supports special equipment with unique skills)
        var equipmentSkill = context.EquipmentSkillRegistry.GetSkillForEquipment(card.DefinitionId);
        
        // Priority 2: If not found, try to find skill by CardSubType (supports category-based shared skills)
        if (equipmentSkill is null)
        {
            equipmentSkill = context.EquipmentSkillRegistry.GetSkillForEquipmentBySubType(card.CardSubType);
        }
        
        if (equipmentSkill is not null)
        {
            context.SkillManager.AddEquipmentSkill(context.Game, context.SourcePlayer, equipmentSkill);
        }
    }

    private static void RemoveEquipmentSkill(ResolutionContext context, Card equipment)
    {
        if (context.SkillManager is null || context.EquipmentSkillRegistry is null)
            return;

        // Priority 1: Try to find skill by DefinitionId
        var oldSkill = context.EquipmentSkillRegistry.GetSkillForEquipment(equipment.DefinitionId);
        
        // Priority 2: If not found, try to find skill by CardSubType
        if (oldSkill is null)
        {
            oldSkill = context.EquipmentSkillRegistry.GetSkillForEquipmentBySubType(equipment.CardSubType);
        }
        
        if (oldSkill is not null)
        {
            context.SkillManager.RemoveEquipmentSkill(context.Game, context.SourcePlayer, oldSkill.Id);
        }
    }
}
