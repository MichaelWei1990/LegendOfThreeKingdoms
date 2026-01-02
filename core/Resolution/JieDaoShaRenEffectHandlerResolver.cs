using System;
using System.Linq;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Zones;

namespace LegendOfThreeKingdoms.Core.Resolution;

/// <summary>
/// Handler resolver for Jie Dao Sha Ren effect that checks nullification result
/// and applies the effect if not nullified.
/// </summary>
internal sealed class JieDaoShaRenEffectHandlerResolver : IResolver
{
    private readonly Player _targetA;
    private readonly Player _targetB;
    private readonly Player _sourcePlayer;

    public JieDaoShaRenEffectHandlerResolver(Player targetA, Player targetB, Player sourcePlayer)
    {
        _targetA = targetA ?? throw new ArgumentNullException(nameof(targetA));
        _targetB = targetB ?? throw new ArgumentNullException(nameof(targetB));
        _sourcePlayer = sourcePlayer ?? throw new ArgumentNullException(nameof(sourcePlayer));
    }

    /// <inheritdoc />
    public ResolutionResult Resolve(ResolutionContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        // Check nullification result
        var nullificationResultKey = $"JieDaoShaRenNullification_{_targetA.Seat}";
        if (context.IntermediateResults is not null &&
            context.IntermediateResults.TryGetValue(nullificationResultKey, out var resultObj) &&
            resultObj is NullificationResult nullificationResult)
        {
            if (nullificationResult.IsNullified)
            {
                // Effect was nullified, skip execution
                if (context.LogSink is not null)
                {
                    context.LogSink.Log(new LogEntry
                    {
                        EventType = "JieDaoShaRenNullified",
                        Level = "Info",
                        Message = $"Jie Dao Sha Ren effect on player {_targetA.Seat} was nullified",
                        Data = new
                        {
                            TargetASeat = _targetA.Seat,
                            TargetBSeat = _targetB.Seat,
                            NullificationCount = nullificationResult.NullificationCount
                        }
                    });
                }
                return ResolutionResult.SuccessResult;
            }
        }

        // Effect was not nullified, proceed with execution
        var game = context.Game;

        // Publish BeforeJieDaoShaRenEffectEvent
        if (context.EventBus is not null)
        {
            var beforeEffectEvent = new Events.BeforeJieDaoShaRenEffectEvent(
                game,
                _sourcePlayer.Seat,
                _targetA.Seat,
                _targetB.Seat);
            context.EventBus.Publish(beforeEffectEvent);
        }

        // Re-validate that A is still alive
        if (!_targetA.IsAlive)
        {
            // A died before resolution, skip effect
            return ResolutionResult.SuccessResult;
        }

        // Second legality check: verify that B is still a legal slash target for A
        if (!IsSlashLegal(context, _targetA, _targetB))
        {
            // B is no longer a legal target, transfer weapon
            return TransferWeapon(context, game);
        }

        // Push forced slash use resolver
        var forcedSlashContext = new ResolutionContext(
            context.Game,
            _targetA, // Source player is A (the one who must use slash)
            context.Action,
            context.Choice,
            context.Stack,
            context.CardMoveService,
            context.RuleService,
            context.PendingDamage,
            context.LogSink,
            context.GetPlayerChoice,
            context.IntermediateResults,
            context.EventBus,
            context.LogCollector,
            context.SkillManager,
            context.EquipmentSkillRegistry,
            context.JudgementService);

        var forcedSlashResolver = new ForcedSlashUseResolver(_targetA, _targetB, _sourcePlayer);
        context.Stack.Push(forcedSlashResolver, forcedSlashContext);

        return ResolutionResult.SuccessResult;
    }

    /// <summary>
    /// Checks if a slash from attacker to target is legal (second legality check).
    /// </summary>
    private static bool IsSlashLegal(
        ResolutionContext context,
        Player attacker,
        Player target)
    {
        if (context.RuleService is null)
        {
            return false;
        }

        // Re-validate target is alive
        if (!target.IsAlive)
        {
            return false;
        }

        // Create a virtual Slash card for checking legality
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
            SourcePlayer: attacker,
            Card: virtualSlash,
            CandidateTargets: context.Game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 0);

        var legalTargets = context.RuleService.GetLegalTargetsForUse(usageContext);
        return legalTargets.HasAny && legalTargets.Items.Contains(target);
    }

    /// <summary>
    /// Transfers weapon from A's equipment zone to source player's hand zone.
    /// </summary>
    private ResolutionResult TransferWeapon(ResolutionContext context, Game game)
    {
        // Check if A still has a weapon (tolerance for edge cases)
        var weapon = _targetA.EquipmentZone.Cards?.FirstOrDefault(c => c.CardSubType == CardSubType.Weapon);
        if (weapon is null)
        {
            // No weapon to transfer, but this is acceptable (weapon might have been removed)
            if (context.LogSink is not null)
            {
                context.LogSink.Log(new LogEntry
                {
                    EventType = "JieDaoShaRenNoWeapon",
                    Level = "Info",
                    Message = $"Jie Dao Sha Ren: Player {_targetA.Seat} has no weapon to transfer",
                    Data = new
                    {
                        TargetASeat = _targetA.Seat,
                        SourcePlayerSeat = _sourcePlayer.Seat
                    }
                });
            }
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
                    context.SkillManager.RemoveEquipmentSkill(game, _targetA, equipmentSkill.Id);
                }
            }

            // Move weapon from A's equipment zone to source player's hand zone
            if (_sourcePlayer.HandZone is not Zone targetHandZone)
            {
                return ResolutionResult.Failure(
                    ResolutionErrorCode.InvalidState,
                    messageKey: "resolution.jiedaosharen.invalidTargetHandZone");
            }

            var moveDescriptor = new CardMoveDescriptor(
                SourceZone: _targetA.EquipmentZone,
                TargetZone: targetHandZone,
                Cards: new[] { weapon },
                Reason: CardMoveReason.Play, // Using Play as there's no specific reason for weapon transfer
                Ordering: CardMoveOrdering.ToTop,
                Game: game);

            context.CardMoveService.MoveSingle(moveDescriptor);

            // Publish WeaponTransferredEvent
            if (context.EventBus is not null)
            {
                var weaponTransferredEvent = new Events.WeaponTransferredEvent(
                    game,
                    _targetA.Seat,
                    _sourcePlayer.Seat,
                    weapon.Id,
                    weapon.CardSubType,
                    "JieDaoShaRen");
                context.EventBus.Publish(weaponTransferredEvent);
            }

            // Log the effect if log sink is available
            if (context.LogSink is not null)
            {
                context.LogSink.Log(new LogEntry
                {
                    EventType = "JieDaoShaRenWeaponTransferred",
                    Level = "Info",
                    Message = $"Player {_sourcePlayer.Seat} obtained weapon {weapon.Id} from player {_targetA.Seat}",
                    Data = new
                    {
                        SourcePlayerSeat = _sourcePlayer.Seat,
                        TargetASeat = _targetA.Seat,
                        WeaponId = weapon.Id,
                        WeaponSubType = weapon.CardSubType
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
}
