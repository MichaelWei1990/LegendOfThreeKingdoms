using System;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Phases;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Skills.Hero;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests;

[TestClass]
public sealed class LuoYiTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Game CreateGameWithCardsInDrawPile(int playerCount = 2, int cardCount = 10)
    {
        var game = CreateDefaultGame(playerCount);

        // Add cards to draw pile
        if (game.DrawPile is Zone drawZone)
        {
            for (int i = 0; i < cardCount; i++)
            {
                var card = new Card
                {
                    Id = i + 1,
                    DefinitionId = $"test_card_{i}",
                    CardType = CardType.Basic,
                    CardSubType = CardSubType.Slash,
                    Suit = Suit.Spade,
                    Rank = 5
                };
                drawZone.MutableCards.Add(card);
            }
        }

        return game;
    }

    private static Card CreateTestCard(int id, Suit suit, int rank)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"card_{id}",
            Name = $"Card {id}",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = suit,
            Rank = rank
        };
    }

    #region Skill Factory Tests

    [TestMethod]
    public void LuoYiSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new LuoYiSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("luoyi", skill.Id);
        Assert.AreEqual("裸衣", skill.Name);
        Assert.AreEqual(SkillType.Trigger, skill.Type);
    }

    [TestMethod]
    public void SkillRegistryRegisterLuoYiSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new LuoYiSkillFactory();

        // Act
        registry.RegisterSkill("luoyi", factory);
        var skill = registry.GetSkill("luoyi");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("luoyi", skill.Id);
        Assert.AreEqual("裸衣", skill.Name);
    }

    #endregion

    #region Skill Properties Tests

    [TestMethod]
    public void LuoYiSkillHasCorrectProperties()
    {
        // Arrange
        var skill = new LuoYiSkill();

        // Act & Assert
        Assert.AreEqual("luoyi", skill.Id);
        Assert.AreEqual("裸衣", skill.Name);
        Assert.AreEqual(SkillType.Trigger, skill.Type);
        Assert.AreEqual(SkillCapability.None, skill.Capabilities);
    }

    #endregion

    #region Draw Phase Modification Tests

    [TestMethod]
    public void LuoYiSkillReducesDrawCountByOneWhenActivated()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(2, 10);
        var luoyiPlayer = game.Players[0];
        luoyiPlayer.IsAlive = true;

        var initialHandCount = luoyiPlayer.HandZone.Cards.Count;

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var eventBus = new BasicEventBus();
        var stack = new BasicResolutionStack();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.AddEquipmentSkill(game, luoyiPlayer, new LuoYiSkill());

        // Setup getPlayerChoice to confirm activation
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = request =>
        {
            if (request.ChoiceType == ChoiceType.Confirm && request.PlayerSeat == luoyiPlayer.Seat)
            {
                return new ChoiceResult(request.RequestId, request.PlayerSeat, null, null, null, true);
            }
            return new ChoiceResult(request.RequestId, request.PlayerSeat, null, null, null, null);
        };

        var drawPhaseService = new DrawPhaseService(cardMoveService, ruleService, eventBus, stack, skillManager, getPlayerChoice);

        // Act - Trigger draw phase
        var phaseStartEvent = new PhaseStartEvent(game, luoyiPlayer.Seat, Phase.Draw);
        eventBus.Publish(phaseStartEvent);

        // Execute resolution stack
        while (!stack.IsEmpty)
        {
            var result = stack.Pop();
            Assert.IsTrue(result.Success, "Resolution should succeed.");
        }

        // Assert - Should have drawn 1 card (2 base - 1 from LuoYi = 1)
        Assert.AreEqual(initialHandCount + 1, luoyiPlayer.HandZone.Cards.Count, "Player should have drawn 1 card (2 - 1).");
    }

    [TestMethod]
    public void LuoYiSkillDoesNotModifyDrawCountWhenNotActivated()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(2, 10);
        var luoyiPlayer = game.Players[0];
        luoyiPlayer.IsAlive = true;

        var initialHandCount = luoyiPlayer.HandZone.Cards.Count;

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var eventBus = new BasicEventBus();
        var stack = new BasicResolutionStack();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.AddEquipmentSkill(game, luoyiPlayer, new LuoYiSkill());

        // Setup getPlayerChoice to decline activation
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = request =>
        {
            if (request.ChoiceType == ChoiceType.Confirm && request.PlayerSeat == luoyiPlayer.Seat)
            {
                return new ChoiceResult(request.RequestId, request.PlayerSeat, null, null, null, false);
            }
            return new ChoiceResult(request.RequestId, request.PlayerSeat, null, null, null, null);
        };

        var drawPhaseService = new DrawPhaseService(cardMoveService, ruleService, eventBus, stack, skillManager, getPlayerChoice);

        // Act - Trigger draw phase
        var phaseStartEvent = new PhaseStartEvent(game, luoyiPlayer.Seat, Phase.Draw);
        eventBus.Publish(phaseStartEvent);

        // Execute resolution stack
        while (!stack.IsEmpty)
        {
            var result = stack.Pop();
            Assert.IsTrue(result.Success, "Resolution should succeed.");
        }

        // Assert - Should have drawn 2 cards normally
        Assert.AreEqual(initialHandCount + 2, luoyiPlayer.HandZone.Cards.Count, "Player should have drawn 2 cards normally.");
    }

    #endregion

    #region Damage Modification Tests

    [TestMethod]
    public void LuoYiSkillIncreasesSlashDamageByOne()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(2, 10);
        var luoyiPlayer = game.Players[0];
        var target = game.Players[1];
        luoyiPlayer.IsAlive = true;
        target.IsAlive = true;
        target.CurrentHealth = 3;

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.AddEquipmentSkill(game, luoyiPlayer, new LuoYiSkill());

        // Activate LuoYi by simulating draw phase activation
        var luoyiSkill = new LuoYiSkill();
        luoyiSkill.Attach(game, luoyiPlayer, eventBus);
        luoyiSkill.OnDrawPhaseModified(game, luoyiPlayer, eventBus);

        // Create damage descriptor for Slash
        var damage = new DamageDescriptor(
            SourceSeat: luoyiPlayer.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash"
        );

        // Act - Publish BeforeDamageEvent
        var beforeDamageEvent = new BeforeDamageEvent(game, damage);
        eventBus.Publish(beforeDamageEvent);

        // Assert - Damage should be increased by 1
        Assert.AreEqual(1, beforeDamageEvent.DamageModification, "Damage should be increased by 1.");
        
        // Verify damage is applied correctly
        var stack = new BasicResolutionStack();
        var context = new ResolutionContext(
            game,
            luoyiPlayer,
            Action: null,
            Choice: null,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: damage,
            LogSink: null,
            GetPlayerChoice: null,
            IntermediateResults: null,
            EventBus: eventBus,
            LogCollector: null,
            SkillManager: skillManager,
            EquipmentSkillRegistry: null,
            JudgementService: null
        );

        stack.Push(new DamageResolver(), context);
        while (!stack.IsEmpty)
        {
            var result = stack.Pop();
            Assert.IsTrue(result.Success, "Damage resolution should succeed.");
        }

        // Target should have lost 2 HP (1 base + 1 from LuoYi)
        Assert.AreEqual(1, target.CurrentHealth, "Target should have lost 2 HP (1 base + 1 from LuoYi).");
    }

    [TestMethod]
    public void LuoYiSkillIncreasesDuelDamageByOne()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(2, 10);
        var luoyiPlayer = game.Players[0];
        var target = game.Players[1];
        luoyiPlayer.IsAlive = true;
        target.IsAlive = true;
        target.CurrentHealth = 3;

        var cardMoveService = new BasicCardMoveService();
        var ruleService = new RuleService();
        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.AddEquipmentSkill(game, luoyiPlayer, new LuoYiSkill());

        // Activate LuoYi
        var luoyiSkill = new LuoYiSkill();
        luoyiSkill.Attach(game, luoyiPlayer, eventBus);
        luoyiSkill.OnDrawPhaseModified(game, luoyiPlayer, eventBus);

        // Create damage descriptor for Duel
        var damage = new DamageDescriptor(
            SourceSeat: luoyiPlayer.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Duel"
        );

        // Act - Publish BeforeDamageEvent
        var beforeDamageEvent = new BeforeDamageEvent(game, damage);
        eventBus.Publish(beforeDamageEvent);

        // Assert - Damage should be increased by 1
        Assert.AreEqual(1, beforeDamageEvent.DamageModification, "Damage should be increased by 1.");
    }

    [TestMethod]
    public void LuoYiSkillDoesNotIncreaseNonSlashOrDuelDamage()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(2, 10);
        var luoyiPlayer = game.Players[0];
        var target = game.Players[1];
        luoyiPlayer.IsAlive = true;
        target.IsAlive = true;

        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.AddEquipmentSkill(game, luoyiPlayer, new LuoYiSkill());

        // Activate LuoYi
        var luoyiSkill = new LuoYiSkill();
        luoyiSkill.Attach(game, luoyiPlayer, eventBus);
        luoyiSkill.OnDrawPhaseModified(game, luoyiPlayer, eventBus);

        // Create damage descriptor for other reason (e.g., Nanman Rushin)
        var damage = new DamageDescriptor(
            SourceSeat: luoyiPlayer.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "NanmanRushin"
        );

        // Act - Publish BeforeDamageEvent
        var beforeDamageEvent = new BeforeDamageEvent(game, damage);
        eventBus.Publish(beforeDamageEvent);

        // Assert - Damage should not be modified
        Assert.AreEqual(0, beforeDamageEvent.DamageModification, "Damage should not be modified for non-Slash/Duel damage.");
    }

    [TestMethod]
    public void LuoYiSkillDoesNotIncreaseDamageWhenNotActiveThisTurn()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(2, 10);
        var luoyiPlayer = game.Players[0];
        var target = game.Players[1];
        luoyiPlayer.IsAlive = true;
        target.IsAlive = true;

        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.AddEquipmentSkill(game, luoyiPlayer, new LuoYiSkill());

        // Attach skill but don't activate it (don't call OnDrawPhaseModified)
        var luoyiSkill = new LuoYiSkill();
        luoyiSkill.Attach(game, luoyiPlayer, eventBus);

        // Create damage descriptor for Slash
        var damage = new DamageDescriptor(
            SourceSeat: luoyiPlayer.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash"
        );

        // Act - Publish BeforeDamageEvent
        var beforeDamageEvent = new BeforeDamageEvent(game, damage);
        eventBus.Publish(beforeDamageEvent);

        // Assert - Damage should not be modified
        Assert.AreEqual(0, beforeDamageEvent.DamageModification, "Damage should not be modified when LuoYi is not active this turn.");
    }

    [TestMethod]
    public void LuoYiSkillResetsAtTurnEnd()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(2, 10);
        var luoyiPlayer = game.Players[0];
        var target = game.Players[1];
        luoyiPlayer.IsAlive = true;
        target.IsAlive = true;

        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.AddEquipmentSkill(game, luoyiPlayer, new LuoYiSkill());

        // Activate LuoYi
        var luoyiSkill = new LuoYiSkill();
        luoyiSkill.Attach(game, luoyiPlayer, eventBus);
        luoyiSkill.OnDrawPhaseModified(game, luoyiPlayer, eventBus);

        // Verify it's active
        var damage1 = new DamageDescriptor(
            SourceSeat: luoyiPlayer.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash"
        );
        var beforeDamageEvent1 = new BeforeDamageEvent(game, damage1);
        eventBus.Publish(beforeDamageEvent1);
        Assert.AreEqual(1, beforeDamageEvent1.DamageModification, "LuoYi should be active before turn end.");

        // Act - Publish TurnEndEvent
        var turnEndEvent = new TurnEndEvent(game, luoyiPlayer.Seat, 1);
        eventBus.Publish(turnEndEvent);

        // Assert - LuoYi should be inactive after turn end
        var damage2 = new DamageDescriptor(
            SourceSeat: luoyiPlayer.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash"
        );
        var beforeDamageEvent2 = new BeforeDamageEvent(game, damage2);
        eventBus.Publish(beforeDamageEvent2);
        Assert.AreEqual(0, beforeDamageEvent2.DamageModification, "LuoYi should be inactive after turn end.");
    }

    #endregion

    #region IDamageModifyingSkill Tests

    [TestMethod]
    public void LuoYiSkillModifyDamageIncreasesSlashDamageByOne()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(2, 10);
        var luoyiPlayer = game.Players[0];
        var target = game.Players[1];
        luoyiPlayer.IsAlive = true;
        target.IsAlive = true;

        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.AddEquipmentSkill(game, luoyiPlayer, new LuoYiSkill());

        // Activate LuoYi
        var luoyiSkill = new LuoYiSkill();
        luoyiSkill.Attach(game, luoyiPlayer, eventBus);
        luoyiSkill.OnDrawPhaseModified(game, luoyiPlayer, eventBus);

        // Create damage descriptor for Slash
        var damage = new DamageDescriptor(
            SourceSeat: luoyiPlayer.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash"
        );

        // Act - Call ModifyDamage directly
        var modification = luoyiSkill.ModifyDamage(damage, game, luoyiPlayer);

        // Assert - Should return +1
        Assert.AreEqual(1, modification, "ModifyDamage should return +1 for Slash damage when active.");
    }

    [TestMethod]
    public void LuoYiSkillModifyDamageIncreasesDuelDamageByOne()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(2, 10);
        var luoyiPlayer = game.Players[0];
        var target = game.Players[1];
        luoyiPlayer.IsAlive = true;
        target.IsAlive = true;

        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.AddEquipmentSkill(game, luoyiPlayer, new LuoYiSkill());

        // Activate LuoYi
        var luoyiSkill = new LuoYiSkill();
        luoyiSkill.Attach(game, luoyiPlayer, eventBus);
        luoyiSkill.OnDrawPhaseModified(game, luoyiPlayer, eventBus);

        // Create damage descriptor for Duel
        var damage = new DamageDescriptor(
            SourceSeat: luoyiPlayer.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Duel"
        );

        // Act - Call ModifyDamage directly
        var modification = luoyiSkill.ModifyDamage(damage, game, luoyiPlayer);

        // Assert - Should return +1
        Assert.AreEqual(1, modification, "ModifyDamage should return +1 for Duel damage when active.");
    }

    [TestMethod]
    public void LuoYiSkillModifyDamageReturnsZeroForNonSlashOrDuelDamage()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(2, 10);
        var luoyiPlayer = game.Players[0];
        var target = game.Players[1];
        luoyiPlayer.IsAlive = true;
        target.IsAlive = true;

        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.AddEquipmentSkill(game, luoyiPlayer, new LuoYiSkill());

        // Activate LuoYi
        var luoyiSkill = new LuoYiSkill();
        luoyiSkill.Attach(game, luoyiPlayer, eventBus);
        luoyiSkill.OnDrawPhaseModified(game, luoyiPlayer, eventBus);

        // Create damage descriptor for other reason
        var damage = new DamageDescriptor(
            SourceSeat: luoyiPlayer.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "NanmanRushin"
        );

        // Act - Call ModifyDamage directly
        var modification = luoyiSkill.ModifyDamage(damage, game, luoyiPlayer);

        // Assert - Should return 0
        Assert.AreEqual(0, modification, "ModifyDamage should return 0 for non-Slash/Duel damage.");
    }

    [TestMethod]
    public void LuoYiSkillModifyDamageReturnsZeroWhenNotActiveThisTurn()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(2, 10);
        var luoyiPlayer = game.Players[0];
        var target = game.Players[1];
        luoyiPlayer.IsAlive = true;
        target.IsAlive = true;

        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.AddEquipmentSkill(game, luoyiPlayer, new LuoYiSkill());

        // Attach skill but don't activate it
        var luoyiSkill = new LuoYiSkill();
        luoyiSkill.Attach(game, luoyiPlayer, eventBus);

        // Create damage descriptor for Slash
        var damage = new DamageDescriptor(
            SourceSeat: luoyiPlayer.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash"
        );

        // Act - Call ModifyDamage directly
        var modification = luoyiSkill.ModifyDamage(damage, game, luoyiPlayer);

        // Assert - Should return 0
        Assert.AreEqual(0, modification, "ModifyDamage should return 0 when LuoYi is not active this turn.");
    }

    [TestMethod]
    public void LuoYiSkillModifyDamageReturnsZeroForOtherPlayerDamage()
    {
        // Arrange
        var game = CreateGameWithCardsInDrawPile(2, 10);
        var luoyiPlayer = game.Players[0];
        var otherPlayer = game.Players[1];
        var target = game.Players[0];
        luoyiPlayer.IsAlive = true;
        otherPlayer.IsAlive = true;
        target.IsAlive = true;

        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);
        skillManager.AddEquipmentSkill(game, luoyiPlayer, new LuoYiSkill());

        // Activate LuoYi
        var luoyiSkill = new LuoYiSkill();
        luoyiSkill.Attach(game, luoyiPlayer, eventBus);
        luoyiSkill.OnDrawPhaseModified(game, luoyiPlayer, eventBus);

        // Create damage descriptor where other player is the source
        var damage = new DamageDescriptor(
            SourceSeat: otherPlayer.Seat,
            TargetSeat: target.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash"
        );

        // Act - Call ModifyDamage directly
        var modification = luoyiSkill.ModifyDamage(damage, game, luoyiPlayer);

        // Assert - Should return 0 (damage source is not the skill owner)
        Assert.AreEqual(0, modification, "ModifyDamage should return 0 when damage source is not the skill owner.");
    }

    [TestMethod]
    public void LuoYiSkillImplementsIDamageModifyingSkill()
    {
        // Arrange
        var skill = new LuoYiSkill();

        // Act & Assert
        Assert.IsTrue(skill is IDamageModifyingSkill, "LuoYiSkill should implement IDamageModifyingSkill interface.");
    }

    #endregion
}

