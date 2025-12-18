# 三国杀 Core 规则层统一技术规划（PLAN）

> 聚焦于 `core_module_breakdown.md` 中的 **4.基础规则服务与查询器**，并向前后衔接模型层与后续结算/响应系统，为整个规则层提供可以直接落地实现的技术蓝图。

---

## 1. 范围与目标

- **范围（Scope）**：
  - `core` Class Library 中，基于既有 **模型层（Game/Player/Card/Zone）** 与 **枚举/基本类型**，实现一组 **纯服务化、可测试的规则判断与查询组件**，统称为“基础规则服务与查询器层”（Rule & Query Layer）。
  - 这些组件不直接驱动游戏流程，也不负责最终结算，只提供：
    - 使用/响应/发动条件的判定（\( can
afford \)），
    - 合法目标/牌/行动集合的计算（\( enumerate options \)），
    - 各类只读规则信息的查询（距离、次数、上限、当前状态下的限制等）。

- **目标（Goals）**：
  - **解耦**：将规则“判断/查询”与“执行/结算”严格拆分，为后续 `Resolver`、`ResponseSystem`、`SkillEngine` 提供统一入口。
  - **可组合**：规则查询器以**小粒度 predicate & calculator** 为主，通过组合形成更复杂的规则（例如杀的判定由多个子规则 `range`、`phase`、`limit` 组合而成）。
  - **可测试**：所有规则服务均可在“无 UI / 无网络 / 仅给定 Game & Player & Card 状态”的前提下进行单元测试。
  - **可扩展**：后续加入扩展包、模式（军争、身份/国战）时，可以通过配置/插件化注册新规则实现而不破坏既有骨架。

- **非目标（Non-Goals）**：
  - 不在本层实现最终的 **伤害结算 / 濒死 / 回合推进 / 技能触发** 等流程，只提供“是否允许”和“可选集合”的判断逻辑。
  - 不处理任何 UI 相关逻辑（按钮位置、文案、多语言等），仅返回结构化数据（DTO / 结果对象）。

---

## 2. 与现有模块的关系

### 2.1 上游依赖

- **模型层（已在 `02.02_ModelLayerGamePlayerCardZone_PLAN.md` 规划）**：
  - `Game`：
    - 当前回合玩家、当前阶段、全局牌堆/弃牌堆、玩家列表等。
  - `Player`：
    - 体力、手牌区、装备区、判定区、阵营/座位号、当前已使用杀次数等。
  - `Card`：
    - `CardType`, `CardSubType`, `Suit`, `Rank` 等基础属性。
  - `Zone`：
    - 手牌、装备、判定、牌堆、弃牌堆等区域抽象。

- **枚举与基础类型层（`02.03_EnumsAndBasicTypes_PLAN.md`）**：
  - `Phase`（准备/判定/摸牌/出牌/弃牌/结束）。
  - `Camp`, `CardType`, `CardSubType`, `RangeType` 等。

### 2.2 下游被依赖方

- **回合与阶段引擎**：
  - UI 或上层引擎在某个 `Phase` 内，会调用规则服务以获取“当前玩家可以执行的动作集合”。

- **Resolver 层（UseCardResolver、SlashResolver 等）**：
  - 在尝试发起一个行动前，通过规则服务验证：
    - 是否允许此玩家在此时机使用该牌；
    - 选择的目标集合是否满足规则；
    - 是否超出次数/距离限制等。

- **响应链（ResponseSystem）**：
  - 在开启响应窗口时，规则服务负责判断：
    - 某玩家是否有资格响应；
    - 其手牌中哪些牌可以作为某类响应（例如【闪】响应【杀】）。

- **技能系统（SkillEngine）**：
  - 技能可以通过扩展或修饰规则服务的内部判定逻辑（例如“杀的额定次数+1”、“无视距离”等）；
  - 需要支持“技能前置/后置修改规则”的 hook 点。

---

## 3. 模块划分与命名建议

在 `core` 项目中，为规则层建议如下命名空间与文件布局（可对应物理文件夹）：

