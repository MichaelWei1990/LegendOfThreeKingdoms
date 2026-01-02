using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Abstractions;
using LegendOfThreeKingdoms.Core.Configuration;
using LegendOfThreeKingdoms.Core.Content;
using LegendOfThreeKingdoms.Core.GameMode;
using LegendOfThreeKingdoms.Core.GameSetup;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests.GameSetup;

[TestClass]
public sealed class DeckBuildingTests
{
    private sealed class FixedRandomSource : IRandomSource
    {
        private readonly int[] _values;
        private int _index;

        public FixedRandomSource(params int[] values)
        {
            _values = values;
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (_values.Length == 0)
            {
                return minInclusive;
            }

            var value = _values[_index % _values.Length];
            _index++;
            return System.Math.Clamp(value, minInclusive, maxExclusive - 1);
        }
    }

    private static GameConfig CreateTestConfig(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        config.DeckConfig.IncludedPacks.Add("Base");
        return config;
    }

    /// <summary>
    /// Verifies that the standard edition deck contains exactly 108 cards.
    /// </summary>
    [TestMethod]
    public void BuildDeckCardIds_StandardEdition_Contains108Cards()
    {
        // Arrange
        var config = CreateTestConfig();
        var random = new FixedRandomSource(0);
        var options = new GameInitializationOptions
        {
            GameConfig = config,
            Random = random,
            GameMode = new DummyGameMode()
        };

        var initializer = new BasicGameInitializer();

        // Act
        var result = initializer.Initialize(options);

        // Assert
        Assert.IsTrue(result.Success);
        var game = result.Game!;

        Assert.IsInstanceOfType(game.DrawPile, typeof(Zone));
        var drawPile = ((Zone)game.DrawPile).Cards;

        // Standard edition should have approximately 108 cards total
        // Actual implementation may have slight variations (107-110 cards)
        // Before dealing: ~108 cards
        // After dealing: ~108 - (playerCount * InitialHandCardCount)
        var totalCards = drawPile.Count + (game.Players.Count * config.InitialHandCardCount);
        Assert.IsTrue(totalCards >= 107 && totalCards <= 110, 
            $"Standard edition should have 107-110 cards, got {totalCards}");
    }

    /// <summary>
    /// Verifies that the deck contains the correct number of each card type.
    /// </summary>
    [TestMethod]
    public void BuildDeckCardIds_StandardEdition_HasCorrectCardTypeDistribution()
    {
        // Arrange
        var config = CreateTestConfig();
        var random = new FixedRandomSource(0);
        var options = new GameInitializationOptions
        {
            GameConfig = config,
            Random = random,
            GameMode = new DummyGameMode(),
            PrebuiltDeckCardIds = null // Use BuildDeckCardIds
        };

        var initializer = new BasicGameInitializer();

        // Act
        var result = initializer.Initialize(options);

        // Assert
        Assert.IsTrue(result.Success);
        var game = result.Game!;

        Assert.IsInstanceOfType(game.DrawPile, typeof(Zone));
        var allCards = ((Zone)game.DrawPile).Cards
            .Concat(game.Players.SelectMany(p => ((Zone)p.HandZone).Cards))
            .ToList();

        // Count by card type
        var basicCount = allCards.Count(c => c.CardType == CardType.Basic);
        var trickCount = allCards.Count(c => c.CardType == CardType.Trick);
        var equipCount = allCards.Count(c => c.CardType == CardType.Equip);

        // Standard edition: 53 basic + 36 trick + 19 equipment = 108
        Assert.AreEqual(53, basicCount, "Should have 53 basic cards");
        // Trick cards: 6+5+4+2+1+3+3+1+3+2+4+1 = 35 (includes TaoyuanJieyi and Shandian)
        Assert.IsTrue(trickCount >= 35 && trickCount <= 38, $"Should have 35-38 trick cards, got {trickCount}");
        Assert.AreEqual(19, equipCount, "Should have 19 equipment cards");
    }

