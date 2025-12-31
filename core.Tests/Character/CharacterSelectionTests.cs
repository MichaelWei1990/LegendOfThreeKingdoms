using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Character;
using LegendOfThreeKingdoms.Core.Configuration;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Skills.Hero;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests.Character;

[TestClass]
public sealed class CharacterSelectionTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static SkillRegistry CreateSkillRegistry()
    {
        var registry = new SkillRegistry();
        
        // Register some test skills
        registry.RegisterSkill("test_skill", new TestSkillFactory());
        registry.RegisterSkill("jijiang", new JijiangSkillFactory());
        registry.RegisterSkill("rende", new RendeSkillFactory());
        
        // Register test hero with skills
        registry.RegisterHeroSkills("test_hero", new[] { "test_skill", "jijiang" });
        registry.RegisterHeroMetadata("test_hero", maxHealth: 4, gender: Gender.Male);
        
        // Register another test hero without lord skill
        registry.RegisterHeroSkills("test_hero2", new[] { "test_skill", "rende" });
        registry.RegisterHeroMetadata("test_hero2", maxHealth: 3, gender: Gender.Female);
        
        return registry;
    }

    private static ICharacterCatalog CreateCatalog(SkillRegistry registry)
    {
        return new BasicCharacterCatalog(registry);
    }

    private static BasicCharacterSelectionService CreateSelectionService(
        ICharacterCatalog catalog,
        SkillManager skillManager,
        IEventBus eventBus)
    {
        return new BasicCharacterSelectionService(catalog, skillManager, eventBus);
    }

    #region OfferCharacters Tests

    [TestMethod]
    public void OfferCharacters_ValidCandidates_PublishesEvent()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var registry = CreateSkillRegistry();
        var catalog = CreateCatalog(registry);
        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(registry, eventBus);
        var service = CreateSelectionService(catalog, skillManager, eventBus);
        
        bool eventPublished = false;
        eventBus.Subscribe<CharactersOfferedEvent>(evt =>
        {
            Assert.AreEqual(0, evt.PlayerSeat);
            Assert.AreEqual(2, evt.CandidateCharacterIds.Count);
            eventPublished = true;
        });

        // Act
        service.OfferCharacters(game, 0, new[] { "test_hero", "test_hero2" });

        // Assert
        Assert.IsTrue(eventPublished);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void OfferCharacters_InvalidCharacterId_ThrowsException()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var registry = CreateSkillRegistry();
        var catalog = CreateCatalog(registry);
        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(registry, eventBus);
        var service = CreateSelectionService(catalog, skillManager, eventBus);

        // Act
        service.OfferCharacters(game, 0, new[] { "nonexistent_hero" });
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void OfferCharacters_EmptyList_ThrowsException()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var registry = CreateSkillRegistry();
        var catalog = CreateCatalog(registry);
        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(registry, eventBus);
        var service = CreateSelectionService(catalog, skillManager, eventBus);

        // Act
        service.OfferCharacters(game, 0, Array.Empty<string>());
    }

    #endregion

    #region SelectCharacter Tests

    [TestMethod]
    public void SelectCharacter_NonLordPlayer_RegistersOnlyNonLordSkills()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var registry = CreateSkillRegistry();
        var catalog = CreateCatalog(registry);
        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(registry, eventBus);
        var service = CreateSelectionService(catalog, skillManager, eventBus);
        
        var player = game.Players[0];
        // Player is not Lord (no IsLord flag set)
        
        service.OfferCharacters(game, 0, new[] { "test_hero", "test_hero2" });

        bool selectedEventPublished = false;
        bool skillsRegisteredEventPublished = false;
        eventBus.Subscribe<CharacterSelectedEvent>(evt =>
        {
            Assert.AreEqual(0, evt.PlayerSeat);
            Assert.AreEqual("test_hero", evt.CharacterId);
            selectedEventPublished = true;
        });
        eventBus.Subscribe<SkillsRegisteredEvent>(evt =>
        {
            Assert.AreEqual(0, evt.PlayerSeat);
            // Should only have test_skill, not jijiang (lord skill)
            Assert.IsTrue(evt.SkillIds.Contains("test_skill"));
            Assert.IsFalse(evt.SkillIds.Contains("jijiang"));
            skillsRegisteredEventPublished = true;
        });

        // Act
        service.SelectCharacter(game, 0, "test_hero");

        // Assert
        Assert.IsTrue(selectedEventPublished);
        Assert.IsTrue(skillsRegisteredEventPublished);
        
        // Verify skills registered
        var registeredSkills = skillManager.GetAllSkills(player).Select(s => s.Id).ToList();
        Assert.IsTrue(registeredSkills.Contains("test_skill"));
        Assert.IsFalse(registeredSkills.Contains("jijiang"));
    }

    [TestMethod]
    public void SelectCharacter_LordPlayer_RegistersLordSkills()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var registry = CreateSkillRegistry();
        var catalog = CreateCatalog(registry);
        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(registry, eventBus);
        var service = CreateSelectionService(catalog, skillManager, eventBus);
        
        var player = game.Players[0];
        player.Flags["IsLord"] = true; // Set player as Lord
        
        service.OfferCharacters(game, 0, new[] { "test_hero", "test_hero2" });

        bool selectedEventPublished = false;
        bool skillsRegisteredEventPublished = false;
        eventBus.Subscribe<CharacterSelectedEvent>(evt =>
        {
            Assert.AreEqual(0, evt.PlayerSeat);
            Assert.AreEqual("test_hero", evt.CharacterId);
            selectedEventPublished = true;
        });
        eventBus.Subscribe<SkillsRegisteredEvent>(evt =>
        {
            Assert.AreEqual(0, evt.PlayerSeat);
            // Should have both test_skill and jijiang (lord skill)
            Assert.IsTrue(evt.SkillIds.Contains("test_skill"));
            Assert.IsTrue(evt.SkillIds.Contains("jijiang"));
            skillsRegisteredEventPublished = true;
        });

        // Act
        service.SelectCharacter(game, 0, "test_hero");

        // Assert
        Assert.IsTrue(selectedEventPublished);
        Assert.IsTrue(skillsRegisteredEventPublished);
        
        // Verify skills registered
        var registeredSkills = skillManager.GetAllSkills(player).Select(s => s.Id).ToList();
        Assert.IsTrue(registeredSkills.Contains("test_skill"));
        Assert.IsTrue(registeredSkills.Contains("jijiang"));
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void SelectCharacter_NoCandidatesOffered_ThrowsException()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var registry = CreateSkillRegistry();
        var catalog = CreateCatalog(registry);
        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(registry, eventBus);
        var service = CreateSelectionService(catalog, skillManager, eventBus);

        // Act - try to select without offering candidates
        service.SelectCharacter(game, 0, "test_hero");
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void SelectCharacter_NotInCandidates_ThrowsException()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var registry = CreateSkillRegistry();
        var catalog = CreateCatalog(registry);
        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(registry, eventBus);
        var service = CreateSelectionService(catalog, skillManager, eventBus);
        
        service.OfferCharacters(game, 0, new[] { "test_hero" });

        // Act - try to select a character not in candidates
        service.SelectCharacter(game, 0, "test_hero2");
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void SelectCharacter_AlreadySelected_ThrowsException()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var registry = CreateSkillRegistry();
        var catalog = CreateCatalog(registry);
        var eventBus = new BasicEventBus();
        var skillManager = new SkillManager(registry, eventBus);
        var service = CreateSelectionService(catalog, skillManager, eventBus);
        
        service.OfferCharacters(game, 0, new[] { "test_hero", "test_hero2" });
        service.SelectCharacter(game, 0, "test_hero");

        // Act - try to select again
        service.SelectCharacter(game, 0, "test_hero2");
    }

    #endregion

    #region Helper Classes

    private sealed class TestSkill : BaseSkill
    {
        public override string Id => "test_skill";
        public override string Name => "Test Skill";
        public override SkillType Type => SkillType.Locked;
        public override SkillCapability Capabilities => SkillCapability.None;
    }

    private sealed class TestSkillFactory : ISkillFactory
    {
        public ISkill CreateSkill() => new TestSkill();
    }

    #endregion
}
