# 三国杀 Core 规则引擎模块拆解（core Class Library）

## 1. 总体思路

- **目标**：在保持模块相对独立的前提下，按照“依赖尽量少、实现难度由简到繁”的顺序，为现有 `core` Class Library 制定一份可执行的模块级开发拆分。
- **原则**：
  - **先状态后行为**：先建纯数据/状态模型，再引入规则、事件与结算管线。
  - **先单向依赖后双向耦合**：尽量让早期模块只被后续模块依赖，而不反向依赖。
  - **先最小闭环后复杂互动**：先实现能跑通一局基础战斗的最小规则，再叠加响应链、复杂技能、扩展包等。

---

## 2. 模块拆分与推荐开发顺序

### 2.1 基础设施与模型层（最独立、最简单）

1. **Core 基础骨架与配置**（低复杂度，高独立性）
   - 在 `core/core.csproj` 下确定命名空间布局（例如 `SanGuoSha.Core`）。
   - 定义基础配置对象：`GameConfig`, `PlayerConfig`, `DeckConfig` 等，仅包含数据字段，不写业务逻辑。
   - 预留 RNG 和日志接口（例如 `IRandomSource`, `ILogSink`），但先只写接口和简单实现或 `NoOp` 实现。

2. **模型层：Game / Player / Card / Zone**（纯状态，几乎无外部依赖）
   - 在 `core` 中建立 `Model` 相关命名空间或文件夹，例如：
     - `Game`：整体对局状态（玩家列表、当前回合玩家、阶段、牌堆等）。
     - `Player`：座位号、阵营、体力、手牌引用、装备区、判定区等。
     - `Card`：卡牌 ID、基础类型（杀/闪/桃/锦囊/装备）、花色点数与标签字段。
     - `Zone`：牌堆、弃牌堆、手牌区、装备区、判定区、移出游戏等的抽象。
   - 定义“只承载状态”的类和基础集合结构，不实现规则判断和结算逻辑。

3. **枚举与基础类型定义**
   - 定义与游戏强相关但逻辑中会大量引用的枚举，例如：
     - `CardType`, `CardSubType`, `Suit`, `Phase`, `Camp` 等。
   - 这些枚举在后续规则层、resolver、技能中会被频繁使用，尽早稳定下来。

---

### 2.2 规则与动作枚举层（依赖模型，仍然相对简单）

4. **基础规则服务与查询器**
   - 定义与实现**只读规则判断**与查询：
     - 例如：`IRuleService` 或类似服务：判断某玩家是否能使用某牌、可选目标列表计算、距离/次数限制等接口。
   - 初期只实现最小集合：杀 / 闪 / 桃 的使用条件与基础目标规则，规则尽量写死以保证简单。

5. **动作与 Choice 描述对象**
   - 定义 `ActionDescriptor` / `ChoiceRequest` / `ChoiceResult` 等 DTO，对 UI/调用方暴露：
     - 可执行动作列表（使用牌、结束阶段等）。
     - 选择请求（选目标、选牌、是否发动等）。
   - 不实现复杂的多步选择逻辑，只支持基础“选目标 → 确认”的流程。

---

### 2.3 回合与阶段引擎（开始驱动时序）

6. **Turn/Phase 状态与简单引擎**
   - 在模型中完善 `Turn` / `Phase` 状态字段。
   - 实现一个最小的回合状态机：
     - 只支持：准备 → 判定（可暂空）→ 摸牌 → 出牌 → 弃牌 → 结束。
   - 提供推进接口，例如：`AdvancePhase()` / `StartNextTurn()`，尚不加入事件总线与技能介入，只是顺序推进状态。

7. **对局初始化与开局流程**
   - 实现创建对局 API，例如：`GameEngine.CreateGame(GameConfig, IRandomSource)`：
     - 构建牌堆（基础牌集合）。
     - 洗牌（通过注入的 RNG）。
     - 发初始手牌、分配武将占位（即使暂时不实现技能）。
   - 目标：能从“配置 + seed”创建一个静态、可检查的初始 `Game` 状态。

---

### 2.4 基础结算管线与牌移动（开始引入可插拔设计）

8. **牌移动与区域服务**
   - 引入统一的“牌移动”接口/服务，例如 `CardMoveService`：
     - 从一个 `Zone` 到另一个 `Zone`（手牌 → 弃牌堆、牌堆 → 手牌等）。
     - 发布 `BeforeMove` / `AfterMove` 的内部事件结构（即使暂时没有外部订阅者）。
   - 这是后续技能与日志的关键扩展点，设计时注意：不要耦合 UI 或网络。

9. **最小 Resolution Pipeline：UseCard & Slash**
   - 设计 resolver 抽象：`IResolver` / `ResolutionContext` / `ResolutionStack` 等核心接口与数据结构。
   - 实现最小闭环所需的几个 resolver：
     - `UseCardResolver`：声明使用牌、检查合法性、确定目标。
     - `SlashResolver`：处理【杀】的使用与被响应（暂不实现复杂追击）。
   - 先不引入响应链轮询，只实现：杀命中 → 造成伤害 → 扣减体力。

10. **基础 Damage 处理（不含濒死救援）**
    - 实现 `DamageResolver`：
      - 包含来源、目标、伤害值、伤害类型等字段。
      - 结算为“扣减体力 + 记录一次伤害事件”。
    - 为后续“濒死”、“免伤”、“转移伤害”等预留字段，但暂不实现复杂逻辑。

---

### 2.5 响应链与濒死流程（复杂度提升，有前置依赖）

11. **Response System 骨架**
    - 定义响应窗口结构：`ResponseWindow`、`ResponseOpportunity` 等。
    - 实现基础轮询逻辑：按顺序询问玩家是否打出响应牌（如【闪】）。
    - 先仅支持“无人响应 / 有响应则生效”这两种结束条件。