- `LegendOfThreeKingdoms.Core.Rules`：规则层根命名空间
  - `LegendOfThreeKingdoms.Core.Rules.Abstractions`：规则接口与通用 DTO
  - `LegendOfThreeKingdoms.Core.Rules.Basic`：基础通用规则实现（不含技能与扩展包）
  - `LegendOfThreeKingdoms.Core.Rules.Range`：距离/座次相关规则
  - `LegendOfThreeKingdoms.Core.Rules.Limit`：次数/上限相关规则
  - `LegendOfThreeKingdoms.Core.Rules.Validation`：动作合法性校验
  - `LegendOfThreeKingdoms.Core.Rules.Query`：只读查询器（可用动作列表、可选目标列表等）

后续若需要为扩展内容（模式/扩展包）单独定义规则实现，可新增：

- `LegendOfThreeKingdoms.Core.Rules.Expansion.*`

---

## 4. 核心抽象设计

### 4.1 规则服务总体接口

- **顶层入口**：`IRuleService`
  - 职责：对外提供统一的规则判断/查询 API，内部组合调用更细粒度的组件。
  - 典型方法（不写签名，仅说明职责）：
    - `CanUseCard(game, sourcePlayer, card, targetCandidates)`：判断某玩家此时是否能以给定目标集合使用某牌。
    - `GetLegalTargetsForUse(game, sourcePlayer, card)`：计算合法目标集合（可返回多个目标组合或一个“候选集合 + 目标数量区间”的描述）。
    - `CanRespondWithCard(game, player, card, responseContext)`：判断是否可用某牌作为指定响应。
    - `GetUsableCards(game, player, context)`：根据当前上下文列出该玩家所有可用于“使用/响应/发动”的牌。
    - `GetAvailableActions(game, player)`：基于当前阶段与状态，返回所有可执行基础动作（使用杀、使用桃、结束出牌阶段等）。

- **拆分子服务**：
  - `IPhaseRuleService`
    - 判断当前阶段允许哪些基础行为（是否允许出杀、出桃、结束阶段等）。
  - `ICardUsageRuleService`
    - 关注“使用牌”的规则：次数、数量、时机、目标合法性等。
  - `IResponseRuleService`
    - 关注各种响应窗口中可用的牌与玩家资格。
  - `IRangeRuleService`
    - 计算座次距离、攻击距离，处理装备/技能修正。
  - `ILimitRuleService`
    - 处理次数/上限类规则（如每回合使用杀的次数限制）。

- **组合关系**：
  - `RuleService` 作为门面实现 `IRuleService`：
    - 构造时注入上述子服务接口；
    - 对外暴露的高层 API 会拆解为对多子服务的调用与结果合并。

### 4.2 规则上下文对象（RuleContext）

- 为避免方法签名过长，将常用参数抽象为上下文对象：
  - `RuleContext` 基类或结构体，包含：
    - `Game` 引用（快照或可变状态容器）；
    - 当前相关玩家（source/target）；
    - 当前阶段/子阶段信息；
    - 当前动作标识（使用牌/响应/技能发动等）。

- 对不同场景再派生专用上下文：
  - `CardUsageContext`：
    - 包含：`sourcePlayer`, `card`, `candidateTargets`, `isExtraAction`, `usageCountThisTurn` 等。
  - `ResponseContext`：
    - 包含：`requiredResponseType`（如 Jink/Peach）、`sourceEvent`（被响应的事件，如 Slash）、`currentResponder` 等。

> 设计要求：上下文对象必须是**只读视图**，即规则服务只读取其中数据，不直接修改 Game 状态，以保持判定的纯粹性和可缓存性。

### 4.3 结果类型与错误信息结构

- 为提高可调试性与可视化能力，规则判断的返回值不应仅为 `bool`：
  - 建议设计通用结果类型 `RuleResult`：
    - `IsAllowed`：是否允许；
    - `ErrorCode`：标准化错误码（如 `PhaseNotAllowed`, `TargetOutOfRange`, `CardTypeNotAllowed` 等）；
    - `MessageKey`：可选，用于 UI 层多语言映射；
    - `Details`：结构化附加信息（如具体超出次数多少、需要的最小/最大目标数等）。

- 对于集合类查询：
  - 使用 `RuleQueryResult<T>`：
    - `Items`：符合规则的集合；
    - `ReasonIfEmpty`：当集合为空时给出对应错误信息（便于向玩家解释）。

---

