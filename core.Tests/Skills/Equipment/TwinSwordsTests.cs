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
using LegendOfThreeKingdoms.Core.Skills.Equipment;
using LegendOfThreeKingdoms.Core.Zones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests;

[TestClass]
public sealed class TwinSwordsTests
{
    private static Game CreateDefaultGame(int playerCount = 2)
    {
        var config = CoreApi.CreateDefaultConfig(playerCount);
        return Game.FromConfig(config);
    }

    private static Card CreateTwinSwordsCard(int id = 1, string definitionId = "twin_swords", string name = "雌雄双股剑")
    {
        return new Card
        {
            Id = id,
            DefinitionId = definitionId,
            Name = name,
            CardType = CardType.Equip,
            CardSubType = CardSubType.Weapon,
            Suit = Suit.Spade,
            Rank = 5
        };
    }

    private static Card CreateSlashCard(int id, Suit suit = Suit.Spade, int rank = 5)
    {
        return new Card
        {
            Id = id,
            DefinitionId = $"slash_{id}",
            CardType = CardType.Basic,
            CardSubType = CardSubType.Slash,
            Suit = suit,
            Rank = rank
        };
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

    #region Skill Factory Tests

    /// <summary>
    /// Tests that TwinSwordsSkillFactory creates correct skill instance.
    /// </summary>
    [TestMethod]
    public void TwinSwordsSkillFactoryCreatesCorrectSkillInstance()
    {
        // Arrange
        var factory = new TwinSwordsSkillFactory();

        // Act
        var skill = factory.CreateSkill();

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("twin_swords", skill.Id);
        Assert.AreEqual("雌雄", skill.Name);
        Assert.AreEqual(SkillType.Trigger, skill.Type);
    }

    /// <summary>
    /// Tests that EquipmentSkillRegistry can register and retrieve Twin Swords skill.
    /// </summary>
    [TestMethod]
    public void EquipmentSkillRegistryRegisterTwinSwordsSkillCanRetrieveSkill()
    {
        // Arrange
        var registry = new EquipmentSkillRegistry();
        var factory = new TwinSwordsSkillFactory();

        // Act
        registry.RegisterEquipmentSkill("twin_swords", factory);
        var skill = registry.GetSkillForEquipment("twin_swords");

        // Assert
        Assert.IsNotNull(skill);
        Assert.AreEqual("twin_swords", skill.Id);
        Assert.AreEqual("雌雄", skill.Name);
    }

    #endregion

    #region AfterCardTargetsDeclaredEvent Tests

    /// <summary>
    /// Tests that Twin Swords skill triggers for opposite gender target.
    /// </summary>
    [TestMethod]
    public void TwinSwordsSkillTriggersForOppositeGenderTarget()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var target = game.Players[1];

        // Set genders
        attacker = new Player
        {
            Seat = attacker.Seat,
            CampId = attacker.CampId,
            FactionId = attacker.FactionId,
            HeroId = attacker.HeroId,
            Gender = Gender.Male,
            MaxHealth = attacker.MaxHealth,
            CurrentHealth = attacker.CurrentHealth,
            IsAlive = attacker.IsAlive,
            HandZone = attacker.HandZone,
            EquipmentZone = attacker.EquipmentZone,
            JudgementZone = attacker.JudgementZone
        };
        game = new Game
        {
            Players = game.Players.Select((p, i) => i == 0 ? attacker : p).ToList(),
            DrawPile = game.DrawPile,
            DiscardPile = game.DiscardPile,
            CurrentPlayerSeat = game.CurrentPlayerSeat,
            CurrentPhase = game.CurrentPhase,
            TurnNumber = game.TurnNumber
        };

        target = game.Players[1];
        target = new Player
        {
            Seat = target.Seat,
            CampId = target.CampId,
            FactionId = target.FactionId,
            HeroId = target.HeroId,
            Gender = Gender.Female,
            MaxHealth = target.MaxHealth,
            CurrentHealth = target.CurrentHealth,
            IsAlive = target.IsAlive,
            HandZone = target.HandZone,
            EquipmentZone = target.EquipmentZone,
            JudgementZone = target.JudgementZone
        };
        game = new Game
        {
            Players = game.Players.Select((p, i) => i == 1 ? target : p).ToList(),
            DrawPile = game.DrawPile,
            DiscardPile = game.DiscardPile,
            CurrentPlayerSeat = game.CurrentPlayerSeat,
            CurrentPhase = game.CurrentPhase,
            TurnNumber = game.TurnNumber
        };

        attacker = game.Players[0];
        target = game.Players[1];

        var eventBus = new BasicEventBus();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("twin_swords", new TwinSwordsSkillFactory());
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);

