using System;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests;

[TestClass]
public sealed class JieDaoShaRenTests
{
    private static Game CreateDefaultGame(int playerCount = 3)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Card CreateJieDaoShaRenCard(int id = 1)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "jiedaosharen",
            Name = "借刀杀人",
            CardType = CardType.Trick,
            CardSubType = CardSubType.JieDaoShaRen,
            Suit = Suit.Heart,
            Rank = 3
        };
    }

    private static Card CreateWeaponCard(int id, string definitionId = "test_weapon")
    {
        return new Card
        {
            Id = id,
            DefinitionId = definitionId,
            Name = "Test Weapon",
            CardType = CardType.Equip,
            CardSubType = CardSubType.Weapon,
            Suit = Suit.Spade,
            Rank = 5
        };
    }

    private static Card CreateSlashCard(int id)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"slash_{id}",
            Name = "杀",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = Suit.Spade,
            Rank = 5
        };
    }

    private static Card CreateDodgeCard(int id)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"dodge_{id}",
            Name = "闪",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Dodge,
            Suit = Suit.Heart,
            Rank = 5
        };
    }

    private static Card CreateWuxiekejiCard(int id)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"wuxiekeji_{id}",
            Name = "无懈可击",
            CardType = CardType.Trick,
            CardSubType = CardSubType.Wuxiekeji,
            Suit = Suit.Club,
            Rank = 5
        };
    }

    #region JieDaoShaRenResolver Tests

    /// <summary>
    /// Tests normal flow: A has weapon and slash, chooses to use slash -> enters slash resolution.
    /// Input: Game with 3 players, A has weapon and slash, B is in A's attack range.
    /// Expected: A uses slash on B, weapon is not transferred.
    /// </summary>
    [TestMethod]
    public void JieDaoShaRenResolver_NormalFlow_AUsesSlash()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        var source = game.Players[0];
        var targetA = game.Players[1];
        var targetB = game.Players[2];

        // Give source the JieDaoShaRen card
        var jieDaoShaRenCard = CreateJieDaoShaRenCard(1);
        ((Zone)source.HandZone).MutableCards.Add(jieDaoShaRenCard);

        // Give targetA a weapon and a slash
        var weapon = CreateWeaponCard(10);
        var slash = CreateSlashCard(20);
        ((Zone)targetA.EquipmentZone).MutableCards.Add(weapon);
        ((Zone)targetA.HandZone).MutableCards.Add(slash);

        // Give targetB a dodge (so they can respond to slash)
        var dodge = CreateDodgeCard(30);
        ((Zone)targetB.HandZone).MutableCards.Add(dodge);

        var initialTargetAWeaponCount = targetA.EquipmentZone.Cards.Count;
        var initialSourceHandCount = source.HandZone.Cards.Count;

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { targetA.Seat },
            SelectedCardIds: new[] { jieDaoShaRenCard.Id },
            SelectedOptionId: null,
            Confirmed: null
        );

        var selectionStep = 0;
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            // Step 1: Select target B (A's slash target)
            if (request.ChoiceType == ChoiceType.SelectTargets && selectionStep == 0)
            {
                selectionStep++;
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: new[] { targetB.Seat },
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: null
                );
            }

            // Step 2: Handle nullification window (no response)
            if (request.ChoiceType == ChoiceType.SelectCards)
            {
                if (request.AllowedCards is null || request.AllowedCards.Count == 0)
                {
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: null,
                        SelectedOptionId: null,
                        Confirmed: null
                    );
                }

                if (request.AllowedCards.Any(c => c.CardSubType == CardSubType.Wuxiekeji))
                {
                    // No nullification response
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: null,
                        SelectedOptionId: null,
                        Confirmed: null
                    );
                }
            }

            // Step 3: A chooses to use slash (when forced by JieDaoShaRen)
            if (request.ChoiceType == ChoiceType.SelectCards && 
                request.PlayerSeat == targetA.Seat &&
                request.AllowedCards?.Any(c => c.CardSubType == CardSubType.Slash) == true)
            {
                // A chooses to use slash
                var slashCard = request.AllowedCards.First(c => c.CardSubType == CardSubType.Slash);
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null, // Target is already fixed by JieDaoShaRen
                    SelectedCardIds: new[] { slashCard.Id },
                    SelectedOptionId: null,
                    Confirmed: null
                );
            }

            // Step 4: B responds to slash with dodge
            if (request.ChoiceType == ChoiceType.SelectCards &&
                request.PlayerSeat == targetB.Seat &&
                request.AllowedCards?.Any(c => c.CardSubType == CardSubType.Dodge) == true)
            {
                var dodgeCard = request.AllowedCards.First(c => c.CardSubType == CardSubType.Dodge);
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: new[] { dodgeCard.Id },
                    SelectedOptionId: null,
                    Confirmed: null
                );
            }

            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var context = new ResolutionContext(
            game,
            source,
            Action: null,
            choice,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: getPlayerChoice
        );

        var resolver = new JieDaoShaRenResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success, "Resolver should succeed.");

        // Execute stack to process all resolvers
        while (!stack.IsEmpty)
        {
            var stackResult = stack.Pop();
            Assert.IsTrue(stackResult.Success, 
                $"Stack execution should succeed. Error: {stackResult.ErrorCode}, Message: {stackResult.MessageKey}");
        }

        // Verify weapon was NOT transferred (A used slash)
        Assert.AreEqual(initialTargetAWeaponCount, targetA.EquipmentZone.Cards.Count, 
            "TargetA should still have weapon (used slash instead).");
        Assert.IsTrue(targetA.EquipmentZone.Cards.Contains(weapon), 
            "TargetA should still have the weapon.");

        // Verify slash was used (should be in discard pile)
        Assert.IsTrue(game.DiscardPile.Cards.Any(c => c.Id == slash.Id), 
            "Slash should be in discard pile after use.");

        // Verify dodge was used (should be in discard pile)
        Assert.IsTrue(game.DiscardPile.Cards.Any(c => c.Id == dodge.Id), 
            "Dodge should be in discard pile after use.");
    }

    /// <summary>
    /// Tests that A refuses to use slash -> weapon is transferred.
    /// Input: Game with 3 players, A has weapon and slash, but chooses not to use slash.
    /// Expected: Weapon is transferred to source player's hand.
    /// </summary>
    [TestMethod]
    public void JieDaoShaRenResolver_ARefusesSlash_WeaponTransferred()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        var source = game.Players[0];
        var targetA = game.Players[1];
        var targetB = game.Players[2];

        var jieDaoShaRenCard = CreateJieDaoShaRenCard(1);
        ((Zone)source.HandZone).MutableCards.Add(jieDaoShaRenCard);

        var weapon = CreateWeaponCard(10);
        var slash = CreateSlashCard(20);
        ((Zone)targetA.EquipmentZone).MutableCards.Add(weapon);
        ((Zone)targetA.HandZone).MutableCards.Add(slash);

        var initialTargetAWeaponCount = targetA.EquipmentZone.Cards.Count;
        var initialSourceHandCount = source.HandZone.Cards.Count;

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { targetA.Seat },
            SelectedCardIds: new[] { jieDaoShaRenCard.Id },
            SelectedOptionId: null,
            Confirmed: null
        );

        var selectionStep = 0;
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            // Step 1: Select target B
            if (request.ChoiceType == ChoiceType.SelectTargets && selectionStep == 0)
            {
                selectionStep++;
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: new[] { targetB.Seat },
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: null
                );
            }

            // Step 2: Handle nullification (no response)
            if (request.ChoiceType == ChoiceType.SelectCards)
            {
                if (request.AllowedCards is null || request.AllowedCards.Count == 0)
                {
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: null,
                        SelectedOptionId: null,
                        Confirmed: null
                    );
                }

                if (request.AllowedCards.Any(c => c.CardSubType == CardSubType.Wuxiekeji))
                {
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: null,
                        SelectedOptionId: null,
                        Confirmed: null
                    );
                }

                // A refuses to use slash (returns no card when asked to select)
                if (request.PlayerSeat == targetA.Seat &&
                    request.AllowedCards?.Any(c => c.CardSubType == CardSubType.Slash) == true &&
                    request.CanPass == true) // This is the JieDaoShaRen forced slash choice
                {
                    // A chooses not to use slash (passes)
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: null, // No card selected = refuse
                        SelectedOptionId: null,
                        Confirmed: null
                    );
                }
            }

            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var context = new ResolutionContext(
            game,
            source,
            Action: null,
            choice,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: getPlayerChoice
        );

        var resolver = new JieDaoShaRenResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);

        // Execute stack
        while (!stack.IsEmpty)
        {
            var stackResult = stack.Pop();
            Assert.IsTrue(stackResult.Success, 
                $"Stack execution should succeed. Error: {stackResult.ErrorCode}");
        }

        // Verify weapon was transferred
        Assert.AreEqual(initialTargetAWeaponCount - 1, targetA.EquipmentZone.Cards.Count, 
            "TargetA should have lost weapon.");
        Assert.IsFalse(targetA.EquipmentZone.Cards.Contains(weapon), 
            "TargetA should not have the weapon anymore.");
        Assert.AreEqual(initialSourceHandCount + 1, source.HandZone.Cards.Count, 
            "Source should have gained weapon in hand.");
        Assert.IsTrue(source.HandZone.Cards.Contains(weapon), 
            "Source should have the weapon in hand.");
    }

    /// <summary>
    /// Tests that A cannot use slash (no slash card, no skills) -> weapon is transferred.
    /// Input: Game with 3 players, A has weapon but no slash and no skills.
    /// Expected: Weapon is transferred to source player's hand.
    /// </summary>
    [TestMethod]
    public void JieDaoShaRenResolver_ACannotUseSlash_WeaponTransferred()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        var source = game.Players[0];
        var targetA = game.Players[1];
        var targetB = game.Players[2];

        var jieDaoShaRenCard = CreateJieDaoShaRenCard(1);
        ((Zone)source.HandZone).MutableCards.Add(jieDaoShaRenCard);

        var weapon = CreateWeaponCard(10);
        ((Zone)targetA.EquipmentZone).MutableCards.Add(weapon);
        // No slash card for targetA

        var initialTargetAWeaponCount = targetA.EquipmentZone.Cards.Count;
        var initialSourceHandCount = source.HandZone.Cards.Count;

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { targetA.Seat },
            SelectedCardIds: new[] { jieDaoShaRenCard.Id },
            SelectedOptionId: null,
            Confirmed: null
        );

        var selectionStep = 0;
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            // Step 1: Select target B
            if (request.ChoiceType == ChoiceType.SelectTargets && selectionStep == 0)
            {
                selectionStep++;
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: new[] { targetB.Seat },
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: null
                );
            }

            // Step 2: Handle nullification (no response)
            if (request.ChoiceType == ChoiceType.SelectCards)
            {
                if (request.AllowedCards is null || request.AllowedCards.Count == 0)
                {
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: null,
                        SelectedOptionId: null,
                        Confirmed: null
                    );
                }

                if (request.AllowedCards.Any(c => c.CardSubType == CardSubType.Wuxiekeji))
                {
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: null,
                        SelectedOptionId: null,
                        Confirmed: null
                    );
                }
            }

            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var context = new ResolutionContext(
            game,
            source,
            Action: null,
            choice,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: getPlayerChoice
        );

        var resolver = new JieDaoShaRenResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);

        // Execute stack
        while (!stack.IsEmpty)
        {
            var stackResult = stack.Pop();
            Assert.IsTrue(stackResult.Success, 
                $"Stack execution should succeed. Error: {stackResult.ErrorCode}");
        }

        // Verify weapon was transferred
        Assert.AreEqual(initialTargetAWeaponCount - 1, targetA.EquipmentZone.Cards.Count, 
            "TargetA should have lost weapon.");
        Assert.IsFalse(targetA.EquipmentZone.Cards.Contains(weapon), 
            "TargetA should not have the weapon anymore.");
        Assert.AreEqual(initialSourceHandCount + 1, source.HandZone.Cards.Count, 
            "Source should have gained weapon in hand.");
        Assert.IsTrue(source.HandZone.Cards.Contains(weapon), 
            "Source should have the weapon in hand.");
    }

    /// <summary>
    /// Tests second legality check failure: B is legal at selection but not at resolution -> weapon transferred.
    /// Input: Game with 3 players, A has weapon, B is legal at selection but becomes illegal (dies or out of range).
    /// Expected: Weapon is transferred (second legality check fails).
    /// </summary>
    [TestMethod]
    public void JieDaoShaRenResolver_SecondLegalityCheckFails_WeaponTransferred()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        var source = game.Players[0];
        var targetA = game.Players[1];
        var targetB = game.Players[2];

        var jieDaoShaRenCard = CreateJieDaoShaRenCard(1);
        ((Zone)source.HandZone).MutableCards.Add(jieDaoShaRenCard);

        var weapon = CreateWeaponCard(10);
        var slash = CreateSlashCard(20);
        ((Zone)targetA.EquipmentZone).MutableCards.Add(weapon);
        ((Zone)targetA.HandZone).MutableCards.Add(slash);

        var initialTargetAWeaponCount = targetA.EquipmentZone.Cards.Count;
        var initialSourceHandCount = source.HandZone.Cards.Count;

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { targetA.Seat },
            SelectedCardIds: new[] { jieDaoShaRenCard.Id },
            SelectedOptionId: null,
            Confirmed: null
        );

        var selectionStep = 0;
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            // Step 1: Select target B
            if (request.ChoiceType == ChoiceType.SelectTargets && selectionStep == 0)
            {
                selectionStep++;
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: new[] { targetB.Seat },
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: null
                );
            }

            // Step 2: Handle nullification (no response)
            if (request.ChoiceType == ChoiceType.SelectCards)
            {
                if (request.AllowedCards is null || request.AllowedCards.Count == 0)
                {
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: null,
                        SelectedOptionId: null,
                        Confirmed: null
                    );
                }

                if (request.AllowedCards.Any(c => c.CardSubType == CardSubType.Wuxiekeji))
                {
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: null,
                        SelectedOptionId: null,
                        Confirmed: null
                    );
                }
            }

            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var context = new ResolutionContext(
            game,
            source,
            Action: null,
            choice,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: getPlayerChoice
        );

        var resolver = new JieDaoShaRenResolver();

        // Act - resolve first part
        var result = resolver.Resolve(context);

        // Simulate targetB dying before resolution (second legality check will fail)
        targetB.CurrentHealth = 0;
        targetB.IsAlive = false;

        // Execute stack
        while (!stack.IsEmpty)
        {
            var stackResult = stack.Pop();
            Assert.IsTrue(stackResult.Success, 
                $"Stack execution should succeed. Error: {stackResult.ErrorCode}");
        }

        // Assert - weapon should be transferred because second legality check fails
        Assert.AreEqual(initialTargetAWeaponCount - 1, targetA.EquipmentZone.Cards.Count, 
            "TargetA should have lost weapon (second legality check failed).");
        Assert.IsFalse(targetA.EquipmentZone.Cards.Contains(weapon), 
            "TargetA should not have the weapon anymore.");
        Assert.AreEqual(initialSourceHandCount + 1, source.HandZone.Cards.Count, 
            "Source should have gained weapon in hand.");
        Assert.IsTrue(source.HandZone.Cards.Contains(weapon), 
            "Source should have the weapon in hand.");
    }

    /// <summary>
    /// Tests nullification: JieDaoShaRen is nullified -> no effect.
    /// Input: Game with 3 players, source uses JieDaoShaRen, targetA nullifies it.
    /// Expected: No weapon transfer, no slash use.
    /// </summary>
    [TestMethod]
    public void JieDaoShaRenResolver_Nullified_NoEffect()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        var source = game.Players[0];
        var targetA = game.Players[1];
        var targetB = game.Players[2];

        var jieDaoShaRenCard = CreateJieDaoShaRenCard(1);
        ((Zone)source.HandZone).MutableCards.Add(jieDaoShaRenCard);

        var weapon = CreateWeaponCard(10);
        var slash = CreateSlashCard(20);
        ((Zone)targetA.EquipmentZone).MutableCards.Add(weapon);
        ((Zone)targetA.HandZone).MutableCards.Add(slash);

        var wuxiekeji = CreateWuxiekejiCard(30);
        ((Zone)targetA.HandZone).MutableCards.Add(wuxiekeji);

        var initialTargetAWeaponCount = targetA.EquipmentZone.Cards.Count;
        var initialTargetAHandCount = targetA.HandZone.Cards.Count; // 2 cards: slash + wuxiekeji

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { targetA.Seat },
            SelectedCardIds: new[] { jieDaoShaRenCard.Id },
            SelectedOptionId: null,
            Confirmed: null
        );

        var selectionStep = 0;
        var nullificationRequestCount = 0;
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            // Step 1: Select target B
            if (request.ChoiceType == ChoiceType.SelectTargets && selectionStep == 0)
            {
                selectionStep++;
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: new[] { targetB.Seat },
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: null
                );
            }

            // Step 2: Handle nullification windows
            if (request.ChoiceType == ChoiceType.SelectCards &&
                request.AllowedCards?.Any(c => c.CardSubType == CardSubType.Wuxiekeji) == true)
            {
                nullificationRequestCount++;
                
                // First nullification request: targetA uses Wuxiekeji
                if (nullificationRequestCount == 1 && request.PlayerSeat == targetA.Seat)
                {
                    var wuxiekejiCard = request.AllowedCards.First(c => c.CardSubType == CardSubType.Wuxiekeji);
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: new[] { wuxiekejiCard.Id },
                        SelectedOptionId: null,
                        Confirmed: null
                    );
                }
                
                // Second nullification request (chain): no one nullifies the nullification
                if (nullificationRequestCount >= 2)
                {
                    // No one nullifies the nullification, pass
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: null,
                        SelectedOptionId: null,
                        Confirmed: null
                    );
                }
            }

            // Step 3: No further nullification or other choices
            if (request.ChoiceType == ChoiceType.SelectCards)
            {
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: null
                );
            }

            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var context = new ResolutionContext(
            game,
            source,
            Action: null,
            choice,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: getPlayerChoice
        );

        var resolver = new JieDaoShaRenResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);

        // Execute stack
        while (!stack.IsEmpty)
        {
            var stackResult = stack.Pop();
            Assert.IsTrue(stackResult.Success, 
                $"Stack execution should succeed. Error: {stackResult.ErrorCode}");
        }

        // Verify weapon was NOT transferred (effect was nullified)
        Assert.AreEqual(initialTargetAWeaponCount, targetA.EquipmentZone.Cards.Count, 
            "TargetA should still have weapon (effect was nullified).");
        Assert.IsTrue(targetA.EquipmentZone.Cards.Contains(weapon), 
            "TargetA should still have the weapon.");

        // Verify slash was NOT used (still in hand)
        Assert.AreEqual(initialTargetAHandCount - 1, targetA.HandZone.Cards.Count, 
            "TargetA should have 1 card (slash) after using Wuxiekeji (effect was nullified).");
        Assert.IsTrue(targetA.HandZone.Cards.Contains(slash), 
            "TargetA should still have the slash.");

        // Verify Wuxiekeji was used and moved to discard pile
        Assert.IsTrue(game.DiscardPile.Cards.Any(c => c.Id == wuxiekeji.Id), 
            "Wuxiekeji should be in discard pile after use.");
        Assert.IsFalse(targetA.HandZone.Cards.Contains(wuxiekeji), 
            "TargetA should not have Wuxiekeji in hand (it was used).");
    }

    /// <summary>
    /// Tests edge case: weapon is removed before resolution -> graceful handling.
    /// Input: Game with 3 players, A has weapon at selection, but weapon is removed before resolution.
    /// Expected: Resolution completes gracefully, no weapon transfer (weapon already gone).
    /// </summary>
    [TestMethod]
    public void JieDaoShaRenResolver_WeaponRemovedBeforeResolution_GracefulHandling()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        var source = game.Players[0];
        var targetA = game.Players[1];
        var targetB = game.Players[2];

        var jieDaoShaRenCard = CreateJieDaoShaRenCard(1);
        ((Zone)source.HandZone).MutableCards.Add(jieDaoShaRenCard);

        var weapon = CreateWeaponCard(10);
        var slash = CreateSlashCard(20);
        ((Zone)targetA.EquipmentZone).MutableCards.Add(weapon);
        ((Zone)targetA.HandZone).MutableCards.Add(slash);

        var initialTargetAWeaponCount = targetA.EquipmentZone.Cards.Count;
        var initialSourceHandCount = source.HandZone.Cards.Count;

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var stack = new BasicResolutionStack();

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: source.Seat,
            SelectedTargetSeats: new[] { targetA.Seat },
            SelectedCardIds: new[] { jieDaoShaRenCard.Id },
            SelectedOptionId: null,
            Confirmed: null
        );

        var selectionStep = 0;
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            // Step 1: Select target B
            if (request.ChoiceType == ChoiceType.SelectTargets && selectionStep == 0)
            {
                selectionStep++;
                // Simulate weapon being removed after selection but before resolution
                ((Zone)targetA.EquipmentZone).MutableCards.Remove(weapon);
                ((Zone)game.DiscardPile).MutableCards.Add(weapon);
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: new[] { targetB.Seat },
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: null
                );
            }

            // Step 2: Handle nullification (no response)
            if (request.ChoiceType == ChoiceType.SelectCards)
            {
                if (request.AllowedCards is null || request.AllowedCards.Count == 0)
                {
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: null,
                        SelectedOptionId: null,
                        Confirmed: null
                    );
                }

                if (request.AllowedCards.Any(c => c.CardSubType == CardSubType.Wuxiekeji))
                {
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: null,
                        SelectedOptionId: null,
                        Confirmed: null
                    );
                }

                // A refuses to use slash (weapon already gone, so transfer will fail gracefully)
                if (request.PlayerSeat == targetA.Seat &&
                    request.AllowedCards.Any(c => c.CardSubType == CardSubType.Slash))
                {
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: null,
                        SelectedOptionId: null,
                        Confirmed: null
                    );
                }
            }

            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var context = new ResolutionContext(
            game,
            source,
            Action: null,
            choice,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            GetPlayerChoice: getPlayerChoice
        );

        var resolver = new JieDaoShaRenResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success);

        // Execute stack
        while (!stack.IsEmpty)
        {
            var stackResult = stack.Pop();
            Assert.IsTrue(stackResult.Success, 
                $"Stack execution should succeed. Error: {stackResult.ErrorCode}");
        }

        // Verify weapon is in discard pile (was removed before resolution)
        Assert.IsTrue(game.DiscardPile.Cards.Contains(weapon), 
            "Weapon should be in discard pile (was removed before resolution).");
        Assert.AreEqual(0, targetA.EquipmentZone.Cards.Count, 
            "TargetA should have no weapon (was removed).");
        // Source should not have gained weapon (transfer failed gracefully)
        Assert.AreEqual(initialSourceHandCount, source.HandZone.Cards.Count, 
            "Source should not have gained weapon (weapon was already removed).");
    }

    #endregion
}
