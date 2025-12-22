# 装备技能查找的简化方案（添加 Name 属性）

## 方案概述

在 `Card` 中添加 `Name` 属性用于显示，同时保持现有的按 `DefinitionId` 查找机制。

## 详细修改

### 1. 修改 `Card` (`core/Model/Card.cs`)

**添加 `Name` 属性：**
```csharp
public sealed class Card
{
    public int Id { get; init; }
    public string DefinitionId { get; init; } = string.Empty;
    
    /// <summary>
    /// Display name of the card (e.g. "赤兔", "的卢").
    /// Used for UI display purposes.
    /// </summary>
    public string Name { get; init; } = string.Empty;
    
    public Suit Suit { get; init; }
    public int Rank { get; init; }
    public CardType CardType { get; init; }
    public CardSubType CardSubType { get; init; } = CardSubType.Unknown;
}
```

### 2. 修改测试文件

**`core.Tests/OffensiveHorseTests.cs`：**
```csharp
private static Card CreateOffensiveHorseCard(int id = 1, string definitionId = "chitu", string name = "赤兔")
{
    return new Card
    {
        Id = id,
        DefinitionId = definitionId,  // 使用具体牌名作为技术标识符
        Name = name,                 // 显示名称
        CardType = CardType.Equip,
        CardSubType = CardSubType.OffensiveHorse,
        Suit = Suit.Spade,
        Rank = 5
    };
}
```

**注册技能时：**
```csharp
// 为每张具体的牌注册技能（如果多张牌共享技能，需要多次注册）
registry.RegisterEquipmentSkill("chitu", factory);
// 如果有其他进攻马牌，也需要注册
// registry.RegisterEquipmentSkill("other_offensive_horse", factory);
```

## 优点

1. **最小修改**：只需添加一个属性
2. **向后兼容**：现有代码无需修改
3. **显示分离**：技术标识符和显示名称分离

## 缺点

1. **需要重复注册**：如果有多张进攻马牌（赤兔、其他），需要为每张牌单独注册相同的技能工厂
2. **不符合实际游戏**：实际游戏中同一类别装备应该共享技能

## 需要修改的文件

1. `core/Model/Card.cs` - 添加 `Name` 属性
2. `core.Tests/OffensiveHorseTests.cs` - 更新测试，使用具体牌名和 Name
3. `core.Tests/DefensiveHorseTests.cs` - 更新测试，使用具体牌名和 Name
4. `core/GameSetup/Basic.cs` - 如果创建卡牌时需要设置 Name（如果有的话）

## 注意事项

如果未来有多张同一类别的装备牌（如多张进攻马），需要为每张牌都注册相同的技能工厂。这可以通过循环注册来实现：

```csharp
var factory = new OffensiveHorseSkillFactory();
var offensiveHorseCards = new[] { "chitu", "other_horse" }; // 所有进攻马牌的 DefinitionId
foreach (var cardId in offensiveHorseCards)
{
    registry.RegisterEquipmentSkill(cardId, factory);
}
```

