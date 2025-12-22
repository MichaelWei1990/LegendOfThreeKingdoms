# 装备技能按 CardSubType 查找的修改方案

## 问题描述

当前系统使用 `DefinitionId`（具体牌名）来查找装备技能，但实际游戏中：
- 进攻马是一个类别（`CardSubType.OffensiveHorse`），具体牌有"赤兔"（chitu）等
- 防御马是一个类别（`CardSubType.DefensiveHorse`），具体牌有"的卢"（dilu）等
- 同一类别的装备应该共享相同的技能

测试中使用 `"offensive_horse"` 作为 DefinitionId 是不正确的，应该使用具体的牌名如 `"chitu"`。

## 修改方案

### 方案概述

采用**混合查找策略**：
1. **优先按 DefinitionId 查找**：支持特殊装备有独特的技能（向后兼容）
2. **回退到按 CardSubType 查找**：如果 DefinitionId 没有找到，则按 CardSubType 查找（用于同一类别装备共享技能）

### 详细修改

#### 1. 修改 `EquipmentSkillRegistry` (`core/Skills/EquipmentSkillRegistry.cs`)

**新增功能：**
- 添加基于 `CardSubType` 的注册和查找方法
- 保持现有的基于 `DefinitionId` 的方法（向后兼容）

**新增方法：**
```csharp
// 按 CardSubType 注册技能
public void RegisterEquipmentSkillBySubType(CardSubType cardSubType, IEquipmentSkillFactory factory)

// 按 CardSubType 查找技能
public ISkill? GetSkillForEquipmentBySubType(CardSubType cardSubType)

// 检查 CardSubType 是否已注册
public bool HasEquipmentSkillBySubType(CardSubType cardSubType)
```

**内部数据结构：**
```csharp
private readonly Dictionary<string, IEquipmentSkillFactory> _equipmentSkillFactories = new(); // 保持现有
private readonly Dictionary<CardSubType, IEquipmentSkillFactory> _subTypeSkillFactories = new(); // 新增
```

#### 2. 修改 `EquipResolver` (`core/Resolution/EquipResolver.cs`)

**修改 `LoadEquipmentSkill` 方法：**
```csharp
private static void LoadEquipmentSkill(ResolutionContext context, Card card)
{
    if (context.SkillManager is null || context.EquipmentSkillRegistry is null)
        return;

    // 优先按 DefinitionId 查找（支持特殊装备）
    var equipmentSkill = context.EquipmentSkillRegistry.GetSkillForEquipment(card.DefinitionId);
    
    // 如果没找到，按 CardSubType 查找（同一类别装备共享技能）
    if (equipmentSkill is null)
    {
        equipmentSkill = context.EquipmentSkillRegistry.GetSkillForEquipmentBySubType(card.CardSubType);
    }
    
    if (equipmentSkill is not null)
    {
        context.SkillManager.AddEquipmentSkill(context.Game, context.SourcePlayer, equipmentSkill);
    }
}
```

**修改 `RemoveEquipmentSkill` 方法：**
```csharp
private static void RemoveEquipmentSkill(ResolutionContext context, Card equipment)
{
    if (context.SkillManager is null || context.EquipmentSkillRegistry is null)
        return;

    // 优先按 DefinitionId 查找
    var oldSkill = context.EquipmentSkillRegistry.GetSkillForEquipment(equipment.DefinitionId);
    
    // 如果没找到，按 CardSubType 查找
    if (oldSkill is null)
    {
        oldSkill = context.EquipmentSkillRegistry.GetSkillForEquipmentBySubType(equipment.CardSubType);
    }
    
    if (oldSkill is not null)
    {
        context.SkillManager.RemoveEquipmentSkill(context.Game, context.SourcePlayer, oldSkill.Id);
    }
}
```

#### 3. 修改测试文件

**`core.Tests/OffensiveHorseTests.cs`：**
- 修改 `CreateOffensiveHorseCard` 方法：使用具体牌名 `"chitu"` 作为默认 DefinitionId
- 修改测试中的注册：使用 `RegisterEquipmentSkillBySubType(CardSubType.OffensiveHorse, factory)` 而不是按 DefinitionId 注册
- 更新测试注释，说明使用具体牌名

**`core.Tests/DefensiveHorseTests.cs`：**
- 修改 `CreateDefensiveHorseCard` 方法：使用具体牌名 `"dilu"` 作为默认 DefinitionId
- 修改测试中的注册：使用 `RegisterEquipmentSkillBySubType(CardSubType.DefensiveHorse, factory)` 而不是按 DefinitionId 注册
- 更新测试注释，说明使用具体牌名

### 修改后的使用方式

**注册装备技能（按类别）：**
```csharp
var registry = new EquipmentSkillRegistry();
var factory = new OffensiveHorseSkillFactory();

// 所有进攻马牌（赤兔、其他进攻马）共享同一个技能
registry.RegisterEquipmentSkillBySubType(CardSubType.OffensiveHorse, factory);
```

**创建测试卡牌：**
```csharp
// 使用具体的牌名
var chitu = CreateOffensiveHorseCard(definitionId: "chitu");
var dilu = CreateDefensiveHorseCard(definitionId: "dilu");
```

**查找逻辑（自动）：**
```csharp
// EquipResolver 会自动：
// 1. 先查找 "chitu" 的 DefinitionId 注册（如果有）
// 2. 如果没找到，查找 CardSubType.OffensiveHorse 的注册
// 3. 找到后加载技能
```

## 优势

1. **向后兼容**：保持现有的按 DefinitionId 注册方式，特殊装备仍可使用
2. **符合实际游戏**：同一类别装备共享技能，无需为每张牌单独注册
3. **灵活性**：支持特殊装备覆盖类别技能（如果某张牌有特殊技能，可以按 DefinitionId 注册）
4. **测试更真实**：测试中使用真实的牌名（如 "chitu"），而不是虚拟的 "offensive_horse"

## 需要修改的文件清单

1. `core/Skills/EquipmentSkillRegistry.cs` - 添加按 CardSubType 的注册和查找方法
2. `core/Resolution/EquipResolver.cs` - 修改查找逻辑，支持混合查找
3. `core.Tests/OffensiveHorseTests.cs` - 更新测试，使用具体牌名和按 SubType 注册
4. `core.Tests/DefensiveHorseTests.cs` - 更新测试，使用具体牌名和按 SubType 注册

## 注意事项

1. `Clear()` 方法需要同时清空两个字典
2. 测试中需要确保注册方式正确（按 SubType 而不是 DefinitionId）
3. 如果未来有特殊装备需要覆盖类别技能，可以同时注册 DefinitionId 和 SubType，DefinitionId 会优先匹配

