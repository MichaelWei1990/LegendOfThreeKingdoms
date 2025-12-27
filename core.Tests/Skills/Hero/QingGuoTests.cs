using System;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Response;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Skills.Hero;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ResolutionExtensions = LegendOfThreeKingdoms.Core.Resolution.ResolutionExtensions;

namespace core.Tests;

[TestClass]
public sealed class QingGuoTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Card CreateBlackCard(CardSubType subType = CardSubType.Slash, int id = 1, Suit suit = Suit.Spade)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"black_card_{id}",
            Name = "Black Card",
            CardType = CardType.Basic,
            CardSubType = subType,
            Suit = suit, // Spade or Club (black)
            Rank = 5
        };
    }

    private static Card CreateRedCard(CardSubType subType = CardSubType.Slash, int id = 2)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"red_card_{id}",
            Name = "Red Card",
            CardType = CardType.Basic,
            CardSubType = subType,
            Suit = Suit.Heart, // Heart or Diamond (red)
            Rank = 5
        };
    }

    private static Card CreateDodgeCard(int id = 3)
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

    #region Skill Factory Tests

    /// <summary>
    /// Tests that QingGuoSkillFactory creates correct skill instance.
    /// Input: QingGuoSkillFactory instance.
    /// Expected: Created skill has correct Id, Name, and SkillType.Active.
    /// </summary>
    [TestMethod]
    public void QingGuoSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new QingGuoSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("qingguo", skill.Id);
        Assert.AreEqual("倾国", skill.Name);
        Assert.AreEqual(SkillType.Active, skill.Type);
        Assert.IsTrue(skill is ICardConversionSkill);
    }

    /// <summary>
    /// Tests that SkillRegistry can register and retrieve QingGuo skill.
    /// Input: Empty registry, QingGuoSkillFactory.
    /// Expected: After registration, GetSkill returns a skill with correct Id.
    /// </summary>
    [TestMethod]
    public void SkillRegistryRegisterQingGuoSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new QingGuoSkillFactory();

        // Act
        registry.RegisterSkill("qingguo", factory);
        var skill = registry.GetSkill("qingguo");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("qingguo", skill.Id);
        Assert.AreEqual("倾国", skill.Name);
    }

    #endregion

    #region Card Conversion Tests

    /// <summary>
    /// Tests that QingGuoSkill.CreateVirtualCard creates a virtual Dodge card from a black hand card.
    /// Input: QingGuoSkill, black card (Spade Slash) in hand.
    /// Expected: Returns a virtual card with CardSubType.Dodge, same ID, Suit, and Rank.
    /// </summary>
    [TestMethod]
    public void QingGuoSkillCreateVirtualCardFromBlackHandCard()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        var blackCard = CreateBlackCard(CardSubType.Slash, 1, Suit.Spade);
        ((Zone)player.HandZone).MutableCards.Add(blackCard);
        var skill = new QingGuoSkill();
        skill.Attach(game, player, new BasicEventBus());

        // Act
        var virtualCard = skill.CreateVirtualCard(blackCard, game, player);

        // Assert
        Assert.IsNotNull(virtualCard);
        Assert.AreEqual(blackCard.Id, virtualCard.Id);
        Assert.AreEqual(CardSubType.Dodge, virtualCard.CardSubType);
        Assert.AreEqual(CardType.Basic, virtualCard.CardType);
        Assert.AreEqual(blackCard.Suit, virtualCard.Suit);
        Assert.AreEqual(blackCard.Rank, virtualCard.Rank);
        Assert.AreEqual("闪", virtualCard.Name);
        Assert.AreEqual("dodge", virtualCard.DefinitionId);
    }

    /// <summary>
    /// Tests that QingGuoSkill.CreateVirtualCard creates a virtual Dodge card from a Club card.
    /// Input: QingGuoSkill, Club card in hand.
    /// Expected: Returns a virtual Dodge card.
    /// </summary>
    [TestMethod]
    public void QingGuoSkillCreateVirtualCardFromClubCard()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        var clubCard = CreateBlackCard(CardSubType.Slash, 1, Suit.Club);
        ((Zone)player.HandZone).MutableCards.Add(clubCard);
        var skill = new QingGuoSkill();
        skill.Attach(game, player, new BasicEventBus());

        // Act
        var virtualCard = skill.CreateVirtualCard(clubCard, game, player);

        // Assert
        Assert.IsNotNull(virtualCard);
        Assert.AreEqual(CardSubType.Dodge, virtualCard.CardSubType);
    }

    /// <summary>
    /// Tests that QingGuoSkill.CreateVirtualCard returns null for a red card.
    /// Input: QingGuoSkill, red card (Heart Slash) in hand.
    /// Expected: Returns null.
    /// </summary>
    [TestMethod]
    public void QingGuoSkillCreateVirtualCardFromRedCardReturnsNull()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        var redCard = CreateRedCard(CardSubType.Slash, 2);
        ((Zone)player.HandZone).MutableCards.Add(redCard);
        var skill = new QingGuoSkill();
        skill.Attach(game, player, new BasicEventBus());

        // Act
        var virtualCard = skill.CreateVirtualCard(redCard, game, player);

        // Assert
        Assert.IsNull(virtualCard);
    }

    /// <summary>
    /// Tests that QingGuoSkill.CreateVirtualCard returns null for a black card not in hand.
    /// Input: QingGuoSkill, black card not in hand zone.
    /// Expected: Returns null.
    /// </summary>
    [TestMethod]
    public void QingGuoSkillCreateVirtualCardFromBlackCardNotInHandReturnsNull()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        var blackCard = CreateBlackCard(CardSubType.Slash, 1, Suit.Spade);
        // Card is not added to hand zone
        var skill = new QingGuoSkill();
        skill.Attach(game, player, new BasicEventBus());

        // Act
        var virtualCard = skill.CreateVirtualCard(blackCard, game, player);

        // Assert
        Assert.IsNull(virtualCard);
    }

    #endregion

    #region Response Window Tests

    /// <summary>
    /// Tests that ResponseRuleService includes convertible black cards in legal response cards for Jink response.
    /// Input: Player with QingGuo skill and black card in hand, JinkAgainstSlash response type.
    /// Expected: Black card is included in legal response cards.
    /// </summary>
    [TestMethod]
    public void ResponseRuleServiceIncludesConvertibleBlackCardsForJinkResponse()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var player = game.Players[0];
        var blackCard = CreateBlackCard(CardSubType.Slash, 1, Suit.Spade);
        ((Zone)player.HandZone).MutableCards.Add(blackCard);

        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("qingguo", new QingGuoSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.AddEquipmentSkill(game, player, skillRegistry.GetSkill("qingguo"));

        var responseRuleService = new ResponseRuleService(skillManager);
        var responseContext = new ResponseContext(
            game,
            player,
            ResponseType.JinkAgainstSlash,
            null);

        // Act
        var legalCards = responseRuleService.GetLegalResponseCards(responseContext);

        // Assert
        Assert.IsTrue(legalCards.HasAny);
        Assert.IsTrue(legalCards.Items.Any(c => c.Id == blackCard.Id), "Black card should be in legal response cards");
    }

    /// <summary>
    /// Tests that player can respond to Slash with black card converted to Dodge.
    /// Input: Player with QingGuo skill and black card in hand, Slash is used against them.
    /// Expected: Player can respond with black card, which is converted to virtual Dodge.
    /// </summary>
    [TestMethod]
    public void PlayerCanRespondToSlashWithBlackCardAsDodge()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 0;
        var attacker = game.Players[0];
        var defender = game.Players[1];
        var blackCard = CreateBlackCard(CardSubType.Slash, 1, Suit.Spade);
        ((Zone)defender.HandZone).MutableCards.Add(blackCard);
        var slashCard = CreateTestCard(10, CardSubType.Slash);
        ((Zone)attacker.HandZone).MutableCards.Add(slashCard);

        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("qingguo", new QingGuoSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);
        skillManager.AddEquipmentSkill(game, defender, skillRegistry.GetSkill("qingguo"));

        var cardMoveService = new BasicCardMoveService(eventBus);
        var ruleService = new RuleService(skillManager: skillManager);
        var stack = new BasicResolutionStack();

        // Create Slash event
        var slashEvent = new
        {
            Type = "Slash",
            SourceSeat = attacker.Seat,
            TargetSeat = defender.Seat,
            SlashCard = slashCard
        };

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: defender.Seat,
            SelectedTargetSeats: null,
            SelectedCardIds: new[] { blackCard.Id },
            SelectedOptionId: null,
            Confirmed: null
        );

        var getPlayerChoice = new Func<ChoiceRequest, ChoiceResult>(req =>
        {
            if (req.PlayerSeat == defender.Seat && req.ChoiceType == ChoiceType.SelectCards)
            {
                return choice;
            }
            return new ChoiceResult(req.RequestId, req.PlayerSeat, null, null, null, null);
        });

        var initialDefenderHandCount = defender.HandZone.Cards.Count;
        var initialDiscardPileCount = game.DiscardPile.Cards.Count;

        // Act - Create response window using extension method
        var intermediateResults = new Dictionary<string, object>();
        var resolutionContext = new ResolutionContext(
            game,
            attacker,
            new ActionDescriptor("UseSlash", null, true, new TargetConstraints(1, 1), new[] { slashCard }),
            choice,
            stack,
            cardMoveService,
            ruleService,
            GetPlayerChoice: getPlayerChoice,
            IntermediateResults: intermediateResults,
            SkillManager: skillManager,
            EventBus: eventBus);

        var responseWindowResolver = resolutionContext.CreateJinkResponseWindow(
            defender,
            slashEvent,
            getPlayerChoice);

        var result = responseWindowResolver.Resolve(resolutionContext);

        // Execute the entire stack
        while (!stack.IsEmpty)
        {
            stack.Pop();
        }

        // Assert
        Assert.IsTrue(result.Success, "Response should succeed");
        Assert.AreEqual(initialDefenderHandCount - 1, defender.HandZone.Cards.Count, "Defender should have one less card");
        Assert.AreEqual(initialDiscardPileCount + 1, game.DiscardPile.Cards.Count, "Black card should be in discard pile");
        Assert.IsTrue(game.DiscardPile.Cards.Any(c => c.Id == blackCard.Id), "Black card should be in discard pile");
    }

    private static Card CreateTestCard(int id, CardSubType subType = CardSubType.Slash)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"test_card_{id}",
            CardType = CardType.Basic,
            CardSubType = subType,
            Suit = Suit.Spade,
            Rank = 5
        };
    }

    #endregion
}

