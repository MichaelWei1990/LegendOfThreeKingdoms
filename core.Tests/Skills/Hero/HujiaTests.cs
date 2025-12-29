using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Configuration;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Judgement;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Response;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Skills.Hero;
using LegendOfThreeKingdoms.Core.Skills.Equipment;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests;

[TestClass]
public sealed class HujiaTests
{
    private static Game CreateDefaultGame(int playerCount = 3)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Game CreateGameWithFactions(int playerCount, Dictionary<int, string> factionMap)
    {
        var baseConfig = CoreApi.CreateDefaultConfig(playerCount);
        var playerConfigs = new List<PlayerConfig>();
        
        for (int i = 0; i < playerCount; i++)
        {
            var playerConfig = new PlayerConfig
            {
                Seat = i,
                MaxHealth = 4,
                InitialHealth = 4,
                FactionId = factionMap.TryGetValue(i, out var faction) ? faction : null
            };
            playerConfigs.Add(playerConfig);
        }

        var config = new GameConfig
        {
            PlayerConfigs = playerConfigs,
            DeckConfig = baseConfig.DeckConfig,
            Seed = baseConfig.Seed,
            GameModeId = baseConfig.GameModeId,
            GameVariantOptions = baseConfig.GameVariantOptions
        };

        return Game.FromConfig(config);
    }