12. **完善 Slash + Jink（杀 / 闪）的流程**
    - 将前面简化的 `SlashResolver` 与 response system 打通：
      - 使用【杀】后，开启响应窗口，允许目标打出【闪】。
      - 无人打出【闪】 → 进入伤害结算；打出【闪】成功 → 取消伤害。
    - 此时应能在无 UI 环境下完整跑通：出杀 → 出闪 → 伤害或免伤。

13. **Dying & 救援流程**
    - 在 `DamageResolver` 完成后接入濒死判断：体力 ≤ 0 时进入 `DyingResolver`。
    - 实现最小的濒死逻辑：
      - 轮询队友是否可以打出【桃】救援。
      - 无人救援 → 死亡结算（标记 player 死亡）。
    - 把死亡事件记入日志/事件序列中，供后续技能挂载使用。

---

### 2.6 事件系统与技能引擎（高度可扩展，但依赖前面模块）

14. **事件总线基础实现**
    - 定义通用事件接口和分发器，例如：`IGameEvent`, `IEventBus`。
    - 在关键 resolver 点发布标准事件：
      - `TurnStart/End`, `PhaseStart/End`, `DamageCreated/Applied`, `DyingStart`, `PlayerDied`, `CardMoved` 等。
    - 先只实现同步、单线程事件分发，不做复杂优先级和取消机制。

15. **技能模型与注册机制**
    - 定义技能基类/接口：
      - 不同技能类型（主动 / 触发 / 锁定）可通过枚举 + 能力标记表达。
    - 引入技能注册中心：一个简单的 `SkillRegistry`，支持根据武将或技能 ID 查找技能实例或工厂。
    - 先实现 1–2 个非常简单的被动技能（例如“额外摸牌”、“杀次数 +1”）。

16. **技能介入结算与规则判断**
    - 让技能可以订阅事件总线，在以下节点改写行为：
      - 修改出牌次数上限、响应机会、伤害数值等。
    - 保持接口简洁：通过 `ModifyXxxContext` 或返回“修饰过的参数对象”的方式减少耦合。

---

### 2.7 日志与回放骨架（依赖前面大部分模块，但实现复杂度中等）

17. **结构化日志模型**
    - 定义 `LogEvent` / `ReplayEvent` 对象，记录：
      - 发生时间顺序、事件类型、关键信息（谁对谁造成了多少伤害、使用了什么牌）。
    - 将事件系统或 resolver 中的关键节点映射到日志事件（不必一开始就覆盖全部）。

18. **最小回放数据结构**
    - 定义 `ReplayRecord`：包含 `seed + initialConfig + choiceSequence`。
    - 提供：
      - `StartReplay(ReplayRecord)`：根据记录重建对局并依序重放 choice。
    - 初期可以不实现持久化，仅支持内存内构建与重放。

---

### 2.8 扩展内容与复杂互动样例（依赖完整管线，放在最后）

19. **基础内容包子集（小量武将/牌）**
    - 在 `core` 内先以内嵌方式定义一小套基础武将与牌：
      - 保证至少能体验常规杀 / 闪 / 桃 + 1–2 个简单锦囊或装备。
    - 后续再视需要拆分为独立 Assembly（目前只规划现有 `core` 项目内部即可）。

20. **复杂互动样例（如青龙偃月刀）**
    - 在响应链与技能系统稳定后，挑选 1 个复杂互动（推荐：青龙偃月刀追击杀）：
      - 通过一个装备技能实现追加“出杀响应窗口”的逻辑。
    - 为该互动编写集成测试，覆盖核心分支，作为整个系统扩展性的“金样例”。

---

## 3. 简要依赖关系示意（Mermaid）

```mermaid
flowchart TD
  modelLayer[ModelLayer(Game/Player/Card/Zone)]
  enumLayer[EnumLayer]
  ruleLayer[RuleAndActionLayer]
  turnEngine[TurnPhaseEngine]
  moveZone[CardMoveAndZones]
  resolveCore[ResolutionCore(UseCard/Slash/Damage)]
  responseSys[ResponseSystem]
  dyingFlow[DyingFlow]
  eventBus[EventBus]
  skillEngine[SkillEngine]
  logging[Logging]
  replay[Replay]
  contentLite[ContentLite]
  complexSample[ComplexSample(Qinglong)]

  modelLayer --> ruleLayer
  enumLayer --> ruleLayer
  ruleLayer --> turnEngine
  modelLayer --> moveZone
  moveZone --> resolveCore
  turnEngine --> resolveCore
  resolveCore --> responseSys
  responseSys --> dyingFlow
  resolveCore --> dyingFlow
  resolveCore --> eventBus
  eventBus --> skillEngine
  skillEngine --> resolveCore
  eventBus --> logging
  logging --> replay
  resolveCore --> contentLite
  skillEngine --> contentLite
  responseSys --> complexSample
  skillEngine --> complexSample
```

---

## 4. 建议的开发节奏

- **阶段 1：步骤 1–3** → 打好模型与配置基础，保证类型稳定。
- **阶段 2：步骤 4–7** → 能创建对局并顺序推进回合与阶段，但不一定能真正“打牌”。
- **阶段 3：步骤 8–10** → 打通最小的“用杀造成伤害”闭环。
- **阶段 4：步骤 11–13** → 完成出杀 / 出闪 / 伤害 / 濒死 / 死亡的完整流程，达到 DoD 要求的最小闭环。
- **阶段 5：步骤 14–16** → 引入事件总线与技能系统，为复杂玩法铺路。
- **阶段 6：步骤 17–18** → 接入日志与回放框架，确保确定性与可调试性。
- **阶段 7：步骤 19–20** → 增量补充内容与复杂互动样例，验证扩展性。