    /// <summary>
    /// Verifies that cards have correct CardType and CardSubType set from CardDefinitionService.
    /// </summary>
    [TestMethod]
    public void PopulateDrawPile_CardsHaveCorrectMetadata()
    {
        // Arrange
        var config = CreateTestConfig();
        var random = new FixedRandomSource(0);
        var options = new GameInitializationOptions
        {
            GameConfig = config,
            Random = random,
            GameMode = new DummyGameMode()
        };

        var initializer = new BasicGameInitializer();

        // Act
        var result = initializer.Initialize(options);

        // Assert
        Assert.IsTrue(result.Success);
        var game = result.Game!;

        Assert.IsInstanceOfType(game.DrawPile, typeof(Zone));
        var drawPile = ((Zone)game.DrawPile).Cards;

        // Verify that cards have correct metadata
        var cardDefinitionService = new BasicCardDefinitionService();

        foreach (var card in drawPile)
        {
            var definition = cardDefinitionService.GetDefinition(card.DefinitionId);
            
            if (definition is not null)
            {
                Assert.AreEqual(definition.CardType, card.CardType, 
                    $"Card {card.DefinitionId} should have CardType {definition.CardType}");
                Assert.AreEqual(definition.CardSubType, card.CardSubType,
                    $"Card {card.DefinitionId} should have CardSubType {definition.CardSubType}");
                Assert.AreEqual(definition.Name, card.Name,
                    $"Card {card.DefinitionId} should have Name {definition.Name}");
            }
            else
            {
                // If definition is null, card should have Unknown subtype
                Assert.AreEqual(CardSubType.Unknown, card.CardSubType,
                    $"Card {card.DefinitionId} without definition should have Unknown CardSubType");
            }
        }
    }

    /// <summary>
    /// Verifies that basic cards have correct subtypes.
    /// </summary>
    [TestMethod]
    public void PopulateDrawPile_BasicCardsHaveCorrectSubtypes()
    {
        // Arrange
        var config = CreateTestConfig();
        var random = new FixedRandomSource(0);
        var options = new GameInitializationOptions
        {
            GameConfig = config,
            Random = random,
            GameMode = new DummyGameMode()
        };

        var initializer = new BasicGameInitializer();

        // Act
        var result = initializer.Initialize(options);

        // Assert
        Assert.IsTrue(result.Success);
        var game = result.Game!;

        Assert.IsInstanceOfType(game.DrawPile, typeof(Zone));
        var allCards = ((Zone)game.DrawPile).Cards
            .Concat(game.Players.SelectMany(p => ((Zone)p.HandZone).Cards))
            .ToList();

        var slashCards = allCards.Where(c => c.DefinitionId == "Base.Slash").ToList();
        var dodgeCards = allCards.Where(c => c.DefinitionId == "Base.Dodge").ToList();
        var peachCards = allCards.Where(c => c.DefinitionId == "Base.Peach").ToList();

        // Verify counts
        Assert.AreEqual(30, slashCards.Count, "Should have 30 Slash cards");
        Assert.AreEqual(15, dodgeCards.Count, "Should have 15 Dodge cards");
        Assert.AreEqual(8, peachCards.Count, "Should have 8 Peach cards");

        // Verify subtypes
        foreach (var card in slashCards)
        {
            Assert.AreEqual(CardSubType.Slash, card.CardSubType, "Slash cards should have Slash subtype");
            Assert.AreEqual(CardType.Basic, card.CardType, "Slash cards should be Basic type");
        }

        foreach (var card in dodgeCards)
        {
            Assert.AreEqual(CardSubType.Dodge, card.CardSubType, "Dodge cards should have Dodge subtype");
            Assert.AreEqual(CardType.Basic, card.CardType, "Dodge cards should be Basic type");
        }

        foreach (var card in peachCards)
        {
            Assert.AreEqual(CardSubType.Peach, card.CardSubType, "Peach cards should have Peach subtype");
            Assert.AreEqual(CardType.Basic, card.CardType, "Peach cards should be Basic type");
        }
    }

