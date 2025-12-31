using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfThreeKingdoms.Core.Events;
using LegendOfThreeKingdoms.Core.Model;
using LegendOfThreeKingdoms.Core.Skills;

namespace LegendOfThreeKingdoms.Core.Character;

/// <summary>
/// Basic implementation of ICharacterCatalog.
/// Builds character definitions from SkillRegistry and HeroMetadata.
/// </summary>
public sealed class BasicCharacterCatalog : ICharacterCatalog
{
    private readonly SkillRegistry _skillRegistry;
    private readonly Dictionary<string, CharacterDefinition> _characters = new();

    /// <summary>
    /// Creates a new BasicCharacterCatalog.
    /// </summary>
    /// <param name="skillRegistry">The skill registry to use for building character definitions.</param>
    /// <exception cref="ArgumentNullException">Thrown if skillRegistry is null.</exception>
    public BasicCharacterCatalog(SkillRegistry skillRegistry)
    {
        _skillRegistry = skillRegistry ?? throw new ArgumentNullException(nameof(skillRegistry));
        BuildCharacterDefinitions();
    }

    /// <inheritdoc />
    public CharacterDefinition? GetCharacter(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
            return null;

        // Check cache first
        if (_characters.TryGetValue(characterId, out var cached))
            return cached;

        // Build definition on-demand
        var definition = BuildCharacterDefinition(characterId);
        if (definition is not null)
        {
            _characters[characterId] = definition;
        }

        return definition;
    }

    /// <inheritdoc />
    public IEnumerable<CharacterDefinition> GetAllCharacters()
    {
        return _characters.Values;
    }