## 5. 基础规则类别拆分

### 5.1 阶段规则（Phase Rule）

- **职责**：
  - 基于 `Game` 中的 `CurrentPhase`、`CurrentPlayer` 等字段，决定：
    - 当前玩家是否可以 **使用牌**（杀/桃/锦囊等）；
    - 是否可以 **结束当前阶段**；
    - 某些牌是否仅在特定阶段可用（如出牌阶段/回合外响应）。

- **关键判定点**：
  - `IsCardUsagePhase(game, player)`：是否处于允许使用普通手牌的阶段（如出牌阶段）。
  - `IsResponseAllowedInCurrentPhase(game, player, responseType)`：某类响应是否允许在当前上下文中发起。

- **与后续模块的协作**：
  - 回合引擎调用 `GetAvailableActions(game, player)` 时，首先通过阶段规则筛选出基础动作。

### 5.2 距离/座次规则（Range Rule）

- **职责**：
  - 计算两个玩家之间的 **座次距离** 与 **攻击距离**，并考虑：
    - 基础座次差；
    - 装备/技能对攻击或防御距离的修正（本阶段要求可先只实现基础版，不引入技能）；
    - 特定模式下的阵营视角（暂可不做，先为身份局实现）。

- **典型接口**：
  - `GetSeatDistance(game, fromPlayer, toPlayer)`：返回正整数距离；
  - `GetAttackDistance(game, fromPlayer, toPlayer)`：综合武器、坐骑后得到的攻击距离；
  - `IsWithinAttackRange(game, fromPlayer, toPlayer)`：判断 `to` 是否在 `from` 当前攻击范围内。

- **短期简化策略**：
  - 第一阶段仅实现“顺时针最短路径距离 + 基础攻击距离 = 1（无装备修正）”，后续再叠加装备/技能修正逻辑。

### 5.3 次数与上限规则（Limit Rule）

- **职责**：
  - 处理“每回合/每阶段/每局”类型的次数限制：
    - 如：默认每回合只能使用一次【杀】。

- **数据依赖**：
  - `Player` 或 `Game` 中应有对应的计数状态：
    - `SlashUsedCountThisTurn`；
    - 其它牌的使用计数按需扩展。

- **判定逻辑**：
  - `CanUseSlashThisTurn(game, player)`：
    - 比较 `SlashUsedCountThisTurn` 与 `MaxSlashPerTurn`（默认 1）；
    - 将后续技能修正抽象为 `GetMaxSlashPerTurn(game, player)`，初始实现固定返回 1。

### 5.4 牌使用规则（Card Usage Rule）

- **职责**：
  - 综合 **阶段规则 + 距离规则 + 次数规则 + 卡牌自身属性**，判断“能否使用某牌”以及“对应合法目标集合”。

- **基础目标（初始范围）**：
  - 先只支持：
    - 【杀】（Slash）
    - 【闪】（Jink）作为响应，而非一般使用牌
    - 【桃】（Peach）
  - 其他锦囊/装备暂不在此阶段强制实现，只需预留结构。

- **关键子问题**：
  - `IsCardUsableNow(game, player, card)`：
    - 检查：
      - 是否在允许使用此类卡牌的阶段；
      - 是否拥有该牌且区域正确（手牌/装备）；
      - 是否未超出次数限制（对杀等牌）；
      - 牌当前是否被某些状态禁止（如“禁杀状态”，暂可不实现）。
  - `GetLegalTargetsForCard(game, player, card)`：
    - 以【杀】为例：
      - 排除自己；
      - 仅包含存活玩家；
      - 仅包含在攻击范围内的玩家；
      - 根据规则确认目标数量（通常为 1，预留多目标支持）。

### 5.5 响应规则（Response Rule）

- **职责**：
  - 用于各种 **响应窗口** 中的规则判定，不直接管理轮询流程。

- **典型场景**：
  - 在“被杀命中”时，目标可以打出【闪】进行响应；
  - 在“濒死”时，队友可以打出【桃】。

- **接口示例**：
  - `GetLegalResponseCards(game, player, responseContext)`：
    - 返回可打出的手牌集合（如所有【闪】或视为【闪】的牌）。
  - `CanPlayerRespond(game, player, responseContext)`：
    - 如：是否在可响应的座次顺序上、是否存活、是否未被禁止响应等。