        var twinSwordsSkill = equipmentSkillRegistry.GetSkillForEquipment("twin_swords");
        var cardMoveService = new BasicCardMoveService(eventBus);

        // Add hand card to target
        var targetCard = CreateTestCard(100);
        if (target.HandZone is Zone targetHand)
        {
            targetHand.MutableCards.Add(targetCard);
        }

        var initialTargetHandCount = target.HandZone.Cards.Count;
        var initialAttackerHandCount = attacker.HandZone.Cards.Count;

        // Set services on skill
        if (twinSwordsSkill is TwinSwordsSkill twinSwords)
        {
            twinSwords.SetCardMoveService(cardMoveService);
        }

        skillManager.AddEquipmentSkill(game, attacker, twinSwordsSkill);

        // Create Slash card
        var slash = CreateSlashCard(1);

        // Act: Publish AfterCardTargetsDeclaredEvent
        var afterTargetsDeclaredEvent = new AfterCardTargetsDeclaredEvent(
            game,
            SourcePlayerSeat: attacker.Seat,
            Card: slash,
            TargetSeats: new[] { target.Seat }
        );

        eventBus.Publish(afterTargetsDeclaredEvent);

        // Assert: Target should have discarded a card OR attacker should have drawn a card
        // (Since we auto-trigger, target should discard)
        var targetHandCountAfter = target.HandZone.Cards.Count;
        var attackerHandCountAfter = attacker.HandZone.Cards.Count;