    /// <inheritdoc />
    public bool HasCharacter(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
            return false;

        // Check cache first
        if (_characters.ContainsKey(characterId))
            return true;

        // Try to build definition to check if it exists
        var definition = BuildCharacterDefinition(characterId);
        if (definition is not null)
        {
            _characters[characterId] = definition;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Builds character definitions from the skill registry.
    /// For each hero that has registered skills, creates a CharacterDefinition.
    /// Since SkillRegistry doesn't expose all hero IDs directly, we build definitions on-demand.
    /// </summary>
    private void BuildCharacterDefinitions()
    {
        // Character definitions are built on-demand in GetCharacter method
        // This allows us to work with the current SkillRegistry structure
    }

    /// <summary>
    /// Builds a character definition for a specific hero ID.
    /// </summary>
    private CharacterDefinition? BuildCharacterDefinition(string heroId)
    {
        if (string.IsNullOrWhiteSpace(heroId))
            return null;

        // Check if hero has skills registered
        if (!_skillRegistry.HasHeroSkills(heroId))
            return null;

        // Get metadata
        var metadata = _skillRegistry.GetHeroMetadata(heroId);
        if (metadata is null)
            return null;

        // Get skills
        var skills = _skillRegistry.GetSkillsForHero(heroId).ToList();
        var skillRefs = new List<SkillDefinitionRef>();
        
        foreach (var skill in skills)
        {
            skillRefs.Add(new SkillDefinitionRef
            {
                SkillId = skill.Id,
                IsLordSkill = skill is ILordSkill
            });
        }

        // Extract faction from hero ID (simplified - in a full implementation,
        // this might be stored in metadata or a separate mapping)
        // For now, we'll try to infer from common patterns or leave it null
        string? factionId = InferFactionFromHeroId(heroId);

        return new CharacterDefinition
        {
            CharacterId = heroId,
            Name = GetCharacterName(heroId), // In a full implementation, this would come from a name mapping
            FactionId = factionId,
            Gender = metadata.Gender,
            MaxHp = metadata.MaxHealth,
            Skills = skillRefs
        };
    }

    /// <summary>
    /// Infers faction from hero ID based on common patterns.
    /// This is a simplified implementation - in a full system, faction would be stored explicitly.
    /// </summary>
    private static string? InferFactionFromHeroId(string heroId)
    {
        // This is a simplified heuristic - in a full implementation,
        // faction should be stored in metadata or a separate mapping
        // For now, we'll check known hero IDs from registration files
        
        // Known Shu heroes
        if (heroId == "liubei" || heroId == "guanyu" || heroId == "zhangfei" ||
            heroId == "zhugeliang" || heroId == "zhaoyun" || heroId == "machao" ||
            heroId == "huangyueying")
            return "Shu";
        
        // Known Wei heroes
        if (heroId == "caocao" || heroId == "simayi" || heroId == "xiahoudun" ||
            heroId == "zhangliao" || heroId == "xuchu" || heroId == "guojia" ||
            heroId == "zhenji")
            return "Wei";
        
        // Known Wu heroes
        if (heroId == "sunquan" || heroId == "ganning" || heroId == "lvmeng" ||
            heroId == "huanggai" || heroId == "zhouyu" || heroId == "daqiao" ||
            heroId == "luxun" || heroId == "sunshangxiang")
            return "Wu";
        
        // Known Qun heroes
        if (heroId == "huatuo" || heroId == "lubu" || heroId == "diaochan")
            return "Qun";
        
        return null;
    }

    /// <summary>
    /// Gets character name from hero ID.
    /// This is a simplified implementation - in a full system, names would be stored in metadata.
    /// </summary>
    private static string GetCharacterName(string heroId)
    {
        // Simplified name mapping - in a full implementation, this would come from metadata
        return heroId switch
        {
            "liubei" => "刘备",
            "guanyu" => "关羽",
            "zhangfei" => "张飞",
            "zhugeliang" => "诸葛亮",
            "zhaoyun" => "赵云",
            "machao" => "马超",
            "huangyueying" => "黄月英",
            "caocao" => "曹操",
            "simayi" => "司马懿",
            "xiahoudun" => "夏侯惇",
            "zhangliao" => "张辽",
            "xuchu" => "许褚",
            "guojia" => "郭嘉",
            "zhenji" => "甄姬",
            "sunquan" => "孙权",
            "ganning" => "甘宁",
            "lvmeng" => "吕蒙",
            "huanggai" => "黄盖",
            "zhouyu" => "周瑜",
            "daqiao" => "大乔",
            "luxun" => "陆逊",
            "sunshangxiang" => "孙尚香",
            "huatuo" => "华佗",
            "lubu" => "吕布",
            "diaochan" => "貂蝉",
            "test_hero" => "测试武将",
            "test_hero2" => "测试武将2",
            _ => heroId
        };
    }
}

/// <summary>
/// Basic implementation of ICharacterSelectionService.
/// Handles character selection process including validation, attribute initialization, and skill registration.
/// </summary>
public sealed class BasicCharacterSelectionService : ICharacterSelectionService
{
    private readonly ICharacterCatalog _catalog;
    private readonly SkillManager _skillManager;
    private readonly IEventBus _eventBus;
    private readonly SelectionContext _selectionContext;

    /// <summary>
    /// Creates a new BasicCharacterSelectionService.
    /// </summary>
    /// <param name="catalog">The character catalog to use for querying character definitions.</param>
    /// <param name="skillManager">The skill manager to use for registering skills.</param>
    /// <param name="eventBus">The event bus to use for publishing events.</param>
    /// <exception cref="ArgumentNullException">Thrown if any parameter is null.</exception>
    public BasicCharacterSelectionService(
        ICharacterCatalog catalog,
        SkillManager skillManager,
        IEventBus eventBus)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _skillManager = skillManager ?? throw new ArgumentNullException(nameof(skillManager));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _selectionContext = new SelectionContext();
    }

    /// <inheritdoc />
    public void OfferCharacters(Game game, int playerSeat, IReadOnlyList<string> candidateCharacterIds)
    {
        if (game is null)
            throw new ArgumentNullException(nameof(game));
        if (candidateCharacterIds is null)
            throw new ArgumentNullException(nameof(candidateCharacterIds));
        if (candidateCharacterIds.Count == 0)
            throw new ArgumentException("Candidate character IDs list cannot be empty.", nameof(candidateCharacterIds));

        // Validate all candidate IDs exist in catalog
        foreach (var characterId in candidateCharacterIds)
        {
            if (!_catalog.HasCharacter(characterId))
            {
                throw new ArgumentException($"Character ID '{characterId}' is not found in catalog.", nameof(candidateCharacterIds));
            }
        }

        // Store candidates in selection context
        var playerState = _selectionContext.GetOrCreatePlayerState(playerSeat);
        playerState.CandidateCharacterIds.Clear();
        playerState.CandidateCharacterIds.AddRange(candidateCharacterIds);

        // Publish event
        var offerEvent = new CharactersOfferedEvent(game, playerSeat, candidateCharacterIds);
        _eventBus.Publish(offerEvent);
    }

    /// <inheritdoc />
    public void SelectCharacter(Game game, int playerSeat, string selectedCharacterId)
    {
        if (game is null)
            throw new ArgumentNullException(nameof(game));
        if (string.IsNullOrWhiteSpace(selectedCharacterId))
            throw new ArgumentException("Selected character ID cannot be null or empty.", nameof(selectedCharacterId));

        // Get player
        var player = game.Players.FirstOrDefault(p => p.Seat == playerSeat);
        if (player is null)
            throw new ArgumentException($"Player with seat {playerSeat} not found.", nameof(playerSeat));

        // Get selection state
        var playerState = _selectionContext.GetPlayerState(playerSeat);
        if (playerState is null)
            throw new InvalidOperationException($"No candidates have been offered to player at seat {playerSeat}.");

        // Validate selection is in candidates
        if (!playerState.CandidateCharacterIds.Contains(selectedCharacterId))
        {
            throw new ArgumentException($"Selected character ID '{selectedCharacterId}' is not in the candidate list.", nameof(selectedCharacterId));
        }

        // Validate player hasn't already selected
        if (!string.IsNullOrWhiteSpace(playerState.SelectedCharacterId))
        {
            throw new InvalidOperationException($"Player at seat {playerSeat} has already selected a character.");
        }

        // Get character definition
        var characterDef = _catalog.GetCharacter(selectedCharacterId);
        if (characterDef is null)
            throw new InvalidOperationException($"Character definition for '{selectedCharacterId}' not found.");

        // Create updated player with character properties
        var updatedPlayer = CreatePlayerWithCharacter(player, characterDef);

        // Update game with new player
        var updatedGame = UpdateGameWithPlayer(game, updatedPlayer);

        // Mark as selected
        playerState.SelectedCharacterId = selectedCharacterId;

        // Register skills (SkillManager will handle conditional Lord skill registration)
        // Note: We use the updated player from the updated game
        var playerFromUpdatedGame = updatedGame.Players.FirstOrDefault(p => p.Seat == playerSeat);
        if (playerFromUpdatedGame is null)
        {
            throw new InvalidOperationException($"Player at seat {playerSeat} not found in updated game.");
        }
        
        _skillManager.LoadSkillsForPlayer(updatedGame, playerFromUpdatedGame);

        // Get registered skill IDs
        var registeredSkills = _skillManager.GetAllSkills(playerFromUpdatedGame).Select(s => s.Id).ToList();

        // Publish events (use updated game)
        var selectedEvent = new CharacterSelectedEvent(updatedGame, playerSeat, selectedCharacterId);
        _eventBus.Publish(selectedEvent);

        var skillsRegisteredEvent = new SkillsRegisteredEvent(updatedGame, playerSeat, registeredSkills);
        _eventBus.Publish(skillsRegisteredEvent);
    }

    /// <summary>
    /// Creates a new Player instance with character properties bound.
    /// Preserves Flags dictionary from original player.
    /// </summary>
    private static Player CreatePlayerWithCharacter(Player originalPlayer, CharacterDefinition characterDef)
    {
        var newPlayer = new Player
        {
            Seat = originalPlayer.Seat,
            CampId = originalPlayer.CampId,
            FactionId = characterDef.FactionId,
            HeroId = characterDef.CharacterId,
            Gender = characterDef.Gender,
            MaxHealth = characterDef.MaxHp,
            CurrentHealth = characterDef.MaxHp, // Start at max health
            IsAlive = originalPlayer.IsAlive,
            HandZone = originalPlayer.HandZone,
            EquipmentZone = originalPlayer.EquipmentZone,
            JudgementZone = originalPlayer.JudgementZone
        };
        
        // Copy flags from original player
        foreach (var flag in originalPlayer.Flags)
        {
            newPlayer.Flags[flag.Key] = flag.Value;
        }
        
        return newPlayer;
    }

    /// <summary>
    /// Updates the game with a new player instance.
    /// Since Game.Players is IReadOnlyList, we need to create a new Game instance.
    /// </summary>
    private static Game UpdateGameWithPlayer(Game originalGame, Player updatedPlayer)
    {
        var updatedPlayers = originalGame.Players
            .Select(p => p.Seat == updatedPlayer.Seat ? updatedPlayer : p)
            .ToArray();

        return new Game
        {
            Players = updatedPlayers,
            CurrentPlayerSeat = originalGame.CurrentPlayerSeat,
            CurrentPhase = originalGame.CurrentPhase,
            TurnNumber = originalGame.TurnNumber,
            DrawPile = originalGame.DrawPile,
            DiscardPile = originalGame.DiscardPile,
            IsFinished = originalGame.IsFinished,
            WinnerDescription = originalGame.WinnerDescription
        };
    }
}
