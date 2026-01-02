# 三国杀基本版缺失卡牌清单

## 评估时间
2025-01-01

## 基本版卡牌总数
108张游戏牌

---

## 已实现的卡牌

### ✅ 基本牌（3/3，100%）
- **杀** (Slash) - `SlashResolver` ✅
- **闪** (Dodge) - 响应系统支持 ✅
- **桃** (Peach) - `PeachResolver` ✅

### ✅ 装备牌（13/13，100%）
所有基本版装备牌都已实现，包括：
- **武器**：
  - 诸葛连弩 (`ZhugeCrossbowSkill`) ✅
  - 青釭剑 (`QinggangSwordSkill`) ✅
  - 雌雄双股剑 (`TwinSwordsSkill`) ✅
  - 寒冰剑 (`IceSwordSkill`) ✅
  - 贯石斧 (`StoneAxeSkill`) ✅
  - 青龙偃月刀 (`QinglongYanyueDaoSkill`) ✅
  - 丈八蛇矛 (`SerpentSpearSkill`) ✅
  - 方天画戟 (`FangTianHuaJiSkill`) ✅
  - 麒麟弓 (`KirinBowSkill`) ✅
- **防具**：
  - 八卦阵 (`BaguaArraySkill`) ✅
  - 仁王盾 (`RenwangShieldSkill`) ✅
- **坐骑**：
  - +1马 (`DefensiveHorseSkill`) ✅
  - -1马 (`OffensiveHorseSkill`) ✅

### ✅ 锦囊牌（11/12，92%）
已实现的锦囊牌：
- **即时锦囊**：
  - 无中生有 (`WuzhongShengyouResolver`) ✅
  - 桃园结义 (`TaoyuanJieyiResolver`) ✅
  - 顺手牵羊 (`ShunshouQianyangResolver`) ✅
  - 过河拆桥 (`GuoheChaiqiaoResolver`) ✅
  - 万箭齐发 (`WanjianqifaResolver`) ✅
  - 南蛮入侵 (`NanmanRushinResolver`) ✅
  - 决斗 (`DuelResolver`) ✅
  - 五谷丰登 (`HarvestResolver`) ✅
- **延时锦囊**：
  - 乐不思蜀 (`LebusishuResolver`) ✅
  - 闪电 (`ShandianResolver`) ✅
- **响应牌**：
  - 无懈可击 (`NullificationHelper`) ✅

---

## ❌ 缺失的卡牌

### 锦囊牌（1张）

#### 1. 借刀杀人 (Jiedao Sharen)
- **卡牌类型**：即时锦囊
- **数量**：2张
- **效果**：出牌阶段，对一名装备区有武器的角色使用。该角色需对其攻击范围内你指定的另一名角色使用一张【杀】，否则将其装备区里的武器交给你。
- **当前状态**：
  - ❌ `CardSubType` 枚举中未定义
  - ❌ 没有对应的解析器 (`JiedaoShaRenResolver`)
  - ❌ 没有动作处理器注册方法 (`RegisterUseJiedaoShaRenHandler`)
  - ❌ `ImmediateTrickResolver` 中没有分发逻辑

---

## 动作处理器注册情况

### 已注册的动作处理器
- `UseSlash` - 杀 ✅

### 未注册但已实现的解析器
以下卡牌有解析器实现，但**缺少动作处理器注册**，需要在 `CreateFlowService` 或类似地方注册：

- `UsePeach` - 桃（有 `PeachResolver`，但未注册处理器）
- `UseWuzhongShengyou` - 无中生有（有 `WuzhongShengyouResolver`，通过 `ImmediateTrickResolver` 分发）
- `UseTaoyuanJieyi` - 桃园结义（有 `TaoyuanJieyiResolver`，通过 `ImmediateTrickResolver` 分发）
- `UseShunshouQianyang` - 顺手牵羊（有 `ShunshouQianyangResolver`，通过 `ImmediateTrickResolver` 分发）
- `UseGuoheChaiqiao` - 过河拆桥（有 `GuoheChaiqiaoResolver`，通过 `ImmediateTrickResolver` 分发）
- `UseWanjianQifa` - 万箭齐发（有 `WanjianqifaResolver`，通过 `ImmediateTrickResolver` 分发）
- `UseNanmanRushin` - 南蛮入侵（有 `NanmanRushinResolver`，通过 `ImmediateTrickResolver` 分发）
- `UseDuel` - 决斗（有 `DuelResolver`，通过 `ImmediateTrickResolver` 分发）
- `UseHarvest` - 五谷丰登（有 `HarvestResolver`，通过 `ImmediateTrickResolver` 分发）
- `UseLebusishu` - 乐不思蜀（有 `LebusishuResolver`，通过 `DelayedTrickJudgementResolver` 处理）
- `UseShandian` - 闪电（有 `ShandianResolver`，通过 `DelayedTrickJudgementResolver` 处理）
- `UseEquip` - 装备（有 `EquipResolver`，但未注册处理器）

**注意**：大部分锦囊牌通过 `UseCardResolver` → `ImmediateTrickResolver` 的通用流程处理，不需要单独注册动作处理器。但需要确保 `ActionIdMapper` 中有正确的映射。

---

## 总结

### 完成度统计
- **基本牌**：100% (3/3)
- **装备牌**：100% (13/13)
- **锦囊牌**：92% (11/12)
- **总体完成度**：96% (27/28)

### 需要完成的工作

#### 高优先级
1. **实现借刀杀人**：
   - 在 `CardSubType` 枚举中添加 `JiedaoShaRen`
   - 创建 `JiedaoShaRenResolver` 类
   - 在 `ImmediateTrickResolver` 中添加分发逻辑
   - 在 `ActionIdMapper` 中添加映射（如果需要）

#### 中优先级
2. **注册动作处理器**（如果需要直接使用）：
   - 检查 `ActionIdMapper` 中是否有所有卡牌的映射
   - 确保 `UseCardResolver` 可以正确处理所有卡牌类型
   - 考虑为常用卡牌（如桃）添加直接的动作处理器注册

#### 低优先级
3. **测试覆盖**：
   - 为借刀杀人添加单元测试
   - 验证所有已实现卡牌的功能完整性

---

## 相关文件位置

### 卡牌类型定义
- `core/Model/Enums.cs` - `CardSubType` 枚举

### 解析器实现
- `core/Resolution/SlashResolver.cs` - 杀
- `core/Resolution/PeachResolver.cs` - 桃
- `core/Resolution/TrickResolvers.cs` - 锦囊牌通用解析器
- `core/Resolution/EquipResolver.cs` - 装备

### 动作映射
- `core/Rules/ActionIdMapper.cs` - 卡牌类型到动作ID的映射
- `core/Resolution/Extensions.cs` - 动作处理器注册方法

### 装备技能
- `core/Skills/Equipment/` - 所有装备技能实现