        // Either target discarded a card OR attacker drew a card
        Assert.IsTrue(
            targetHandCountAfter < initialTargetHandCount || attackerHandCountAfter > initialAttackerHandCount,
            "Either target should have discarded a card or attacker should have drawn a card");
    }

    /// <summary>
    /// Tests that Twin Swords skill does not trigger for same gender target.
    /// </summary>
    [TestMethod]
    public void TwinSwordsSkillDoesNotTriggerForSameGenderTarget()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var target = game.Players[1];

        // Set same genders
        attacker = new Player
        {
            Seat = attacker.Seat,
            CampId = attacker.CampId,
            FactionId = attacker.FactionId,
            HeroId = attacker.HeroId,
            Gender = Gender.Male,
            MaxHealth = attacker.MaxHealth,
            CurrentHealth = attacker.CurrentHealth,
            IsAlive = attacker.IsAlive,
            HandZone = attacker.HandZone,
            EquipmentZone = attacker.EquipmentZone,
            JudgementZone = attacker.JudgementZone
        };
        game = new Game
        {
            Players = game.Players.Select((p, i) => i == 0 ? attacker : p).ToList(),
            DrawPile = game.DrawPile,
            DiscardPile = game.DiscardPile,
            CurrentPlayerSeat = game.CurrentPlayerSeat,
            CurrentPhase = game.CurrentPhase,
            TurnNumber = game.TurnNumber
        };

        target = game.Players[1];
        target = new Player
        {
            Seat = target.Seat,
            CampId = target.CampId,
            FactionId = target.FactionId,
            HeroId = target.HeroId,
            Gender = Gender.Male, // Same gender
            MaxHealth = target.MaxHealth,
            CurrentHealth = target.CurrentHealth,
            IsAlive = target.IsAlive,
            HandZone = target.HandZone,
            EquipmentZone = target.EquipmentZone,
            JudgementZone = target.JudgementZone
        };
        game = new Game
        {
            Players = game.Players.Select((p, i) => i == 1 ? target : p).ToList(),
            DrawPile = game.DrawPile,
            DiscardPile = game.DiscardPile,
            CurrentPlayerSeat = game.CurrentPlayerSeat,
            CurrentPhase = game.CurrentPhase,
            TurnNumber = game.TurnNumber
        };

        attacker = game.Players[0];
        target = game.Players[1];

        var eventBus = new BasicEventBus();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("twin_swords", new TwinSwordsSkillFactory());
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);

        var twinSwordsSkill = equipmentSkillRegistry.GetSkillForEquipment("twin_swords");
        var cardMoveService = new BasicCardMoveService(eventBus);

        // Add hand card to target
        var targetCard = CreateTestCard(100);
        if (target.HandZone is Zone targetHand)
        {
            targetHand.MutableCards.Add(targetCard);
        }

        var initialTargetHandCount = target.HandZone.Cards.Count;
        var initialAttackerHandCount = attacker.HandZone.Cards.Count;

        // Set services on skill
        if (twinSwordsSkill is TwinSwordsSkill twinSwords)
        {
            twinSwords.SetCardMoveService(cardMoveService);
        }

        skillManager.AddEquipmentSkill(game, attacker, twinSwordsSkill);

        // Create Slash card
        var slash = CreateSlashCard(1);

        // Act: Publish AfterCardTargetsDeclaredEvent
        var afterTargetsDeclaredEvent = new AfterCardTargetsDeclaredEvent(
            game,
            SourcePlayerSeat: attacker.Seat,
            Card: slash,
            TargetSeats: new[] { target.Seat }
        );

        eventBus.Publish(afterTargetsDeclaredEvent);

        // Assert: No changes should occur
        Assert.AreEqual(initialTargetHandCount, target.HandZone.Cards.Count,
            "Target should not have discarded cards (same gender)");
        Assert.AreEqual(initialAttackerHandCount, attacker.HandZone.Cards.Count,
            "Attacker should not have drawn cards (same gender)");
    }

    /// <summary>
    /// Tests that Twin Swords skill does not trigger when card is not Slash.
    /// </summary>
    [TestMethod]
    public void TwinSwordsSkillDoesNotTriggerWhenCardNotSlash()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var target = game.Players[1];

        // Set opposite genders
        attacker = new Player
        {
            Seat = attacker.Seat,
            CampId = attacker.CampId,
            FactionId = attacker.FactionId,
            HeroId = attacker.HeroId,
            Gender = Gender.Male,
            MaxHealth = attacker.MaxHealth,
            CurrentHealth = attacker.CurrentHealth,
            IsAlive = attacker.IsAlive,
            HandZone = attacker.HandZone,
            EquipmentZone = attacker.EquipmentZone,
            JudgementZone = attacker.JudgementZone
        };
        game = new Game
        {
            Players = game.Players.Select((p, i) => i == 0 ? attacker : p).ToList(),
            DrawPile = game.DrawPile,
            DiscardPile = game.DiscardPile,
            CurrentPlayerSeat = game.CurrentPlayerSeat,
            CurrentPhase = game.CurrentPhase,
            TurnNumber = game.TurnNumber
        };

        target = game.Players[1];
        target = new Player
        {
            Seat = target.Seat,
            CampId = target.CampId,
            FactionId = target.FactionId,
            HeroId = target.HeroId,
            Gender = Gender.Female,
            MaxHealth = target.MaxHealth,
            CurrentHealth = target.CurrentHealth,
            IsAlive = target.IsAlive,
            HandZone = target.HandZone,
            EquipmentZone = target.EquipmentZone,
            JudgementZone = target.JudgementZone
        };
        game = new Game
        {
            Players = game.Players.Select((p, i) => i == 1 ? target : p).ToList(),
            DrawPile = game.DrawPile,
            DiscardPile = game.DiscardPile,
            CurrentPlayerSeat = game.CurrentPlayerSeat,
            CurrentPhase = game.CurrentPhase,
            TurnNumber = game.TurnNumber
        };

        attacker = game.Players[0];
        target = game.Players[1];

        var eventBus = new BasicEventBus();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("twin_swords", new TwinSwordsSkillFactory());
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);

        var twinSwordsSkill = equipmentSkillRegistry.GetSkillForEquipment("twin_swords");
        var cardMoveService = new BasicCardMoveService(eventBus);

        // Set services on skill
        if (twinSwordsSkill is TwinSwordsSkill twinSwords)
        {
            twinSwords.SetCardMoveService(cardMoveService);
        }

        skillManager.AddEquipmentSkill(game, attacker, twinSwordsSkill);

        // Create non-Slash card
        var nonSlashCard = CreateTestCard(1, CardSubType.Peach);

        var initialTargetHandCount = target.HandZone.Cards.Count;
        var initialAttackerHandCount = attacker.HandZone.Cards.Count;

        // Act: Publish AfterCardTargetsDeclaredEvent with non-Slash card
        var afterTargetsDeclaredEvent = new AfterCardTargetsDeclaredEvent(
            game,
            SourcePlayerSeat: attacker.Seat,
            Card: nonSlashCard,
            TargetSeats: new[] { target.Seat }
        );

        eventBus.Publish(afterTargetsDeclaredEvent);

        // Assert: No changes should occur
        Assert.AreEqual(initialTargetHandCount, target.HandZone.Cards.Count,
            "Target should not have discarded cards (not Slash)");
        Assert.AreEqual(initialAttackerHandCount, attacker.HandZone.Cards.Count,
            "Attacker should not have drawn cards (not Slash)");
    }

    /// <summary>
    /// Tests that Twin Swords skill allows target to choose discarding hand card.
    /// </summary>
    [TestMethod]
    public void TwinSwordsSkillAllowsTargetToChooseDiscardingHandCard()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var target = game.Players[1];

        // Set opposite genders
        attacker = new Player
        {
            Seat = attacker.Seat,
            CampId = attacker.CampId,
            FactionId = attacker.FactionId,
            HeroId = attacker.HeroId,
            Gender = Gender.Male,
            MaxHealth = attacker.MaxHealth,
            CurrentHealth = attacker.CurrentHealth,
            IsAlive = attacker.IsAlive,
            HandZone = attacker.HandZone,
            EquipmentZone = attacker.EquipmentZone,
            JudgementZone = attacker.JudgementZone
        };
        game = new Game
        {
            Players = game.Players.Select((p, i) => i == 0 ? attacker : p).ToList(),
            DrawPile = game.DrawPile,
            DiscardPile = game.DiscardPile,
            CurrentPlayerSeat = game.CurrentPlayerSeat,
            CurrentPhase = game.CurrentPhase,
            TurnNumber = game.TurnNumber
        };

        target = game.Players[1];
        target = new Player
        {
            Seat = target.Seat,
            CampId = target.CampId,
            FactionId = target.FactionId,
            HeroId = target.HeroId,
            Gender = Gender.Female,
            MaxHealth = target.MaxHealth,
            CurrentHealth = target.CurrentHealth,
            IsAlive = target.IsAlive,
            HandZone = target.HandZone,
            EquipmentZone = target.EquipmentZone,
            JudgementZone = target.JudgementZone
        };
        game = new Game
        {
            Players = game.Players.Select((p, i) => i == 1 ? target : p).ToList(),
            DrawPile = game.DrawPile,
            DiscardPile = game.DiscardPile,
            CurrentPlayerSeat = game.CurrentPlayerSeat,
            CurrentPhase = game.CurrentPhase,
            TurnNumber = game.TurnNumber
        };

        attacker = game.Players[0];
        target = game.Players[1];

        var eventBus = new BasicEventBus();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("twin_swords", new TwinSwordsSkillFactory());
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);

        var twinSwordsSkill = equipmentSkillRegistry.GetSkillForEquipment("twin_swords");
        var cardMoveService = new BasicCardMoveService(eventBus);

        // Add hand card to target
        var targetCard = CreateTestCard(100);
        if (target.HandZone is Zone targetHand)
        {
            targetHand.MutableCards.Add(targetCard);
        }

        var initialTargetHandCount = target.HandZone.Cards.Count;
        var initialAttackerHandCount = attacker.HandZone.Cards.Count;

        // Setup getPlayerChoice to simulate target choosing to discard
        ChoiceRequest? lastTargetChoiceRequest = null;
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            if (request.PlayerSeat == attacker.Seat && request.ChoiceType == ChoiceType.Confirm)
            {
                // Attacker confirms activation
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: true
                );
            }
            else if (request.PlayerSeat == target.Seat && request.ChoiceType == ChoiceType.SelectCards)
            {
                // Target chooses to discard a card
                lastTargetChoiceRequest = request;
                var selectedId = request.AllowedCards?.FirstOrDefault()?.Id ?? 0;
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: selectedId > 0 ? new List<int> { selectedId } : null,
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
                Confirmed: false
            );
        };

        // Set services on skill
        if (twinSwordsSkill is TwinSwordsSkill twinSwords)
        {
            twinSwords.SetCardMoveService(cardMoveService);
            twinSwords.SetGetPlayerChoice(getPlayerChoice);
        }

        skillManager.AddEquipmentSkill(game, attacker, twinSwordsSkill);

        // Create Slash card
        var slash = CreateSlashCard(1);

        // Act: Publish AfterCardTargetsDeclaredEvent
        var afterTargetsDeclaredEvent = new AfterCardTargetsDeclaredEvent(
            game,
            SourcePlayerSeat: attacker.Seat,
            Card: slash,
            TargetSeats: new[] { target.Seat }
        );

        eventBus.Publish(afterTargetsDeclaredEvent);

        // Assert: Target should have discarded a card
        Assert.IsNotNull(lastTargetChoiceRequest, "Target should have been asked to choose");
        Assert.AreEqual(target.Seat, lastTargetChoiceRequest.PlayerSeat);
        Assert.IsTrue(target.HandZone.Cards.Count < initialTargetHandCount,
            "Target should have discarded a card");
        Assert.AreEqual(initialAttackerHandCount, attacker.HandZone.Cards.Count,
            "Attacker should not have drawn a card (target chose to discard)");
    }

    /// <summary>
    /// Tests that Twin Swords skill allows target to choose letting attacker draw.
    /// </summary>
    [TestMethod]
    public void TwinSwordsSkillAllowsTargetToChooseLettingAttackerDraw()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var target = game.Players[1];

        // Set opposite genders
        attacker = new Player
        {
            Seat = attacker.Seat,
            CampId = attacker.CampId,
            FactionId = attacker.FactionId,
            HeroId = attacker.HeroId,
            Gender = Gender.Male,
            MaxHealth = attacker.MaxHealth,
            CurrentHealth = attacker.CurrentHealth,
            IsAlive = attacker.IsAlive,
            HandZone = attacker.HandZone,
            EquipmentZone = attacker.EquipmentZone,
            JudgementZone = attacker.JudgementZone
        };
        game = new Game
        {
            Players = game.Players.Select((p, i) => i == 0 ? attacker : p).ToList(),
            DrawPile = game.DrawPile,
            DiscardPile = game.DiscardPile,
            CurrentPlayerSeat = game.CurrentPlayerSeat,
            CurrentPhase = game.CurrentPhase,
            TurnNumber = game.TurnNumber
        };

        target = game.Players[1];
        target = new Player
        {
            Seat = target.Seat,
            CampId = target.CampId,
            FactionId = target.FactionId,
            HeroId = target.HeroId,
            Gender = Gender.Female,
            MaxHealth = target.MaxHealth,
            CurrentHealth = target.CurrentHealth,
            IsAlive = target.IsAlive,
            HandZone = target.HandZone,
            EquipmentZone = target.EquipmentZone,
            JudgementZone = target.JudgementZone
        };
        game = new Game
        {
            Players = game.Players.Select((p, i) => i == 1 ? target : p).ToList(),
            DrawPile = game.DrawPile,
            DiscardPile = game.DiscardPile,
            CurrentPlayerSeat = game.CurrentPlayerSeat,
            CurrentPhase = game.CurrentPhase,
            TurnNumber = game.TurnNumber
        };

        attacker = game.Players[0];
        target = game.Players[1];

        // Add cards to draw pile
        var drawCard = CreateTestCard(200);
        if (game.DrawPile is Zone drawPile)
        {
            drawPile.MutableCards.Add(drawCard);
        }

        var eventBus = new BasicEventBus();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("twin_swords", new TwinSwordsSkillFactory());
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);

        var twinSwordsSkill = equipmentSkillRegistry.GetSkillForEquipment("twin_swords");
        var cardMoveService = new BasicCardMoveService(eventBus);

        // Add hand card to target
        var targetCard = CreateTestCard(100);
        if (target.HandZone is Zone targetHand)
        {
            targetHand.MutableCards.Add(targetCard);
        }

        var initialTargetHandCount = target.HandZone.Cards.Count;
        var initialAttackerHandCount = attacker.HandZone.Cards.Count;

        // Setup getPlayerChoice to simulate target choosing to let attacker draw (pass)
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            if (request.PlayerSeat == attacker.Seat && request.ChoiceType == ChoiceType.Confirm)
            {
                // Attacker confirms activation
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: true
                );
            }
            else if (request.PlayerSeat == target.Seat && request.ChoiceType == ChoiceType.SelectCards)
            {
                // Target chooses to pass (let attacker draw)
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null, // Pass = no cards selected
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
                Confirmed: false
            );
        };

        // Set services on skill
        if (twinSwordsSkill is TwinSwordsSkill twinSwords)
        {
            twinSwords.SetCardMoveService(cardMoveService);
            twinSwords.SetGetPlayerChoice(getPlayerChoice);
        }

        skillManager.AddEquipmentSkill(game, attacker, twinSwordsSkill);

        // Create Slash card
        var slash = CreateSlashCard(1);

        // Act: Publish AfterCardTargetsDeclaredEvent
        var afterTargetsDeclaredEvent = new AfterCardTargetsDeclaredEvent(
            game,
            SourcePlayerSeat: attacker.Seat,
            Card: slash,
            TargetSeats: new[] { target.Seat }
        );

        eventBus.Publish(afterTargetsDeclaredEvent);

        // Assert: Attacker should have drawn a card
        Assert.AreEqual(initialTargetHandCount, target.HandZone.Cards.Count,
            "Target should not have discarded cards (chose to let attacker draw)");
        Assert.IsTrue(attacker.HandZone.Cards.Count > initialAttackerHandCount,
            "Attacker should have drawn a card");
    }

    /// <summary>
    /// Tests that Twin Swords skill automatically lets attacker draw when target has no hand cards.
    /// </summary>
    [TestMethod]
    public void TwinSwordsSkillAutomaticallyLetsAttackerDrawWhenTargetHasNoHandCards()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var target = game.Players[1];

        // Set opposite genders
        attacker = new Player
        {
            Seat = attacker.Seat,
            CampId = attacker.CampId,
            FactionId = attacker.FactionId,
            HeroId = attacker.HeroId,
            Gender = Gender.Male,
            MaxHealth = attacker.MaxHealth,
            CurrentHealth = attacker.CurrentHealth,
            IsAlive = attacker.IsAlive,
            HandZone = attacker.HandZone,
            EquipmentZone = attacker.EquipmentZone,
            JudgementZone = attacker.JudgementZone
        };
        game = new Game
        {
            Players = game.Players.Select((p, i) => i == 0 ? attacker : p).ToList(),
            DrawPile = game.DrawPile,
            DiscardPile = game.DiscardPile,
            CurrentPlayerSeat = game.CurrentPlayerSeat,
            CurrentPhase = game.CurrentPhase,
            TurnNumber = game.TurnNumber
        };

        target = game.Players[1];
        target = new Player
        {
            Seat = target.Seat,
            CampId = target.CampId,
            FactionId = target.FactionId,
            HeroId = target.HeroId,
            Gender = Gender.Female,
            MaxHealth = target.MaxHealth,
            CurrentHealth = target.CurrentHealth,
            IsAlive = target.IsAlive,
            HandZone = target.HandZone,
            EquipmentZone = target.EquipmentZone,
            JudgementZone = target.JudgementZone
        };
        game = new Game
        {
            Players = game.Players.Select((p, i) => i == 1 ? target : p).ToList(),
            DrawPile = game.DrawPile,
            DiscardPile = game.DiscardPile,
            CurrentPlayerSeat = game.CurrentPlayerSeat,
            CurrentPhase = game.CurrentPhase,
            TurnNumber = game.TurnNumber
        };

        attacker = game.Players[0];
        target = game.Players[1];

        // Add cards to draw pile
        var drawCard = CreateTestCard(200);
        if (game.DrawPile is Zone drawPile)
        {
            drawPile.MutableCards.Add(drawCard);
        }

        var eventBus = new BasicEventBus();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("twin_swords", new TwinSwordsSkillFactory());
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);

        var twinSwordsSkill = equipmentSkillRegistry.GetSkillForEquipment("twin_swords");
        var cardMoveService = new BasicCardMoveService(eventBus);

        // Ensure target has no hand cards
        if (target.HandZone is Zone targetHand)
        {
            targetHand.MutableCards.Clear();
        }

        var initialTargetHandCount = target.HandZone.Cards.Count;
        var initialAttackerHandCount = attacker.HandZone.Cards.Count;

        // Set services on skill (auto-trigger)
        if (twinSwordsSkill is TwinSwordsSkill twinSwords)
        {
            twinSwords.SetCardMoveService(cardMoveService);
        }

        skillManager.AddEquipmentSkill(game, attacker, twinSwordsSkill);

        // Create Slash card
        var slash = CreateSlashCard(1);

        // Act: Publish AfterCardTargetsDeclaredEvent
        var afterTargetsDeclaredEvent = new AfterCardTargetsDeclaredEvent(
            game,
            SourcePlayerSeat: attacker.Seat,
            Card: slash,
            TargetSeats: new[] { target.Seat }
        );

        eventBus.Publish(afterTargetsDeclaredEvent);

        // Assert: Attacker should have drawn a card (automatic choice when target has no hand cards)
        Assert.AreEqual(0, initialTargetHandCount, "Target should have no hand cards initially");
        Assert.IsTrue(attacker.HandZone.Cards.Count > initialAttackerHandCount,
            "Attacker should have drawn a card (target has no hand cards)");
    }

    /// <summary>
    /// Tests that Twin Swords skill does not trigger when attacker chooses not to activate.
    /// </summary>
    [TestMethod]
    public void TwinSwordsSkillDoesNotTriggerWhenAttackerChoosesNotToActivate()
    {
        // Arrange
        var game = CreateDefaultGame(2);
        var attacker = game.Players[0];
        var target = game.Players[1];

        // Set opposite genders
        attacker = new Player
        {
            Seat = attacker.Seat,
            CampId = attacker.CampId,
            FactionId = attacker.FactionId,
            HeroId = attacker.HeroId,
            Gender = Gender.Male,
            MaxHealth = attacker.MaxHealth,
            CurrentHealth = attacker.CurrentHealth,
            IsAlive = attacker.IsAlive,
            HandZone = attacker.HandZone,
            EquipmentZone = attacker.EquipmentZone,
            JudgementZone = attacker.JudgementZone
        };
        game = new Game
        {
            Players = game.Players.Select((p, i) => i == 0 ? attacker : p).ToList(),
            DrawPile = game.DrawPile,
            DiscardPile = game.DiscardPile,
            CurrentPlayerSeat = game.CurrentPlayerSeat,
            CurrentPhase = game.CurrentPhase,
            TurnNumber = game.TurnNumber
        };

        target = game.Players[1];
        target = new Player
        {
            Seat = target.Seat,
            CampId = target.CampId,
            FactionId = target.FactionId,
            HeroId = target.HeroId,
            Gender = Gender.Female,
            MaxHealth = target.MaxHealth,
            CurrentHealth = target.CurrentHealth,
            IsAlive = target.IsAlive,
            HandZone = target.HandZone,
            EquipmentZone = target.EquipmentZone,
            JudgementZone = target.JudgementZone
        };
        game = new Game
        {
            Players = game.Players.Select((p, i) => i == 1 ? target : p).ToList(),
            DrawPile = game.DrawPile,
            DiscardPile = game.DiscardPile,
            CurrentPlayerSeat = game.CurrentPlayerSeat,
            CurrentPhase = game.CurrentPhase,
            TurnNumber = game.TurnNumber
        };

        attacker = game.Players[0];
        target = game.Players[1];

        var eventBus = new BasicEventBus();
        var equipmentSkillRegistry = new EquipmentSkillRegistry();
        equipmentSkillRegistry.RegisterEquipmentSkill("twin_swords", new TwinSwordsSkillFactory());
        var skillManager = new SkillManager(new SkillRegistry(), eventBus);

        var twinSwordsSkill = equipmentSkillRegistry.GetSkillForEquipment("twin_swords");
        var cardMoveService = new BasicCardMoveService(eventBus);

        // Add hand card to target
        var targetCard = CreateTestCard(100);
        if (target.HandZone is Zone targetHand)
        {
            targetHand.MutableCards.Add(targetCard);
        }

        var initialTargetHandCount = target.HandZone.Cards.Count;
        var initialAttackerHandCount = attacker.HandZone.Cards.Count;

        // Setup getPlayerChoice to decline activation
        Func<ChoiceRequest, ChoiceResult> getPlayerChoice = (request) =>
        {
            if (request.PlayerSeat == attacker.Seat && request.ChoiceType == ChoiceType.Confirm)
            {
                // Attacker declines activation
                return new ChoiceResult(
                    RequestId: request.RequestId,
                    PlayerSeat: request.PlayerSeat,
                    SelectedTargetSeats: null,
                    SelectedCardIds: null,
                    SelectedOptionId: null,
                    Confirmed: false // Decline
                );
            }
            return new ChoiceResult(
                RequestId: request.RequestId,
                PlayerSeat: request.PlayerSeat,
                SelectedTargetSeats: null,
                SelectedCardIds: null,
                SelectedOptionId: null,
                Confirmed: false
            );
        };

        // Set services on skill
        if (twinSwordsSkill is TwinSwordsSkill twinSwords)
        {
            twinSwords.SetCardMoveService(cardMoveService);
            twinSwords.SetGetPlayerChoice(getPlayerChoice);
        }

        skillManager.AddEquipmentSkill(game, attacker, twinSwordsSkill);

        // Create Slash card
        var slash = CreateSlashCard(1);

        // Act: Publish AfterCardTargetsDeclaredEvent
        var afterTargetsDeclaredEvent = new AfterCardTargetsDeclaredEvent(
            game,
            SourcePlayerSeat: attacker.Seat,
            Card: slash,
            TargetSeats: new[] { target.Seat }
        );

        eventBus.Publish(afterTargetsDeclaredEvent);

        // Assert: No changes should occur
        Assert.AreEqual(initialTargetHandCount, target.HandZone.Cards.Count,
            "Target should not have discarded cards (attacker declined)");
        Assert.AreEqual(initialAttackerHandCount, attacker.HandZone.Cards.Count,
            "Attacker should not have drawn cards (attacker declined)");
    }

    #endregion
}