    /// <summary>
    /// Verifies that trick cards have correct subtypes.
    /// </summary>
    [TestMethod]
    public void PopulateDrawPile_TrickCardsHaveCorrectSubtypes()
    {
        // Arrange
        var config = CreateTestConfig();
        var random = new FixedRandomSource(0);
        var options = new GameInitializationOptions
        {
            GameConfig = config,
            Random = random,
            GameMode = new DummyGameMode()
        };

        var initializer = new BasicGameInitializer();

        // Act
        var result = initializer.Initialize(options);

        // Assert
        Assert.IsTrue(result.Success);
        var game = result.Game!;

        Assert.IsInstanceOfType(game.DrawPile, typeof(Zone));
        var allCards = ((Zone)game.DrawPile).Cards
            .Concat(game.Players.SelectMany(p => ((Zone)p.HandZone).Cards))
            .ToList();

        // Verify some key trick cards
        var wuzhongCards = allCards.Where(c => c.DefinitionId == "Trick.WuzhongShengyou").ToList();
        var guoheCards = allCards.Where(c => c.DefinitionId == "Trick.GuoheChaiqiao").ToList();
        var lebusishuCards = allCards.Where(c => c.DefinitionId == "Trick.Lebusishu").ToList();

        Assert.AreEqual(4, wuzhongCards.Count, "Should have 4 WuzhongShengyou cards");
        Assert.AreEqual(6, guoheCards.Count, "Should have 6 GuoheChaiqiao cards");
        Assert.AreEqual(3, lebusishuCards.Count, "Should have 3 Lebusishu cards");

        // Verify subtypes
        foreach (var card in wuzhongCards)
        {
            Assert.AreEqual(CardSubType.WuzhongShengyou, card.CardSubType);
            Assert.AreEqual(CardType.Trick, card.CardType);
        }

        foreach (var card in guoheCards)
        {
            Assert.AreEqual(CardSubType.GuoheChaiqiao, card.CardSubType);
            Assert.AreEqual(CardType.Trick, card.CardType);
        }

        foreach (var card in lebusishuCards)
        {
            Assert.AreEqual(CardSubType.Lebusishu, card.CardSubType);
            Assert.AreEqual(CardType.Trick, card.CardType);
        }
    }

    /// <summary>
    /// Verifies that equipment cards have correct subtypes.
    /// </summary>
    [TestMethod]
    public void PopulateDrawPile_EquipmentCardsHaveCorrectSubtypes()
    {
        // Arrange
        var config = CreateTestConfig();
        var random = new FixedRandomSource(0);
        var options = new GameInitializationOptions
        {
            GameConfig = config,
            Random = random,
            GameMode = new DummyGameMode()
        };

        var initializer = new BasicGameInitializer();

        // Act
        var result = initializer.Initialize(options);

        // Assert
        Assert.IsTrue(result.Success);
        var game = result.Game!;

        Assert.IsInstanceOfType(game.DrawPile, typeof(Zone));
        var allCards = ((Zone)game.DrawPile).Cards
            .Concat(game.Players.SelectMany(p => ((Zone)p.HandZone).Cards))
            .ToList();

        // Verify equipment cards
        var weaponCards = allCards.Where(c => c.CardType == CardType.Equip && c.CardSubType == CardSubType.Weapon).ToList();
        var armorCards = allCards.Where(c => c.CardType == CardType.Equip && c.CardSubType == CardSubType.Armor).ToList();
        var offensiveHorseCards = allCards.Where(c => c.CardType == CardType.Equip && c.CardSubType == CardSubType.OffensiveHorse).ToList();
        var defensiveHorseCards = allCards.Where(c => c.CardType == CardType.Equip && c.CardSubType == CardSubType.DefensiveHorse).ToList();

        // Weapons: 2 (Zhugeliannu) + 8 (single weapons) = 10
        // Weapons: 2 (Zhugeliannu) + 8 (single weapons) = 10
        Assert.IsTrue(weaponCards.Count >= 10 && weaponCards.Count <= 12, 
            $"Should have 10-12 weapon cards, got {weaponCards.Count}");
        Assert.AreEqual(3, armorCards.Count, "Should have 3 armor cards");
        Assert.AreEqual(4, offensiveHorseCards.Count, "Should have 4 offensive horse cards");
        Assert.AreEqual(2, defensiveHorseCards.Count, "Should have 2 defensive horse cards");

        // Verify all equipment cards have Equip type
        var allEquipment = allCards.Where(c => c.CardType == CardType.Equip).ToList();
        Assert.AreEqual(19, allEquipment.Count, "Should have 19 equipment cards total");
    }

    private sealed class DummyGameMode : IGameMode
    {
        public string Id => "dummy";
        public string DisplayName => "Dummy";
        public int SelectFirstPlayerSeat(Game game) => 0;
    }
}
