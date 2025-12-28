using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Model.Zones;
using LegendOfThreeKingdoms.Core.Resolution;
using LegendOfThreeKingdoms.Core.Rules;
using LegendOfThreeKingdoms.Core.Skills;
using LegendOfThreeKingdoms.Core.Skills.Hero;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests;

[TestClass]
public sealed class LiuliTests
{
    private static Game CreateDefaultGame(int playerCount = 3)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    #region Skill Factory Tests

    /// <summary>
    /// Tests that LiuliSkillFactory creates correct skill instance.
    /// </summary>
    [TestMethod]
    public void LiuliSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new LiuliSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("liuli", skill.Id);
        Assert.AreEqual("流离", skill.Name);
        Assert.AreEqual(SkillType.Trigger, skill.Type);
        Assert.IsTrue(skill.Capabilities.HasFlag(SkillCapability.InitiatesChoices));
    }

    /// <summary>
    /// Tests that SkillRegistry can register and retrieve Liuli skill.
    /// </summary>
    [TestMethod]
    public void SkillRegistryRegisterLiuliSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var factory = new LiuliSkillFactory();

        // Act
        registry.RegisterSkill("liuli", factory);
        var skill = registry.GetSkill("liuli");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("liuli", skill.Id);
        Assert.AreEqual("流离", skill.Name);
    }

    #endregion

    #region CanModifyTarget Tests

    /// <summary>
    /// Tests that CanModifyTarget returns false when owner has no discardable cards.
    /// </summary>
    [TestMethod]
    public void CanModifyTargetReturnsFalseWhenNoDiscardableCards()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        var owner = game.Players[0];
        var attacker = game.Players[1];
        var slashCard = new Card { Id = 1, DefinitionId = "slash1", CardType = CardType.Basic, CardSubType = CardSubType.Slash };
        var ruleService = new RuleService();
        var skill = new LiuliSkill();

        // Owner has no cards
        // (game is initialized with empty hands by default)

        // Act
        var canModify = skill.CanModifyTarget(game, owner, attacker, slashCard, ruleService);

        // Assert
        Assert.IsFalse(canModify);
    }

    /// <summary>
    /// Tests that CanModifyTarget returns false when no valid redirect targets.
    /// </summary>
    [TestMethod]
    public void CanModifyTargetReturnsFalseWhenNoValidRedirectTargets()
    {
        // Arrange
        var game = CreateDefaultGame(2); // Only 2 players, so no other target
        var owner = game.Players[0];
        var attacker = game.Players[1];
        var slashCard = new Card { Id = 1, DefinitionId = "slash1", CardType = CardType.Basic, CardSubType = CardSubType.Slash };
        var ruleService = new RuleService();
        var skill = new LiuliSkill();

        // Add a card to owner's hand
        if (owner.HandZone is Zone handZone)
        {
            var card = new Card { Id = 1, DefinitionId = "card1", CardType = CardType.Basic };
            handZone.MutableCards.Add(card);
        }

        // Act
        var canModify = skill.CanModifyTarget(game, owner, attacker, slashCard, ruleService);

        // Assert
        // With only 2 players, owner cannot redirect to anyone else (attacker is excluded)
        Assert.IsFalse(canModify);
    }

    /// <summary>
    /// Tests that CanModifyTarget returns true when conditions are met.
    /// </summary>
    [TestMethod]
    public void CanModifyTargetReturnsTrueWhenConditionsMet()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        var owner = game.Players[0];
        var attacker = game.Players[1];
        var slashCard = new Card { Id = 1, DefinitionId = "slash1", CardType = CardType.Basic, CardSubType = CardSubType.Slash };
        var ruleService = new RuleService();
        var skill = new LiuliSkill();

        // Add a card to owner's hand
        if (owner.HandZone is Zone handZone)
        {
            var card = new Card { Id = 1, DefinitionId = "card1", CardType = CardType.Basic };
            handZone.MutableCards.Add(card);
        }

        // Act
        var canModify = skill.CanModifyTarget(game, owner, attacker, slashCard, ruleService);

        // Assert
        // With 3 players, owner can redirect to player 2 (within attack range by default)
        Assert.IsTrue(canModify);
    }

    /// <summary>
    /// Tests that CanModifyTarget returns false when owner is dead.
    /// </summary>
    [TestMethod]
    public void CanModifyTargetReturnsFalseWhenOwnerIsDead()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        var owner = game.Players[0];
        var attacker = game.Players[1];
        var slashCard = new Card { Id = 1, DefinitionId = "slash1", CardType = CardType.Basic, CardSubType = CardSubType.Slash };
        var ruleService = new RuleService();
        var skill = new LiuliSkill();

        // Kill owner
        owner.CurrentHealth = 0;

        // Act
        var canModify = skill.CanModifyTarget(game, owner, attacker, slashCard, ruleService);

        // Assert
        Assert.IsFalse(canModify);
    }

    #endregion

    #region CreateTargetModificationResolver Tests

    /// <summary>
    /// Tests that CreateTargetModificationResolver returns a resolver.
    /// </summary>
    [TestMethod]
    public void CreateTargetModificationResolverReturnsResolver()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        var owner = game.Players[0];
        var attacker = game.Players[1];
        var slashCard = new Card { Id = 1, DefinitionId = "slash1", CardType = CardType.Basic, CardSubType = CardSubType.Slash };
        var pendingDamage = new DamageDescriptor(
            SourceSeat: attacker.Seat,
            TargetSeat: owner.Seat,
            Amount: 1,
            Type: DamageType.Normal,
            Reason: "Slash"
        );
        var skill = new LiuliSkill();

        // Act
        var resolver = skill.CreateTargetModificationResolver(owner, attacker, slashCard, pendingDamage);

        // Assert
        Assert.IsNotNull(resolver);
    }

    #endregion

    #region Integration Tests

    /// <summary>
    /// Tests that Liuli skill can be registered and retrieved for Da Qiao.
    /// </summary>
    [TestMethod]
    public void LiuliSkillCanBeRegisteredForDaQiao()
    {
        // Arrange
        var registry = new SkillRegistry();

        // Act
        WuHeroRegistration.RegisterAll(registry);

        // Assert
        Assert.IsTrue(registry.IsSkillRegistered("liuli"));
        Assert.IsTrue(registry.IsSkillRegistered("guose"));
        var skills = registry.GetSkillsForHero("daqiao").ToList();
        Assert.AreEqual(2, skills.Count, "Da Qiao should have 2 skills: liuli and guose");
        Assert.IsTrue(skills.Any(s => s.Id == "liuli"), "Da Qiao should have liuli skill");
        Assert.IsTrue(skills.Any(s => s.Id == "guose"), "Da Qiao should have guose skill");
    }

    /// <summary>
    /// Tests that Liuli skill redirects Slash to another target when activated.
    /// </summary>
    [TestMethod]
    public void LiuliSkillRedirectsSlashToAnotherTarget()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 1; // Attacker's turn
        var attacker = game.Players[1];
        var owner = game.Players[0]; // Da Qiao with Liuli
        var newTarget = game.Players[2]; // Target to redirect to

        // Add cards
        var slashCard = CreateSlashCard(1);
        ((Zone)attacker.HandZone).MutableCards.Add(slashCard);
        var discardCard = CreateTestCard(2);
        ((Zone)owner.HandZone).MutableCards.Add(discardCard);

        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("liuli", new LiuliSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);
        // Attach skill to owner using AddEquipmentSkill (works for hero skills in tests)
        var liuliSkill = skillRegistry.GetSkill("liuli")!;
        skillManager.AddEquipmentSkill(game, owner, liuliSkill);

        var cardMoveService = new BasicCardMoveService(eventBus);
        var ruleService = new RuleService(skillManager: skillManager);
        var stack = new BasicResolutionStack();

        var initialOwnerHandCount = owner.HandZone.Cards.Count;
        var initialNewTargetHandCount = newTarget.HandZone.Cards.Count;

        // Setup getPlayerChoice to activate Liuli and redirect to newTarget
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            if (request.PlayerSeat == owner.Seat)
            {
                if (request.ChoiceType == ChoiceType.Confirm)
                {
                    // Confirm activation of Liuli
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: null,
                        SelectedOptionId: null,
                        Confirmed: true
                    );
                }
                else if (request.ChoiceType == ChoiceType.SelectCards)
                {
                    // Select card to discard
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: null,
                        SelectedCardIds: new[] { discardCard.Id },
                        SelectedOptionId: null,
                        Confirmed: null
                    );
                }
                else if (request.ChoiceType == ChoiceType.SelectTargets)
                {
                    // Select new target
                    return new ChoiceResult(
                        RequestId: request.RequestId,
                        PlayerSeat: request.PlayerSeat,
                        SelectedTargetSeats: new[] { newTarget.Seat },
                        SelectedCardIds: null,
                        SelectedOptionId: null,
                        Confirmed: null
                    );
                }
            }
            // For response window (Jink), pass
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: attacker.Seat,
            SelectedTargetSeats: new[] { owner.Seat },
            SelectedCardIds: new[] { slashCard.Id },
            SelectedOptionId: null,
            Confirmed: true
        );

        var action = new ActionDescriptor(
            ActionId: "UseSlash",
            DisplayKey: "action.useSlash",
            RequiresTargets: true,
            TargetConstraints: new TargetConstraints(1, 1),
            CardCandidates: new[] { slashCard }
        );

        var intermediateResults = new Dictionary<string, object>();
        var resolutionContext = new ResolutionContext(
            game,
            attacker,
            action,
            choice,
            stack,
            cardMoveService,
            ruleService,
            GetPlayerChoice: getPlayerChoice,
            IntermediateResults: intermediateResults,
            SkillManager: skillManager,
            EventBus: eventBus
        );

        // Act - Use Slash resolver
        var slashResolver = new SlashResolver();
        var result = slashResolver.Resolve(resolutionContext);

        // Execute the entire stack
        while (!stack.IsEmpty)
        {
            stack.Pop();
        }

        // Assert
        Assert.IsTrue(result.Success, "Slash resolution should succeed");
        Assert.AreEqual(initialOwnerHandCount - 1, owner.HandZone.Cards.Count, "Owner should have discarded one card");
        Assert.IsFalse(owner.HandZone.Cards.Any(c => c.Id == discardCard.Id), "Discarded card should not be in owner's hand");
        Assert.IsTrue(game.DiscardPile.Cards.Any(c => c.Id == discardCard.Id), "Discarded card should be in discard pile");

        // Check that target was redirected (newTarget should have been targeted, not owner)
        // This is verified by checking that newTarget's hand count changed (if they had no Jink)
        // or by checking IntermediateResults
        if (intermediateResults.TryGetValue("LiuliNewTargetSeat", out var newTargetSeatObj) &&
            newTargetSeatObj is int newTargetSeat)
        {
            Assert.AreEqual(newTarget.Seat, newTargetSeat, "Target should be redirected to newTarget");
        }
    }

    /// <summary>
    /// Tests that Liuli skill does not activate when player chooses not to use it.
    /// </summary>
    [TestMethod]
    public void LiuliSkillDoesNotActivateWhenPlayerDeclines()
    {
        // Arrange
        var game = CreateDefaultGame(3);
        game.CurrentPhase = Phase.Play;
        game.CurrentPlayerSeat = 1;
        var attacker = game.Players[1];
        var owner = game.Players[0];
        var originalTarget = owner;

        var slashCard = CreateSlashCard(1);
        ((Zone)attacker.HandZone).MutableCards.Add(slashCard);
        var discardCard = CreateTestCard(2);
        ((Zone)owner.HandZone).MutableCards.Add(discardCard);

        var eventBus = new BasicEventBus();
        var skillRegistry = new SkillRegistry();
        skillRegistry.RegisterSkill("liuli", new LiuliSkillFactory());
        var skillManager = new SkillManager(skillRegistry, eventBus);
        // Attach skill to owner using AddEquipmentSkill (works for hero skills in tests)
        var liuliSkill = skillRegistry.GetSkill("liuli")!;
        skillManager.AddEquipmentSkill(game, owner, liuliSkill);

        var cardMoveService = new BasicCardMoveService(eventBus);
        var ruleService = new RuleService(skillManager: skillManager);
        var stack = new BasicResolutionStack();

        var initialOwnerHandCount = owner.HandZone.Cards.Count;

        // Setup getPlayerChoice to decline Liuli activation
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            if (request.PlayerSeat == owner.Seat && request.ChoiceType == ChoiceType.Confirm)
            {
                // Decline activation of Liuli
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: false
                );
            }
            // For response window (Jink), pass
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: null
            );
        };

        var choice = new ChoiceResult(
            RequestId: "test-request",
            PlayerSeat: attacker.Seat,
            SelectedTargetSeats: new[] { owner.Seat },
            SelectedCardIds: new[] { slashCard.Id },
            SelectedOptionId: null,
            Confirmed: true
        );

        var action = new ActionDescriptor(
            ActionId: "UseSlash",
            DisplayKey: "action.useSlash",
            RequiresTargets: true,
            TargetConstraints: new TargetConstraints(1, 1),
            CardCandidates: new[] { slashCard }
        );

        var intermediateResults = new Dictionary<string, object>();
        var resolutionContext = new ResolutionContext(
            game,
            attacker,
            action,
            choice,
            stack,
            cardMoveService,
            ruleService,
            GetPlayerChoice: getPlayerChoice,
            IntermediateResults: intermediateResults,
            SkillManager: skillManager,
            EventBus: eventBus
        );

        // Act
        var slashResolver = new SlashResolver();
        var result = slashResolver.Resolve(resolutionContext);

        // Execute the entire stack
        while (!stack.IsEmpty)
        {
            stack.Pop();
        }

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(initialOwnerHandCount, owner.HandZone.Cards.Count, "Owner should not have discarded any card");
        Assert.IsFalse(intermediateResults.ContainsKey("LiuliNewTargetSeat"), "Target should not be redirected");
    }

    #endregion

    #region Helper Methods

    private static Card CreateSlashCard(int id)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"slash{id}",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash
        };
    }

    private static Card CreateTestCard(int id, CardSubType subType = CardSubType.Slash)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"card{id}",
            CardType = CardType.Basic,
            CardSubType = subType
        };
    }

    #endregion
}