> 注意：本阶段的实现先不考虑“视为技”等复杂转换，只要求在**明确存在对应牌时**能够给出正确结果。

---

## 6. 查询器（Query）与动作描述对象（Action / Choice）

### 6.1 可用动作查询（Available Actions）

- 为方便 UI 或上层引擎，每个时刻可以通过查询器拿到 **当前玩家可执行的所有基础动作**：
  - `IActionQueryService`：
    - `GetAvailableActions(game, player)`：返回一组 `ActionDescriptor`。

- **`ActionDescriptor` 设计要点**：
  - 字段示意：
    - `ActionId`：内部唯一标识（如 `UseSlash`, `UsePeach`, `EndPhase` 等）；
    - `DisplayKey`：UI 可用于显示的文案 key；
    - `RequiresTargets`：是否需要选择目标；
    - `TargetConstraints`：
      - `MinTargets`, `MaxTargets`；
      - `TargetFilterType`（敌方/任意/队友等，初期可简化）；
    - `CardCandidates`：若此动作与某类牌绑定，可预填可用牌列表或过滤条件（如所有【杀】）。

### 6.2 选择请求与结果（Choice）

- 为后续网络/UI 层统一交互，规则层需要用 DTO 形式表达“请求玩家选择”：

- `ChoiceRequest`：
  - `RequestId`：唯一标识；
  - `PlayerId`：需要做出选择的玩家；
  - `ChoiceType`：如 `SelectTargets`, `SelectCard`, `Confirm`, `SelectOption`；
  - `Constraints`：
    - 对目标：`MinTargets`, `MaxTargets`, `AllowedTargets` 列表或过滤条件；
    - 对牌：`AllowedCards` 或基于谓词的筛选规则（如“只能选手牌中类型为 Slash 的牌”）。

- `ChoiceResult`：
  - `RequestId`；
  - `SelectedTargets` / `SelectedCards` / `Confirmed` 等实际选择结果。

> 规则层的职责是：**生成**这些 `ChoiceRequest` 的约束信息；而实际的“等待玩家操作 → 收到 `ChoiceResult`”由上层引擎或网络层处理。

---

## 7. 实现分阶段计划（按复杂度递进）

### 阶段 1：规则骨架与最小实现

- **目标**：建立规则层基础接口与简单实现，只支持【杀】/【桃】/【闪】的最小判断。

- **工作项**：
  1. 在 `LegendOfThreeKingdoms.Core.Rules.Abstractions` 中定义：
     - `IRuleService`, `IPhaseRuleService`, `ICardUsageRuleService`, `IRangeRuleService`, `ILimitRuleService`, `IResponseRuleService`；
     - `RuleContext`, `CardUsageContext`, `ResponseContext`；
     - `RuleResult`, `RuleQueryResult<T>` 等结果类型；
     - `ActionDescriptor`, `ChoiceRequest`, `ChoiceResult` 等 DTO。
  2. 在 `LegendOfThreeKingdoms.Core.Rules.Basic` 中实现：
     - `PhaseRuleService`：最小版阶段规则（仅出牌阶段允许使用【杀】/【桃】）。
     - `LimitRuleService`：只实现“每回合一杀”限制。
     - `RangeRuleService`：实现基础座次距离与固定攻击范围=1。
     - `CardUsageRuleService`：
       - `IsCardUsableNow` 支持【杀】、【桃】；
       - `GetLegalTargetsForCard` 支持【杀】的目标筛选逻辑。
     - `ResponseRuleService`：
       - `GetLegalResponseCards` 用于【闪】响应【杀】的最小实现。
  3. 实现门面 `RuleService`（组合上述子服务）。
  4. 为所有服务撰写基础单元测试：
     - 以少量玩家/简单牌堆构造 `Game` 对象，验证关键判定路径。

### 阶段 2：动作查询与 Choice 描述

- **目标**：让上层可以在任意时刻通过规则层获得“可用动作列表”与“选择约束信息”。