    private static Card CreateDodgeCard(int id = 1)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "dodge",
            Name = "闪",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Dodge,
            Suit = Suit.Heart,
            Rank = 5
        };
    }

    private static Card CreateSlashCard(int id = 10)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "slash",
            Name = "杀",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = Suit.Spade,
            Rank = 5
        };
    }

    private static Card CreateBlackCard(int id = 20, Suit suit = Suit.Spade)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"black_card_{id}",
            Name = "Black Card",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = suit, // Spade or Club (black)
            Rank = 5
        };
    }

    #region Skill Factory Tests

    /// <summary>
    /// Tests that HujiaSkillFactory creates correct skill instance.
    /// </summary>
    [TestMethod]
    public void HujiaSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new HujiaSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("hujia", skill.Id);
        Assert.AreEqual("护驾", skill.Name);
        Assert.AreEqual(SkillType.Trigger, skill.Type);
        Assert.IsTrue(skill is IResponseAssistanceSkill);
    }

    /// <summary>
    /// Tests that SkillRegistry can register and retrieve Hujia skill.
    /// </summary>
    [TestMethod]
    public void SkillRegistryRegisterHujiaSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new HujiaSkillFactory();

        // Act
        registry.RegisterSkill("hujia", factory);
        var skill = registry.GetSkill("hujia");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("hujia", skill.Id);
        Assert.AreEqual("护驾", skill.Name);
    }

    #endregion

    #region Skill Properties Tests

    /// <summary>
    /// Tests that HujiaSkill has correct properties.
    /// </summary>
    [TestMethod]
    public void HujiaSkill_HasCorrectProperties()
    {
        // Arrange
        var skill = new HujiaSkill();

        // Assert
        Assert.AreEqual("hujia", skill.Id);
        Assert.AreEqual("护驾", skill.Name);
        Assert.AreEqual(SkillType.Trigger, skill.Type);
        Assert.AreEqual(SkillCapability.None, skill.Capabilities);
    }

    #endregion

    #region CanProvideAssistance Tests

    /// <summary>
    /// Tests that HujiaSkill can provide assistance when owner is Lord and has Wei faction assistants.
    /// </summary>
    [TestMethod]
    public void HujiaSkill_CanProvideAssistance_WhenOwnerIsLordAndHasWeiAssistants()
    {
        // Arrange
        var factionMap = new Dictionary<int, string>
        {
            { 0, null }, // Owner (no faction needed for test)
            { 1, "wei" }, // Assistant
            { 2, "shu" }  // Other player
        };
        var game = CreateGameWithFactions(3, factionMap);
        var owner = game.Players[0];
        owner.Flags["IsLord"] = true;

        var skill = new HujiaSkill();

        // Act
        var canProvide = skill.CanProvideAssistance(
            game,
            owner,
            ResponseType.JinkAgainstSlash,
            null);

        // Assert
        Assert.IsTrue(canProvide);
    }

    /// <summary>
    /// Tests that HujiaSkill cannot provide assistance when owner is not Lord.
    /// </summary>
    [TestMethod]
    public void HujiaSkill_CannotProvideAssistance_WhenOwnerIsNotLord()
    {
        // Arrange
        var factionMap = new Dictionary<int, string>
        {
            { 0, null }, // Owner (not Lord)
            { 1, "wei" }, // Assistant
            { 2, "shu" }  // Other player
        };
        var game = CreateGameWithFactions(3, factionMap);
        var owner = game.Players[0];
        // Owner is not Lord

        var skill = new HujiaSkill();

        // Act
        var canProvide = skill.CanProvideAssistance(
            game,
            owner,
            ResponseType.JinkAgainstSlash,
            null);

        // Assert
        Assert.IsFalse(canProvide);
    }

    /// <summary>
    /// Tests that HujiaSkill cannot provide assistance when there are no Wei faction assistants.
    /// </summary>
    [TestMethod]
    public void HujiaSkill_CannotProvideAssistance_WhenNoWeiAssistants()
    {
        // Arrange
        var factionMap = new Dictionary<int, string>
        {
            { 0, null }, // Owner (Lord)
            { 1, "shu" }, // Not Wei
            { 2, "wu" }   // Not Wei
        };
        var game = CreateGameWithFactions(3, factionMap);
        var owner = game.Players[0];
        owner.Flags["IsLord"] = true;

        var skill = new HujiaSkill();

        // Act
        var canProvide = skill.CanProvideAssistance(
            game,
            owner,
            ResponseType.JinkAgainstSlash,
            null);

        // Assert
        Assert.IsFalse(canProvide);
    }

    /// <summary>
    /// Tests that HujiaSkill only works for Dodge response types.
    /// </summary>
    [TestMethod]
    public void HujiaSkill_OnlyWorksForDodgeResponseTypes()
    {
        // Arrange
        var factionMap = new Dictionary<int, string>
        {
            { 0, null }, // Owner (Lord)
            { 1, "wei" }, // Assistant
            { 2, "shu" }  // Other player
        };
        var game = CreateGameWithFactions(3, factionMap);
        var owner = game.Players[0];
        owner.Flags["IsLord"] = true;

        var skill = new HujiaSkill();

        // Act & Assert
        Assert.IsTrue(skill.CanProvideAssistance(game, owner, ResponseType.JinkAgainstSlash, null));
        Assert.IsTrue(skill.CanProvideAssistance(game, owner, ResponseType.JinkAgainstWanjianqifa, null));
        Assert.IsFalse(skill.CanProvideAssistance(game, owner, ResponseType.PeachForDying, null));
        Assert.IsFalse(skill.CanProvideAssistance(game, owner, ResponseType.SlashAgainstDuel, null));
    }

    #endregion

    #region GetAssistants Tests

    /// <summary>
    /// Tests that HujiaSkill returns Wei faction assistants in seat order.
    /// </summary>
    [TestMethod]
    public void HujiaSkill_GetAssistants_ReturnsWeiFactionPlayersInSeatOrder()
    {
        // Arrange
        var factionMap = new Dictionary<int, string>
        {
            { 0, "wei" }, // Owner is also Wei, but should be excluded
            { 1, "wei" }, // Assistant
            { 2, "shu" }, // Not Wei
            { 3, "wei" }  // Assistant
        };
        var game = CreateGameWithFactions(4, factionMap);
        var owner = game.Players[0];

        var skill = new HujiaSkill();

        // Act
        var assistants = skill.GetAssistants(game, owner);

        // Assert
        Assert.AreEqual(2, assistants.Count);
        Assert.AreEqual(game.Players[1].Seat, assistants[0].Seat);
        Assert.AreEqual(game.Players[3].Seat, assistants[1].Seat);
    }

    /// <summary>
    /// Tests that HujiaSkill excludes dead players from assistants.
    /// </summary>
    [TestMethod]
    public void HujiaSkill_GetAssistants_ExcludesDeadPlayers()
    {
        // Arrange
        var factionMap = new Dictionary<int, string>
        {
            { 0, null }, // Owner
            { 1, "wei" }, // Assistant (will be dead)
            { 2, "shu" }  // Other player
        };
        var game = CreateGameWithFactions(3, factionMap);
        var owner = game.Players[0];

        var assistant = game.Players[1];
        assistant.IsAlive = false; // Dead assistant

        var skill = new HujiaSkill();

        // Act
        var assistants = skill.GetAssistants(game, owner);

        // Assert
        Assert.AreEqual(0, assistants.Count);
    }

    #endregion

    #region ShouldActivate Tests

    /// <summary>
    /// Tests that HujiaSkill asks owner if they want to activate when conditions are met.
    /// </summary>
    [TestMethod]
    public void HujiaSkill_ShouldActivate_AsksOwnerWhenConditionsMet()
    {
        // Arrange
        var factionMap = new Dictionary<int, string>
        {
            { 0, null }, // Owner (Lord)
            { 1, "wei" }, // Assistant
            { 2, "shu" }  // Other player
        };
        var game = CreateGameWithFactions(3, factionMap);
        var owner = game.Players[0];
        owner.Flags["IsLord"] = true;

        var skill = new HujiaSkill();

        bool activationAsked = false;
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            activationAsked = true;
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: true
            );
        };

        // Act
        var shouldActivate = skill.ShouldActivate(game, owner, getPlayerChoice);

        // Assert
        Assert.IsTrue(activationAsked);
        Assert.IsTrue(shouldActivate);
    }

    /// <summary>
    /// Tests that HujiaSkill does not ask owner when conditions are not met.
    /// </summary>
    [TestMethod]
    public void HujiaSkill_ShouldActivate_DoesNotAskWhenConditionsNotMet()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        var owner = game.Players[0];
        // Owner is not Lord

        var skill = new HujiaSkill();

        bool activationAsked = false;
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            activationAsked = true;
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: false
            );
        };

        // Act
        var shouldActivate = skill.ShouldActivate(game, owner, getPlayerChoice);

        // Assert
        Assert.IsFalse(activationAsked);
        Assert.IsFalse(shouldActivate);
    }

    #endregion

    #region Integration Tests

    /// <summary>
    /// Tests that HujiaSkill returns Wei faction assistants in seat order.
    /// This is a simplified integration test that verifies the skill structure.
    /// </summary>
    [TestMethod]
    public void HujiaSkill_GetAssistants_ReturnsInSeatOrder()
    {
        // Arrange
        var factionMap = new Dictionary<int, string>
        {
            { 0, null }, // Beneficiary (Lord)
            { 1, "wei" }, // Assistant
            { 2, "wei" }, // Assistant
            { 3, "shu" }  // Not Wei
        };
        var game = CreateGameWithFactions(4, factionMap);
        var beneficiary = game.Players[0];
        beneficiary.Flags["IsLord"] = true;

        var skill = new HujiaSkill();
        var assistants = skill.GetAssistants(game, beneficiary);

        // Assert
        Assert.AreEqual(2, assistants.Count);
        Assert.AreEqual(game.Players[1].Seat, assistants[0].Seat);
        Assert.AreEqual(game.Players[2].Seat, assistants[1].Seat);
    }

    /// <summary>
    /// Tests that assistant with Bagua Array can use it to respond during Hujia assistance.
    /// Input: Lord is attacked by Slash, uses Hujia, assistant has Bagua Array but no Dodge, red card in draw pile.
    /// Expected: Assistant can use Bagua Array to successfully respond, Slash is dodged.
    /// </summary>
    [TestMethod]
    public void ResponseAssistanceResolver_AssistantWithBaguaArray_CanUseBaguaArrayToRespond()
    {
        // Arrange
        var factionMap = new Dictionary<int, string>
        {
            { 0, null }, // Beneficiary (Lord)
            { 1, "wei" }, // Assistant with Bagua Array
            { 2, "shu" }  // Attacker
        };
        var game = CreateGameWithFactions(3, factionMap);
        var beneficiary = game.Players[0];
        var assistant = game.Players[1];
        var attacker = game.Players[2];
        
        beneficiary.Flags["IsLord"] = true;
        beneficiary.IsAlive = true;
        assistant.IsAlive = true;
        attacker.IsAlive = true;

        // Add red card to draw pile for Bagua Array judgement
        if (game.DrawPile is Zone drawZone)
        {
            var redCard = new Card
            {
                Id = 100,
                DefinitionId = "red_card",
                Name = "Red Card",
                CardType = CardType.Basic,
                CardSubType = CardSubType.Slash,
                Suit = Suit.Heart, // Red suit
                Rank = 5
            };
            drawZone.MutableCards.Add(redCard);
        }

        // Setup skill system
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("hujia", new HujiaSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.LoadSkillsForPlayer(game, beneficiary);
        skillManager.LoadSkillsForPlayer(game, assistant);
        skillManager.LoadSkillsForPlayer(game, attacker);

        // Add Bagua Array to assistant
        var baguaArraySkill = new BaguaFormation();
        skillManager.AddEquipmentSkill(game, assistant, baguaArraySkill);

        // Create Slash card
        var slashCard = CreateSlashCard(10);
        if (attacker.HandZone is Zone attackerHand)
        {
            attackerHand.MutableCards.Add(slashCard);
        }

        // Create source event
        var sourceEvent = new { Type = "Slash", SourceSeat = attacker.Seat, TargetSeat = beneficiary.Seat, SlashCard = slashCard };

        // Setup services
        var cardMoveService = new BasicCardMoveService(eventBus);
        var ruleService = new RuleService(skillManager: skillManager);
        var judgementService = new BasicJudgementService(eventBus);
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("bagua_array", new BaguaFormationFactory());

        // Create resolution stack
        var stack = new BasicResolutionStack();
        var intermediateResults = new Dictionary<string, object>();

        // Mock getPlayerChoice: 
        // 1. Beneficiary chooses to use Hujia
        // 2. Assistant chooses to help
        // 3. Assistant chooses to use Bagua Array (no Dodge card)
        // 4. Assistant confirms Bagua Array activation
        var choiceSequence = new Queue<ChoiceResult>();
        
        // Beneficiary uses Hujia
        choiceSequence.Enqueue(new ChoiceResult(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: beneficiary.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: true
        ));
        
        // Assistant chooses to help
        choiceSequence.Enqueue(new ChoiceResult(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: assistant.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: true
        ));
        
        // Assistant chooses to use Bagua Array (no card selected, meaning no Dodge)
        choiceSequence.Enqueue(new ChoiceResult(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: assistant.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: null
        ));
        
        // Assistant confirms Bagua Array activation
        choiceSequence.Enqueue(new ChoiceResult(
            RequestId: Guid.NewGuid().ToString(),
            PlayerSeat: assistant.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: null,
            SelectedOptionId: null,
            Confirmed: true
        ));

        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            if (choiceSequence.Count > 0)
            {
                return choiceSequence.Dequeue();
            }
            // Default: pass
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        // Create Hujia skill instance
        var hujiaSkill = new HujiaSkill();

        // Create ResponseAssistanceResolver
        var resolver = new ResponseAssistanceResolver(
            beneficiary: beneficiary,
            responseType: ResponseType.JinkAgainstSlash,
            sourceEvent: sourceEvent,
            assistanceSkill: hujiaSkill
        );

        // Create resolution context
        var context = new ResolutionContext(
            game,
            beneficiary,
            Action: null,
            Choice: null,
            stack,
            cardMoveService,
            ruleService,
            PendingDamage: null,
            LogSink: null,
            getPlayerChoice,
            intermediateResults,
            eventBus,
            LogCollector: null,
            skillManager,
            equipmentSkillRegistry,
            judgementService
        );

        // Act: Execute resolver
        var result = resolver.Resolve(context);

        // Assert: Resolver should succeed
        Assert.IsTrue(result.Success, "ResponseAssistanceResolver should succeed.");

        // Execute the stack to process the response window
        while (!stack.IsEmpty)
        {
            var resolverResult = stack.Pop();
            if (!resolverResult.Success)
            {
                break;
            }
        }

        // Assert: Response should be successful (Bagua Array judgement succeeded)
        Assert.IsTrue(
            intermediateResults.TryGetValue("LastResponseResult", out var responseResultObj),
            "Response result should be stored in IntermediateResults."
        );

        if (responseResultObj is ResponseWindowResult responseResult)
        {
            Assert.AreEqual(
                ResponseWindowState.ResponseSuccess,
                responseResult.State,
                "Response should be successful when Bagua Array judgement succeeds."
            );
        }
    }

    #endregion
}

