using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests.Rules;

[TestClass]
public sealed class ActionIdMapperTests
{
    /// <summary>
    /// Verifies that all implemented card subtypes have action ID mappings.
    /// </summary>
    [TestMethod]
    public void ActionIdMapper_AllCardSubTypesHaveMappings()
    {
        // Get all card subtypes that should have action IDs
        var cardSubTypesThatShouldHaveActions = new[]
        {
            CardSubType.Slash,
            CardSubType.Peach,
            CardSubType.Weapon,
            CardSubType.Armor,
            CardSubType.OffensiveHorse,
            CardSubType.DefensiveHorse,
            CardSubType.WuzhongShengyou,
            CardSubType.TaoyuanJieyi,
            CardSubType.ShunshouQianyang,
            CardSubType.GuoheChaiqiao,
            CardSubType.WanjianQifa,
            CardSubType.NanmanRushin,
            CardSubType.Duel,
            CardSubType.Harvest,
            CardSubType.JieDaoShaRen,
            CardSubType.Lebusishu,
            CardSubType.Shandian
        };

        foreach (var cardSubType in cardSubTypesThatShouldHaveActions)
        {
            var actionId = ActionIdMapper.GetActionIdForCardSubType(cardSubType);
            Assert.IsNotNull(actionId, $"CardSubType {cardSubType} should have an action ID mapping, but it is null.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(actionId), $"CardSubType {cardSubType} should have a non-empty action ID.");
        }
    }

    /// <summary>
    /// Verifies that all action IDs have corresponding card subtype mappings.
    /// </summary>
    [TestMethod]
    public void ActionIdMapper_AllActionIdsHaveCardSubTypeMappings()
    {
        // Get all action IDs that should have card subtype mappings
        var actionIdsThatShouldHaveCardSubTypes = new[]
        {
            "UseSlash",
            "UsePeach",
            "UseEquip",
            "UseWuzhongShengyou",
            "UseTaoyuanJieyi",
            "UseShunshouQianyang",
            "UseGuoheChaiqiao",
            "UseWanjianQifa",
            "UseNanmanRushin",
            "UseDuel",
            "UseHarvest",
            "UseJieDaoShaRen",
            "UseLebusishu",
            "UseShandian"
        };

        foreach (var actionId in actionIdsThatShouldHaveCardSubTypes)
        {
            var cardSubType = ActionIdMapper.GetCardSubTypeForActionId(actionId);
            Assert.IsNotNull(cardSubType, $"Action ID {actionId} should have a card subtype mapping, but it is null.");
            Assert.AreNotEqual(CardSubType.Unknown, cardSubType, $"Action ID {actionId} should not map to Unknown card subtype.");
        }
    }

    /// <summary>
    /// Verifies that Register and Unregister work correctly.
    /// </summary>
    [TestMethod]
    public void ActionIdMapper_RegisterAndUnregisterWorks()
    {
        // Use a card subtype that is not commonly used in other tests to avoid state pollution
        // Use Dodge as it's less likely to be tested in bidirectional mapping tests
        var testCardSubType = CardSubType.Dodge;
        var testActionId = "TestUseDodge";
        
        // Save original state
        var originalActionId = ActionIdMapper.GetActionIdForCardSubType(testCardSubType);

        try
        {
            // Test Register
            ActionIdMapper.Register(testCardSubType, testActionId);
            var registeredActionId = ActionIdMapper.GetActionIdForCardSubType(testCardSubType);
            Assert.AreEqual(testActionId, registeredActionId, "Registered action ID should match");

            var registeredCardSubType = ActionIdMapper.GetCardSubTypeForActionId(testActionId);
            Assert.AreEqual(testCardSubType, registeredCardSubType, "Registered card subtype should match");

            // Test Unregister
            var unregisterResult = ActionIdMapper.Unregister(testCardSubType);
            Assert.IsTrue(unregisterResult, "Unregister should return true when removing an existing mapping");

            var unregisteredActionId = ActionIdMapper.GetActionIdForCardSubType(testCardSubType);
            Assert.IsNull(unregisteredActionId, "Unregistered action ID should be null");

            var unregisteredCardSubType = ActionIdMapper.GetCardSubTypeForActionId(testActionId);
            Assert.IsNull(unregisteredCardSubType, "Unregistered card subtype should be null");
        }
        finally
        {
            // Restore original state if it existed
            if (originalActionId is not null)
            {
                ActionIdMapper.Register(testCardSubType, originalActionId);
            }
            else
            {
                // If there was no original mapping, ensure we clean up
                ActionIdMapper.Unregister(testCardSubType);
            }
        }
    }

    /// <summary>
    /// Verifies that equipment cards (Weapon, Armor, OffensiveHorse, DefensiveHorse) all map to "UseEquip".
    /// </summary>
    [TestMethod]
    public void ActionIdMapper_EquipmentCardsMapToUseEquip()
    {
        var equipmentCardSubTypes = new[]
        {
            CardSubType.Weapon,
            CardSubType.Armor,
            CardSubType.OffensiveHorse,
            CardSubType.DefensiveHorse
        };

        foreach (var cardSubType in equipmentCardSubTypes)
        {
            var actionId = ActionIdMapper.GetActionIdForCardSubType(cardSubType);
            Assert.AreEqual("UseEquip", actionId, $"Equipment card {cardSubType} should map to UseEquip");
        }
    }

    /// <summary>
    /// Verifies that immediate trick cards have correct action ID mappings.
    /// </summary>
    [TestMethod]
    public void ActionIdMapper_ImmediateTrickCardsHaveCorrectMappings()
    {
        var immediateTrickMappings = new[]
        {
            (CardSubType.WuzhongShengyou, "UseWuzhongShengyou"),
            (CardSubType.TaoyuanJieyi, "UseTaoyuanJieyi"),
            (CardSubType.ShunshouQianyang, "UseShunshouQianyang"),
            (CardSubType.GuoheChaiqiao, "UseGuoheChaiqiao"),
            (CardSubType.WanjianQifa, "UseWanjianQifa"),
            (CardSubType.NanmanRushin, "UseNanmanRushin"),
            (CardSubType.Duel, "UseDuel"),
            (CardSubType.Harvest, "UseHarvest"),
            (CardSubType.JieDaoShaRen, "UseJieDaoShaRen")
        };

        foreach (var (cardSubType, expectedActionId) in immediateTrickMappings)
        {
            var actionId = ActionIdMapper.GetActionIdForCardSubType(cardSubType);
            Assert.AreEqual(expectedActionId, actionId, $"Immediate trick {cardSubType} should map to {expectedActionId}");
        }
    }

    /// <summary>
    /// Verifies that delayed trick cards have correct action ID mappings.
    /// </summary>
    [TestMethod]
    public void ActionIdMapper_DelayedTrickCardsHaveCorrectMappings()
    {
        var delayedTrickMappings = new[]
        {
            (CardSubType.Lebusishu, "UseLebusishu"),
            (CardSubType.Shandian, "UseShandian")
        };

        foreach (var (cardSubType, expectedActionId) in delayedTrickMappings)
        {
            var actionId = ActionIdMapper.GetActionIdForCardSubType(cardSubType);
            Assert.AreEqual(expectedActionId, actionId, $"Delayed trick {cardSubType} should map to {expectedActionId}");
        }
    }

    /// <summary>
    /// Verifies that mappings are bidirectional (action ID -> card subtype and card subtype -> action ID).
    /// </summary>
    [TestMethod]
    public void ActionIdMapper_MappingsAreBidirectional()
    {
        var testMappings = new[]
        {
            (CardSubType.Slash, "UseSlash"),
            (CardSubType.Peach, "UsePeach"),
            (CardSubType.WuzhongShengyou, "UseWuzhongShengyou"),
            (CardSubType.Lebusishu, "UseLebusishu")
        };

        // Save original mappings to restore after test (in case other tests modified them)
        var originalMappings = new Dictionary<CardSubType, string?>();
        foreach (var (cardSubType, _) in testMappings)
        {
            originalMappings[cardSubType] = ActionIdMapper.GetActionIdForCardSubType(cardSubType);
        }

        try
        {
            foreach (var (cardSubType, expectedActionId) in testMappings)
            {
                // Test forward mapping (card subtype -> action ID)
                var forwardActionId = ActionIdMapper.GetActionIdForCardSubType(cardSubType);
                Assert.AreEqual(expectedActionId, forwardActionId, $"Forward mapping for {cardSubType} should return {expectedActionId}");

                // Test reverse mapping (action ID -> card subtype)
                var reverseCardSubType = ActionIdMapper.GetCardSubTypeForActionId(expectedActionId);
                Assert.AreEqual(cardSubType, reverseCardSubType, $"Reverse mapping for {expectedActionId} should return {cardSubType}");
            }
        }
        finally
        {
            // Restore original mappings if they were modified by other tests
            foreach (var (cardSubType, originalActionId) in originalMappings)
            {
                if (originalActionId is not null)
                {
                    var currentActionId = ActionIdMapper.GetActionIdForCardSubType(cardSubType);
                    if (currentActionId != originalActionId)
                    {
                        ActionIdMapper.Register(cardSubType, originalActionId);
                    }
                }
            }
        }
    }
}
