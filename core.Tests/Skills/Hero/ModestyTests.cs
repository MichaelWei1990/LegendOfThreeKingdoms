using System;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Skills.Hero;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests;

[TestClass]
public sealed class ModestyTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Card CreateShunshouQianyangCard(int id = 1)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "shunshou_qianyang",
            Name = "顺手牵羊",
            CardType = CardType.Trick,
            CardSubType = CardSubType.ShunshouQianyang,
            Suit = Suit.Heart,
            Rank = 3
        };
    }

    private static Card CreateLebusishuCard(int id = 2)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "lebusishu",
            Name = "乐不思蜀",
            CardType = CardType.Trick,
            CardSubType = CardSubType.Lebusishu,
            Suit = Suit.Heart,
            Rank = 3
        };
    }

    private static Card CreateGuoheChaiqiaoCard(int id = 3)
    {
        return new Card
        {
            Id = id,
            DefinitionId = "guohe_chaiqiao",
            Name = "过河拆桥",
            CardType = CardType.Trick,
            CardSubType = CardSubType.GuoheChaiqiao,
            Suit = Suit.Heart,
            Rank = 3
        };
    }

    #region Skill Factory Tests

    /// <summary>
    /// Tests that ModestySkillFactory creates correct skill instance.
    /// Input: ModestySkillFactory instance.
    /// Expected: Created skill has correct Id, Name, and SkillType.Locked.
    /// </summary>
    [TestMethod]
    public void ModestySkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new ModestySkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("modesty", skill.Id);
        Assert.AreEqual("谦逊", skill.Name);
        Assert.AreEqual(SkillType.Locked, skill.Type);
        Assert.IsTrue(skill is ITargetFilteringSkill);
    }

    /// <summary>
    /// Tests that SkillRegistry can register and retrieve Modesty skill.
    /// Input: Empty registry, ModestySkillFactory.
    /// Expected: After registration, GetSkill returns a skill with correct Id.
    /// </summary>
    [TestMethod]
    public void SkillRegistryRegisterModestySkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new ModestySkillFactory();

        // Act
        registry.RegisterSkill("modesty", factory);
        var skill = registry.GetSkill("modesty");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("modesty", skill.Id);
        Assert.AreEqual("谦逊", skill.Name);
    }

    #endregion

    #region Target Filtering Tests

    /// <summary>
    /// Tests that ModestySkill excludes the owner from ShunshouQianyang targets.
    /// Input: Game with 2 players, target has Modesty skill, source uses ShunshouQianyang.
    /// Expected: Target is excluded from legal targets.
    /// </summary>
    [TestMethod]
    public void ModestySkillExcludesOwnerFromShunshouQianyangTargets()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];
        var target = game.Players[1]; // Target has Modesty skill

        var shunshouQianyang = CreateShunshouQianyangCard();
        ((Zone)source.HandZone).MutableCards.Add(shunshouQianyang);

        // Setup skill manager and register Modesty skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("modesty", new ModestySkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Modesty skill to target player
        var modestySkill = skillRegistry.GetSkill("modesty");
        if (modestySkill is not null)
        {
            skillManager.AddEquipmentSkill(game, target, modestySkill);
        }

        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var rangeRuleService = new RangeRuleService(modifierProvider);
        var cardUsageRuleService = new CardUsageRuleService(
            new PhaseRuleService(),
            rangeRuleService,
            new LimitRuleService(),
            modifierProvider,
            skillManager);

        var usageContext = new CardUsageContext(
            game,
            source,
            shunshouQianyang,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 0);

        // Act
        var legalTargets = cardUsageRuleService.GetLegalTargets(usageContext);

        // Assert
        Assert.IsFalse(legalTargets.HasAny, "Target with Modesty skill should be excluded from ShunshouQianyang targets.");
        Assert.AreEqual(0, legalTargets.Items.Count);
    }

    /// <summary>
    /// Tests that ModestySkill excludes the owner from Lebusishu targets.
    /// Input: Game with 2 players, target has Modesty skill, source uses Lebusishu.
    /// Expected: Target is excluded from legal targets.
    /// </summary>
    [TestMethod]
    public void ModestySkillExcludesOwnerFromLebusishuTargets()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];
        var target = game.Players[1]; // Target has Modesty skill

        var lebusishu = CreateLebusishuCard();
        ((Zone)source.HandZone).MutableCards.Add(lebusishu);

        // Setup skill manager and register Modesty skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("modesty", new ModestySkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Modesty skill to target player
        var modestySkill = skillRegistry.GetSkill("modesty");
        if (modestySkill is not null)
        {
            skillManager.AddEquipmentSkill(game, target, modestySkill);
        }

        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var rangeRuleService = new RangeRuleService(modifierProvider);
        var cardUsageRuleService = new CardUsageRuleService(
            new PhaseRuleService(),
            rangeRuleService,
            new LimitRuleService(),
            modifierProvider,
            skillManager);

        var usageContext = new CardUsageContext(
            game,
            source,
            lebusishu,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 0);

        // Act
        var legalTargets = cardUsageRuleService.GetLegalTargets(usageContext);

        // Assert
        Assert.IsFalse(legalTargets.HasAny, "Target with Modesty skill should be excluded from Lebusishu targets.");
        Assert.AreEqual(0, legalTargets.Items.Count);
    }

    /// <summary>
    /// Tests that ModestySkill does NOT exclude the owner from GuoheChaiqiao targets.
    /// Input: Game with 2 players, target has Modesty skill, source uses GuoheChaiqiao.
    /// Expected: Target is still a legal target (Modesty only affects ShunshouQianyang and Lebusishu).
    /// </summary>
    [TestMethod]
    public void ModestySkillDoesNotExcludeOwnerFromGuoheChaiqiaoTargets()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];
        var target = game.Players[1]; // Target has Modesty skill

        var guoheChaiqiao = CreateGuoheChaiqiaoCard();
        ((Zone)source.HandZone).MutableCards.Add(guoheChaiqiao);

        // Setup skill manager and register Modesty skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("modesty", new ModestySkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Modesty skill to target player
        var modestySkill = skillRegistry.GetSkill("modesty");
        if (modestySkill is not null)
        {
            skillManager.AddEquipmentSkill(game, target, modestySkill);
        }

        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var rangeRuleService = new RangeRuleService(modifierProvider);
        var cardUsageRuleService = new CardUsageRuleService(
            new PhaseRuleService(),
            rangeRuleService,
            new LimitRuleService(),
            modifierProvider,
            skillManager);

        var usageContext = new CardUsageContext(
            game,
            source,
            guoheChaiqiao,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 0);

        // Act
        var legalTargets = cardUsageRuleService.GetLegalTargets(usageContext);

        // Assert
        Assert.IsTrue(legalTargets.HasAny, "Target with Modesty skill should still be a legal target for GuoheChaiqiao.");
        Assert.AreEqual(1, legalTargets.Items.Count);
        Assert.AreEqual(target.Seat, legalTargets.Items[0].Seat);
    }

    /// <summary>
    /// Tests that ModestySkill does NOT exclude other players from ShunshouQianyang targets.
    /// Input: Game with 3 players, player 1 has Modesty skill, source uses ShunshouQianyang on player 2.
    /// Expected: Player 2 is still a legal target (Modesty only protects the owner).
    /// </summary>
    [TestMethod]
    public void ModestySkillDoesNotExcludeOtherPlayersFromShunshouQianyangTargets()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];
        var modestyOwner = game.Players[1]; // Has Modesty skill
        var otherTarget = game.Players[2]; // Does not have Modesty skill

        var shunshouQianyang = CreateShunshouQianyangCard();
        ((Zone)source.HandZone).MutableCards.Add(shunshouQianyang);

        // Setup skill manager and register Modesty skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("modesty", new ModestySkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Modesty skill to player 1 only
        var modestySkill = skillRegistry.GetSkill("modesty");
        if (modestySkill is not null)
        {
            skillManager.AddEquipmentSkill(game, modestyOwner, modestySkill);
        }

        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var rangeRuleService = new RangeRuleService(modifierProvider);
        var cardUsageRuleService = new CardUsageRuleService(
            new PhaseRuleService(),
            rangeRuleService,
            new LimitRuleService(),
            modifierProvider,
            skillManager);

        var usageContext = new CardUsageContext(
            game,
            source,
            shunshouQianyang,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 0);

        // Act
        var legalTargets = cardUsageRuleService.GetLegalTargets(usageContext);

        // Assert
        Assert.IsTrue(legalTargets.HasAny, "Other players without Modesty should still be legal targets.");
        Assert.AreEqual(1, legalTargets.Items.Count, "Only player 2 should be a legal target (player 1 has Modesty, player 2 is within distance 1).");
        Assert.AreEqual(otherTarget.Seat, legalTargets.Items[0].Seat);
        Assert.IsFalse(legalTargets.Items.Any(p => p.Seat == modestyOwner.Seat), "Player with Modesty should be excluded.");
    }

    /// <summary>
    /// Tests that ModestySkill does NOT exclude other players from Lebusishu targets.
    /// Input: Game with 3 players, player 1 has Modesty skill, source uses Lebusishu on player 2.
    /// Expected: Player 2 is still a legal target (Modesty only protects the owner).
    /// </summary>
    [TestMethod]
    public void ModestySkillDoesNotExcludeOtherPlayersFromLebusishuTargets()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];
        var modestyOwner = game.Players[1]; // Has Modesty skill
        var otherTarget = game.Players[2]; // Does not have Modesty skill

        var lebusishu = CreateLebusishuCard();
        ((Zone)source.HandZone).MutableCards.Add(lebusishu);

        // Setup skill manager and register Modesty skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("modesty", new ModestySkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Modesty skill to player 1 only
        var modestySkill = skillRegistry.GetSkill("modesty");
        if (modestySkill is not null)
        {
            skillManager.AddEquipmentSkill(game, modestyOwner, modestySkill);
        }

        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var rangeRuleService = new RangeRuleService(modifierProvider);
        var cardUsageRuleService = new CardUsageRuleService(
            new PhaseRuleService(),
            rangeRuleService,
            new LimitRuleService(),
            modifierProvider,
            skillManager);

        var usageContext = new CardUsageContext(
            game,
            source,
            lebusishu,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 0);

        // Act
        var legalTargets = cardUsageRuleService.GetLegalTargets(usageContext);

        // Assert
        Assert.IsTrue(legalTargets.HasAny, "Other players without Modesty should still be legal targets.");
        Assert.AreEqual(1, legalTargets.Items.Count, "Only player 2 should be a legal target (player 1 has Modesty).");
        Assert.AreEqual(otherTarget.Seat, legalTargets.Items[0].Seat);
        Assert.IsFalse(legalTargets.Items.Any(p => p.Seat == modestyOwner.Seat), "Player with Modesty should be excluded.");
    }

    /// <summary>
    /// Tests that ModestySkill does not exclude targets when the skill owner is not alive.
    /// Input: Game with 2 players, target has Modesty skill but is dead, source uses ShunshouQianyang.
    /// Expected: Target is not excluded (skill is inactive when owner is dead).
    /// </summary>
    [TestMethod]
    public void ModestySkillDoesNotExcludeTargetsWhenOwnerIsDead()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        game.CurrentPhase = Phase.Play;
        var source = game.Players[0];
        var target = game.Players[1];
        target.IsAlive = false; // Target is dead

        var shunshouQianyang = CreateShunshouQianyangCard();
        ((Zone)source.HandZone).MutableCards.Add(shunshouQianyang);

        // Setup skill manager and register Modesty skill
        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("modesty", new ModestySkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);

        // Add Modesty skill to target player (even though dead)
        var modestySkill = skillRegistry.GetSkill("modesty");
        if (modestySkill is not null)
        {
            skillManager.AddEquipmentSkill(game, target, modestySkill);
        }

        var modifierProvider = new SkillRuleModifierProvider(skillManager);
        var rangeRuleService = new RangeRuleService(modifierProvider);
        var cardUsageRuleService = new CardUsageRuleService(
            new PhaseRuleService(),
            rangeRuleService,
            new LimitRuleService(),
            modifierProvider,
            skillManager);

        var usageContext = new CardUsageContext(
            game,
            source,
            shunshouQianyang,
            game.Players,
            IsExtraAction: false,
            UsageCountThisTurn: 0);

        // Act
        var legalTargets = cardUsageRuleService.GetLegalTargets(usageContext);

        // Assert
        // Note: Dead players are already excluded by basic rules (p.IsAlive check),
        // so this test mainly verifies that the skill doesn't cause issues when owner is dead
        Assert.IsFalse(legalTargets.HasAny, "Dead players should not be legal targets (excluded by basic rules).");
    }

    #endregion
}