- **工作项**：
  1. `ActionQueryService` 实现：
     - 基于当前 `Game`、`Player` 和 `PhaseRuleService` 判断：
       - 出牌阶段：`UseSlash`, `UsePeach`, `EndPhase`；
       - 非出牌阶段：根据上下文（如响应窗口）可能无动作或仅有`Confirm` 类动作。
  2. 把 `ActionQueryService` 接入 `RuleService` 统一出口。
  3. 为 `ChoiceRequest` 定义最小子类/枚举以适配当前需求：
     - `TargetSelectionRequest`（针对【杀】）；
     - `CardSelectionRequest`（针对【闪】/【桃】响应）。
  4. 单元测试覆盖：
     - 出牌阶段可用动作列表正确；
     - 生成的 `ChoiceRequest` 目标/牌约束与规则一致。

### 阶段 3：为 Resolver 与 Response System 预留扩展点

- **目标**：
  - 使规则服务可以被 `UseCardResolver` / `SlashResolver` / `ResponseSystem` 等后续模块直接使用，同时为技能系统扩展提供 hook。

- **工作项**：
  1. 在规则服务接口层增加：
     - `ValidateActionBeforeResolve(game, actionDescriptor, choiceResult)`：
       - 在 Resolver 真正执行前对“动作+选择”进行最终合法性校验；
     - 该方法只读，不负责执行任何状态变更。
  2. 预留技能扩展点：
     - 通过接口或装饰器模式，让技能可以：
       - 修改 `GetMaxSlashPerTurn` 的返回值；
       - 修改 `GetSeatDistance` / `IsWithinAttackRange` 的判定；
       - 修改 `GetLegalTargetsForCard` 的过滤逻辑。
  3. 初期实现简单的“空技能扩展容器”：
     - 例如 `IRuleModifierProvider`，当前返回为空集合；
     - 保证未来加入技能时，只需在此处注册即可参与规则计算。

---

## 8. 设计约束与最佳实践

- **纯函数倾向**：
  - 规则服务应尽量设计为“给定上下文 → 返回结果”的纯函数，避免内部持久状态，以便：
    - 易于单测；
    - 易于回放和重现 bug；
    - 有利于未来并发/多线程优化。

- **错误码标准化**：
  - 在 `Rules` 层定义统一错误码枚举（如 `RuleErrorCode`），避免在各处使用硬编码字符串。

- **与日志/回放的衔接**：
  - 规则层自身不产生日志，但其结果应便于在上层被结构化记录：
    - 例如，在 Resolver 使用规则结果时，将 `RuleResult.ErrorCode` 与上下文一并写入日志。

- **性能考虑**：
  - 早期实现优先保证正确性与清晰度，若未来出现性能瓶颈，可在：
    - 距离计算结果；
    - 可用动作列表；
    - 合法目标集合
    上做缓存，但需注意游戏状态变更时的缓存失效策略。

---

## 9. 与其它 PLAN 文档的对齐

- `02.01_CoreFoundationAndConfiguration_PLAN.md`：
  - 规则层接口中使用的配置项（如 `MaxSlashPerTurnDefault`）应来源于统一的 `GameConfig`，避免魔法数字散落在规则实现中。

- `02.02_ModelLayerGamePlayerCardZone_PLAN.md`：
  - 规则层只能通过模型层公开的属性/方法访问游戏状态，禁止反向在规则中直接维护“影子状态”。

- `02.03_EnumsAndBasicTypes_PLAN.md`：
  - 所有与规则相关的枚举（阶段、阵营、卡牌类型等）必须统一使用此处定义的类型，避免重复定义。

---

## 10. 完成定义（Definition of Done）

- **接口层**：所有上文提到的规则接口与 DTO 已在 `core` 项目中定义，并通过编译。
- **基础实现**：
  - 阶段、距离、次数、牌使用、响应规则均有最小实现，能正确处理【杀】/【闪】/【桃】的基础场景。
- **查询器**：
  - 能根据当前 `Game` 状态为当前玩家给出可用动作列表与相关 `ChoiceRequest` 约束。
- **测试**：
  - 核心规则路径（尤其是“出杀 → 选目标 → 检查合法性”、“被杀 → 能否出闪响应”）均有覆盖率良好的单元测试。
- **文档**：
  - 本 `PLAN.md` 已与其它 `*_PLAN.md` 文档在命名与职责划分上保持一致，并在必要位置添加交叉引用。
