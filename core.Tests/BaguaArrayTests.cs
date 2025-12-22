using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Judgement;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Response;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Skills.Equipment;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests;

[TestClass]
public sealed class BaguaArrayTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Card CreateBaguaArrayCard(int id = 1, string definitionId = "bagua_array", string name = "八卦阵")
    {
        return new Card
        {
            Id = id,
            DefinitionId = definitionId,
            Name = name,
            CardType = CardType.Equip,
            CardSubType = CardSubType.Armor,
            Suit = Suit.Spade,
            Rank = 2
        };
    }

    private static Card CreateSlashCard(int id, Suit suit = Suit.Spade)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"slash_{id}",
            Name = "杀",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = suit,
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
            Suit = Suit.Spade,
            Rank = 2
        };
    }

    private static object CreateSlashSourceEvent(int sourceSeat, int targetSeat, Card slashCard)
    {
        return new { Type = "Slash", SourceSeat = sourceSeat, TargetSeat = targetSeat, SlashCard = slashCard };
    }

    #region BaguaArraySkill Tests

    /// <summary>
    /// Tests that BaguaFormation.CanProvideResponse returns true for JinkAgainstSlash when skill is active.
    /// Input: Game, player with active BaguaFormation, ResponseType.JinkAgainstSlash.
    /// Expected: CanProvideResponse returns true.
    /// </summary>
    [TestMethod]
    public void BaguaArraySkillCanProvideResponseReturnsTrueForJinkAgainstSlash()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = true;
        var skill = new BaguaFormation();
        var sourceEvent = CreateSlashSourceEvent(1, 0, CreateSlashCard(100));

        // Act
        var result = skill.CanProvideResponse(game, player, ResponseType.JinkAgainstSlash, sourceEvent);

        // Assert
        Assert.IsTrue(result, "Bagua Array should be able to provide response for JinkAgainstSlash.");
    }

    /// <summary>
    /// Tests that BaguaFormation.CanProvideResponse returns false for non-JinkAgainstSlash response types.
    /// Input: Game, player with active BaguaFormation, ResponseType.PeachForDying.
    /// Expected: CanProvideResponse returns false.
    /// </summary>
    [TestMethod]
    public void BaguaArraySkillCanProvideResponseReturnsFalseForNonJinkResponseTypes()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = true;
        var skill = new BaguaFormation();

        // Act
        var result = skill.CanProvideResponse(game, player, ResponseType.PeachForDying, null);

        // Assert
        Assert.IsFalse(result, "Bagua Array should not provide response for PeachForDying.");
    }

    /// <summary>
    /// Tests that BaguaArraySkill.ExecuteAlternativeResponse returns true when judgement succeeds (red card).
    /// Input: Game with red card in draw pile, player, judgement service.
    /// Expected: ExecuteAlternativeResponse returns true after successful red judgement.
    /// </summary>
    [TestMethod]
    public void BaguaArraySkillExecuteAlternativeResponseReturnsTrueWhenJudgementSucceeds()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = true;

        // Add red card to draw pile
        if (game.DrawPile is Zone drawZone)
        {
            var redCard = new Card
            {
                Id = 1,
                DefinitionId = "red_card",
                Name = "Red Card",
                CardType = CardType.Basic,
                CardSubType = CardSubType.Slash,
                Suit = Suit.Heart,
                Rank = 5
            };
            drawZone.MutableCards.Add(redCard);
        }

        var skill = new BaguaFormation();
        var sourceEvent = CreateSlashSourceEvent(1, 0, CreateSlashCard(100));
        var cardMoveService = new BasicCardMoveService();
        var judgementService = new BasicJudgementService();

        // Mock getPlayerChoice to return confirmed (player chooses to activate)
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = request =>
        {
            return new ChoiceResult(
                request.RequestId,
                player.Seat,
                null,
                null,
                null,
                true // Confirmed: player chooses to activate Bagua Array
            );
        };

        // Act
        var result = skill.ExecuteAlternativeResponse(
            game,
            player,
            ResponseType.JinkAgainstSlash,
            sourceEvent,
            getPlayerChoice,
            judgementService,
            cardMoveService);

        // Assert
        Assert.IsTrue(result, "Bagua Array should return true when red card judgement succeeds.");
        Assert.IsTrue(game.DiscardPile.Cards.Any(c => c.Suit.IsRed()), "Red judgement card should be in discard pile.");
    }

    /// <summary>
    /// Tests that BaguaFormation.ExecuteAlternativeResponse returns false when judgement fails (black card).
    /// Input: Game with black card in draw pile, player, judgement service.
    /// Expected: ExecuteAlternativeResponse returns false after failed black judgement.
    /// </summary>
    [TestMethod]
    public void BaguaArraySkillExecuteAlternativeResponseReturnsFalseWhenJudgementFails()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = true;

        // Add black card to draw pile
        if (game.DrawPile is Zone drawZone)
        {
            var blackCard = new Card
            {
                Id = 1,
                DefinitionId = "black_card",
                Name = "Black Card",
                CardType = CardType.Basic,
                CardSubType = CardSubType.Slash,
                Suit = Suit.Spade,
                Rank = 5
            };
            drawZone.MutableCards.Add(blackCard);
        }

        var skill = new BaguaFormation();
        var sourceEvent = CreateSlashSourceEvent(1, 0, CreateSlashCard(100));
        var cardMoveService = new BasicCardMoveService();
        var judgementService = new BasicJudgementService();

        // Mock getPlayerChoice to return confirmed
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = request =>
        {
            return new ChoiceResult(
                request.RequestId,
                player.Seat,
                null,
                null,
                null,
                true
            );
        };

        // Act
        var result = skill.ExecuteAlternativeResponse(
            game,
            player,
            ResponseType.JinkAgainstSlash,
            sourceEvent,
            getPlayerChoice,
            judgementService,
            cardMoveService);

        // Assert
        Assert.IsFalse(result, "Bagua Array should return false when black card judgement fails.");
        Assert.IsTrue(game.DiscardPile.Cards.Any(c => c.Suit.IsBlack()), "Black judgement card should be in discard pile.");
    }

    /// <summary>
    /// Tests that BaguaFormation.ExecuteAlternativeResponse returns false when player chooses not to activate.
    /// Input: Game, player, getPlayerChoice returns not confirmed.
    /// Expected: ExecuteAlternativeResponse returns false without executing judgement.
    /// </summary>
    [TestMethod]
    public void BaguaArraySkillExecuteAlternativeResponseReturnsFalseWhenPlayerChoosesNotToActivate()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        player.IsAlive = true;

        var skill = new BaguaFormation();
        var sourceEvent = CreateSlashSourceEvent(1, 0, CreateSlashCard(100));
        var cardMoveService = new BasicCardMoveService();
        var judgementService = new BasicJudgementService();

        // Mock getPlayerChoice to return not confirmed (player chooses not to activate)
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = request =>
        {
            return new ChoiceResult(
                request.RequestId,
                player.Seat,
                null,
                null,
                null,
                false // Not confirmed: player chooses not to activate
            );
        };

        var initialDrawPileCount = game.DrawPile.Cards.Count;

        // Act
        var result = skill.ExecuteAlternativeResponse(
            game,
            player,
            ResponseType.JinkAgainstSlash,
            sourceEvent,
            getPlayerChoice,
            judgementService,
            cardMoveService);

        // Assert
        Assert.IsFalse(result, "Bagua Array should return false when player chooses not to activate.");
        Assert.AreEqual(initialDrawPileCount, game.DrawPile.Cards.Count, "No card should be drawn if player chooses not to activate.");
    }

    #endregion

    #region Response Window Integration Tests

    /// <summary>
    /// Tests that BasicResponseWindow uses Bagua Array when player has no Dodge cards and judgement succeeds.
    /// Input: Game, player with Bagua Array but no Dodge cards, red card in draw pile.
    /// Expected: Response window returns ResponseSuccess after successful Bagua Array judgement.
    /// </summary>
    [TestMethod]
    public void BasicResponseWindowUsesBaguaArrayWhenNoDodgeCardsAndJudgementSucceeds()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var defender = game.Players[1];
        defender.IsAlive = true;

        // Add red card to draw pile
        if (game.DrawPile is Zone drawZone)
        {
            var redCard = new Card
            {
                Id = 1,
                DefinitionId = "red_card",
                Name = "Red Card",
                CardType = CardType.Basic,
                CardSubType = CardSubType.Slash,
                Suit = Suit.Heart,
                Rank = 5
            };
            drawZone.MutableCards.Add(redCard);
        }

        // Setup skill system
        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.LoadSkillsForPlayer(game, defender);

        // Add Bagua Array skill
        var baguaArraySkill = new BaguaFormation();
        skillManager.AddEquipmentSkill(game, defender, baguaArraySkill);

        var slashCard = CreateSlashCard(100);
        var sourceEvent = CreateSlashSourceEvent(attacker.Seat, defender.Seat, slashCard);

        var ruleService = new RuleService(skillManager: skillManager);
        var responseRuleService = new ResponseRuleService();
        var choiceFactory = new ChoiceRequestFactory();
        var cardMoveService = new BasicCardMoveService();
        var judgementService = new BasicJudgementService(eventBus);

        var windowContext = new ResponseWindowContext(
            game,
            ResponseType.JinkAgainstSlash,
            new[] { defender },
            sourceEvent,
            ruleService,
            responseRuleService,
            choiceFactory,
            cardMoveService,
            LogSink: null,
            skillManager,
            judgementService
        );

        // Mock getPlayerChoice: player chooses to activate Bagua Array
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = request =>
        {
            if (request.ChoiceType == ChoiceType.Confirm)
            {
                return new ChoiceResult(
                    request.RequestId,
                    defender.Seat,
                    null,
                    null,
                    null,
                    true // Activate Bagua Array
                );
            }
            return new ChoiceResult(request.RequestId, defender.Seat, null, null, null, null);
        };

        var responseWindow = new BasicResponseWindow();

        // Act
        var result = responseWindow.Execute(windowContext, getPlayerChoice);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(ResponseWindowState.ResponseSuccess, result.State, "Response should be successful after Bagua Array judgement succeeds.");
        Assert.AreEqual(defender.Seat, result.Responder?.Seat);
    }

    /// <summary>
    /// Tests that BasicResponseWindow allows player to use Bagua Array even when they have Dodge cards but choose not to use them.
    /// Input: Game, player with Bagua Array and Dodge card, player chooses not to use Dodge, then activates Bagua Array with red card.
    /// Expected: Response window returns ResponseSuccess after successful Bagua Array judgement.
    /// </summary>
    [TestMethod]
    public void BasicResponseWindowAllowsBaguaArrayWhenPlayerHasDodgeButChoosesNotToUseIt()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var defender = game.Players[1];
        defender.IsAlive = true;

        // Add Dodge card to defender's hand
        var dodgeCard = CreateDodgeCard(1);
        if (defender.HandZone is Zone handZone)
        {
            handZone.MutableCards.Add(dodgeCard);
        }

        // Add red card to draw pile
        if (game.DrawPile is Zone drawZone)
        {
            var redCard = new Card
            {
                Id = 2,
                DefinitionId = "red_card",
                Name = "Red Card",
                CardType = CardType.Basic,
                CardSubType = CardSubType.Slash,
                Suit = Suit.Heart,
                Rank = 5
            };
            drawZone.MutableCards.Add(redCard);
        }

        // Setup skill system
        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.LoadSkillsForPlayer(game, defender);

        // Add Bagua Array skill
        var baguaArraySkill = new BaguaFormation();
        skillManager.AddEquipmentSkill(game, defender, baguaArraySkill);

        var slashCard = CreateSlashCard(100);
        var sourceEvent = CreateSlashSourceEvent(attacker.Seat, defender.Seat, slashCard);

        var ruleService = new RuleService(skillManager: skillManager);
        var responseRuleService = new ResponseRuleService();
        var choiceFactory = new ChoiceRequestFactory();
        var cardMoveService = new BasicCardMoveService();
        var judgementService = new BasicJudgementService(eventBus);

        var windowContext = new ResponseWindowContext(
            game,
            ResponseType.JinkAgainstSlash,
            new[] { defender },
            sourceEvent,
            ruleService,
            responseRuleService,
            choiceFactory,
            cardMoveService,
            LogSink: null,
            skillManager,
            judgementService
        );

        // Mock getPlayerChoice: first pass on Dodge, then activate Bagua Array
        var choiceCount = 0;
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = request =>
        {
            choiceCount++;
            if (request.ChoiceType == ChoiceType.SelectCards)
            {
                // Player chooses not to use Dodge (pass)
                return new ChoiceResult(request.RequestId, defender.Seat, null, null, null, null);
            }
            else if (request.ChoiceType == ChoiceType.Confirm)
            {
                // Player chooses to activate Bagua Array
                return new ChoiceResult(
                    request.RequestId,
                    defender.Seat,
                    null,
                    null,
                    null,
                    true
                );
            }
            return new ChoiceResult(request.RequestId, defender.Seat, null, null, null, null);
        };

        var responseWindow = new BasicResponseWindow();

        // Act
        var result = responseWindow.Execute(windowContext, getPlayerChoice);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(ResponseWindowState.ResponseSuccess, result.State, "Response should be successful after Bagua Array judgement succeeds.");
        Assert.IsTrue(dodgeCard.Id == 1 && defender.HandZone.Cards.Contains(dodgeCard), "Dodge card should still be in hand since Bagua Array was used instead.");
    }

    /// <summary>
    /// Tests that BasicResponseWindow does not use Bagua Array when armor is ignored by attacker.
    /// Input: Game, attacker with Qinggang Sword (armor ignore), defender with Bagua Array.
    /// Expected: Bagua Array is not triggered, response window returns NoResponse.
    /// </summary>
    [TestMethod]
    public void BasicResponseWindowDoesNotUseBaguaArrayWhenArmorIsIgnored()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var defender = game.Players[1];
        attacker.IsAlive = true;
        defender.IsAlive = true;

        // Add red card to draw pile
        if (game.DrawPile is Zone drawZone)
        {
            var redCard = new Card
            {
                Id = 1,
                DefinitionId = "red_card",
                Name = "Red Card",
                CardType = CardType.Basic,
                CardSubType = CardSubType.Slash,
                Suit = Suit.Heart,
                Rank = 5
            };
            drawZone.MutableCards.Add(redCard);
        }

        // Setup skill system
        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.LoadSkillsForPlayer(game, attacker);
        skillManager.LoadSkillsForPlayer(game, defender);

        // Add Qinggang Sword to attacker (armor ignore)
        var qinggangSwordSkill = new QinggangSwordSkill();
        skillManager.AddEquipmentSkill(game, attacker, qinggangSwordSkill);

        // Add Bagua Array to defender
        var baguaArraySkill = new BaguaFormation();
        skillManager.AddEquipmentSkill(game, defender, baguaArraySkill);

        var slashCard = CreateSlashCard(100);
        var sourceEvent = CreateSlashSourceEvent(attacker.Seat, defender.Seat, slashCard);

        var ruleService = new RuleService(skillManager: skillManager);
        var responseRuleService = new ResponseRuleService();
        var choiceFactory = new ChoiceRequestFactory();
        var cardMoveService = new BasicCardMoveService();
        var judgementService = new BasicJudgementService(eventBus);

        var windowContext = new ResponseWindowContext(
            game,
            ResponseType.JinkAgainstSlash,
            new[] { defender },
            sourceEvent,
            ruleService,
            responseRuleService,
            choiceFactory,
            cardMoveService,
            LogSink: null,
            skillManager,
            judgementService
        );

        // Mock getPlayerChoice: should not be called for Bagua Array activation
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = request =>
        {
            Assert.Fail("getPlayerChoice should not be called when armor is ignored.");
            return new ChoiceResult(request.RequestId, defender.Seat, null, null, null, null);
        };

        var responseWindow = new BasicResponseWindow();

        // Act
        var result = responseWindow.Execute(windowContext, getPlayerChoice);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(ResponseWindowState.NoResponse, result.State, "Response should be NoResponse when armor is ignored and no Dodge cards available.");
    }

    #endregion

    #region Integration Tests with SlashResolver

    /// <summary>
    /// Tests complete flow: Slash -> Response Window -> Bagua Array judgement -> Response Success.
    /// Input: Game, attacker uses Slash, defender has Bagua Array but no Dodge, red card in draw pile.
    /// Expected: Slash is dodged via Bagua Array, no damage is dealt.
    /// </summary>
    [TestMethod]
    public void SlashResolverWithBaguaArrayDodgesSlashWhenJudgementSucceeds()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var attacker = game.Players[0];
        var defender = game.Players[1];
        attacker.IsAlive = true;
        defender.IsAlive = true;

        // Add Slash to attacker's hand
        var slashCard = CreateSlashCard(1);
        if (attacker.HandZone is Zone attackerHand)
        {
            attackerHand.MutableCards.Add(slashCard);
        }

        // Add red card to draw pile
        if (game.DrawPile is Zone drawZone)
        {
            var redCard = new Card
            {
                Id = 2,
                DefinitionId = "red_card",
                Name = "Red Card",
                CardType = CardType.Basic,
                CardSubType = CardSubType.Slash,
                Suit = Suit.Heart,
                Rank = 5
            };
            drawZone.MutableCards.Add(redCard);
        }

        // Setup skill system
        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.LoadSkillsForPlayer(game, attacker);
        skillManager.LoadSkillsForPlayer(game, defender);

        // Add Bagua Array to defender
        var baguaArraySkill = new BaguaFormation();
        skillManager.AddEquipmentSkill(game, defender, baguaArraySkill);

        var cardMoveService = new BasicCardMoveService(eventBus);
        var ruleService = new RuleService(skillManager: skillManager);
        var stack = new BasicResolutionStack();
        var judgementService = new BasicJudgementService(eventBus);

        var initialDefenderHealth = defender.CurrentHealth;

        // Mock getPlayerChoice: for Slash target selection and Bagua Array activation
        var choiceCount = 0;
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = request =>
        {
            choiceCount++;
            if (request.ChoiceType == ChoiceType.SelectTargets)
            {
                // Attacker selects defender as target
                return new ChoiceResult(
                    request.RequestId,
                    attacker.Seat,
                    new[] { defender.Seat },
                    null,
                    null,
                    null
                );
            }
            else if (request.ChoiceType == ChoiceType.Confirm)
            {
                // Defender chooses to activate Bagua Array
                return new ChoiceResult(
                    request.RequestId,
                    defender.Seat,
                    null,
                    null,
                    null,
                    true
                );
            }
            return new ChoiceResult(request.RequestId, attacker.Seat, null, null, null, null);
        };

        // Create initial choice with selected card
        var initialChoice = new ChoiceResult(
            Guid.NewGuid().ToString(),
            attacker.Seat,
            null,
            new[] { slashCard.Id },
            null,
            null
        );

        var context = new ResolutionContext(
            game,
            attacker,
            new ActionDescriptor("use_card", null, false, new TargetConstraints(1, 1)),
            initialChoice,
            stack,
            cardMoveService,
            ruleService,
            null,
            null,
            getPlayerChoice,
            new Dictionary<string, object>(),
            eventBus,
            null,
            skillManager,
            null,
            judgementService
        );

        var resolver = new UseCardResolver();

        // Act
        var result = resolver.Resolve(context);

        // Assert
        Assert.IsTrue(result.Success, "Resolution should succeed.");
        Assert.AreEqual(initialDefenderHealth, defender.CurrentHealth, "Defender health should not decrease when Slash is dodged by Bagua Array.");
    }

    #endregion
}
