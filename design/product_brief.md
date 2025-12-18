三国杀 Core（C# DLL 规则引擎）Product Description
1. Product overview

本产品为“三国杀”游戏的核心规则引擎（Core），以 C# Class Library（DLL） 形式交付，负责规则、状态、时序、结算、日志与扩展机制，不包含 UI、网络、账号、存储等应用层能力。目标是让任意客户端/应用端仅通过调用 Core API 即可驱动完整对局，并支持未来的联机一致性、回放与 AI 仿真。

Non-goals（当前不做）

UI/动画/音效

网络同步/房间系统

数据持久化（存档/数据库）

全量武将与扩展包（先框架 + 小套样例内容）

2. Broad feature list（功能特性总览）
2.1 Game lifecycle

对局创建与配置：玩家数量、座位顺序、初始体力/手牌、牌堆构成、武将/技能装配、扩展包开关

可重复随机：注入 seed，确保发牌/判定/随机结果可复现（测试/回放/联机一致性）

2.2 Turn/phase engine

自动推进回合与阶段：准备/判定/摸牌/出牌/弃牌/结束

标准事件：TurnStart/TurnEnd、PhaseStart/PhaseEnd 等，供技能/装备监听与改写

出牌阶段支持“选牌→选目标→确认/取消→回到选牌”的交互循环

2.3 Action / choice system（无 UI 交互核心）

Core 输出当前玩家可执行动作列表（使用牌/发动技能/结束阶段等）

Core 发出选择请求（选目标/选牌/选项/是否发动）

应用端提交 choice，Core 进行合法性校验并推进结算

2.4 Card & zones（牌与区域）

卡牌定义：基础牌/锦囊/装备（类型、花色点数、使用条件、目标规则）

区域系统：牌堆/弃牌堆/手牌/装备区/判定区/移出游戏

牌移动统一入口与事件（BeforeMove/AfterMove），供技能监听

可见性模型：支持“谁能看到哪张牌”（为未来 UI/联机提供数据）

2.5 Resolution pipeline（结算管线）

可插拔 resolver：UseCard / Slash / Damage / Dying / Judgement 等

支持：无效、替换、追加、取消、改值、改目标、改来源等复杂互动

多目标效果支持规则顺序结算

2.6 Response system（响应链）

响应窗口与轮询：按规则向玩家依次询问响应

支持响应结束条件：无人响应/响应成功/响应被替换

支持追击类机制：例如【青龙偃月刀】在杀被闪后触发追加出杀窗口

2.7 Damage & dying（伤害与濒死）

伤害独立 resolver：可挂载“伤害+1/免伤/转移/属性变化”等

濒死流程：进入濒死→轮询救援→死亡结算（扩展点预留）

标准事件点：DamageCreated/DamageApplied/AfterDamage、DyingStart/PlayerDied 等

2.8 Judgement（判定）

统一判定流程：翻开判定牌→计算结果→后续处理

预留改判/替换判定牌扩展点

2.9 Skill engine（技能系统）

技能类型：主动技、触发技、锁定技（可扩展限定技/觉醒技）

技能能力：提供额外动作、修改规则判断、介入结算、发起选择请求

2.10 Extensibility（扩展）

新增卡牌/技能/武将通过注册扩展接入，不改核心主流程

内容可按包加载：Base / Expansion，便于版本管理与测试

2.11 Logging & replay（日志与回放）

结构化日志：可解释“为什么能/不能出牌、为什么无效、为什么触发技能”

回放：seed + 初始配置 + 选择序列 → 可重放得到相同结果

调试：可输出事件序列、resolver 栈、响应链状态

3. Typical use cases（典型使用场景）

客户端 UI 驱动：UI 读取 action list/choice request 展示给玩家；提交 choice；Core 返回状态更新与日志事件。

联机一致性（未来）：服务端只同步 seed/配置与玩家选择；各端用 Core 确定性结算；日志用于对账/回放。

AI 对战/仿真：AI 选择动作提交给 Core；无 UI 环境跑大量对局用于评估或训练。

回放与战报：读取回放数据重放；输出战报与关键事件摘要。

规则验证与回归测试：构造局面并断言事件序列与最终状态；用 golden replay 防回归。

扩展包开发：新增武将/技能/牌作为内容包接入，并用集成测试验证兼容。

4. Architecture（小型架构描述）
4.1 High-level components

Model layer（纯状态）：Game/Player/Card/Zone 等对象，尽量只承载状态。

Rule & Action layer（规则与动作）：合法性判断（距离/次数/目标筛选）+ 动作枚举 + choice request。

Resolution layer（结算管线）：resolver 驱动时序（UseCard→Slash→Damage→Dying…），形成可观察的结算栈。

Event system（事件总线）：resolver 在关键节点发布事件；技能/装备订阅事件并以优先级介入（可插入/替换/拦截）。

Content layer（内容包）：卡牌/技能/武将定义与注册中心；Base/Expansion 可拆包。

Logging & Replay（可观测性）：结构化日志事件流与回放数据导出/导入。

4.2 Typical data flow

应用端提交 Choice → Core 校验 → 推进/创建 resolver → 发布事件 → 技能/装备改写流程 → 状态变更与日志输出 → Core 返回下一步 ChoiceRequest 或下一行动方。

5. Functional requirements（功能需求）
FR-1 对局初始化与配置

创建对局、注入 seed、构建牌堆、发初始手牌、加载武将/技能/扩展包

规则开关可配置（预留）

FR-2 回合与阶段推进

自动推进阶段与回合，阶段事件完整可监听

支持出牌阶段多次动作与取消/重选流程

FR-3 动作与选择

输出可执行动作列表与选择请求

接收 choice 并校验合法性；非法选择返回可解释错误信息（错误码/原因）

FR-4 使用牌结算

宣告→目标确认/修正→无效判定→逐目标结算→结束

结算可被技能/装备拦截、替换、追加

FR-5 响应链

支持响应窗口、轮询、优先级、结束条件

支持追击/追加窗口（青龙偃月刀等）

FR-6 伤害与濒死

伤害独立 resolver；濒死/死亡完整闭环

标准事件点可供技能挂载

FR-7 判定

统一判定流程与改判扩展点

FR-8 牌移动与区域

牌移动统一入口，事件完整，可见性可追踪

FR-9 技能系统

主动/触发/锁定技能可组合工作

技能可修改规则与结算，可发起选择请求

FR-10 日志与回放

结构化日志与回放数据导出/导入

同 seed + 同 choices → 同结果（确定性）

6. Technical stack & packaging（技术栈与交付形态）
6.1 Language / runtime / targets

Language：C#

Runtime：.NET

Project type：Class Library（DLL）

Target framework（建议）：net8.0

若需更广泛兼容，可额外 multi-target（例如 net6.0 或 netstandard2.0），但会限制部分能力与性能优化。

6.2 Dependency policy

Core 尽量保持低依赖/无 UI/无 IO/无网络。

不在 Core 内绑定：WPF/Unity、SignalR/gRPC、数据库/文件系统、具体序列化框架。

所有外部交互通过接口注入（例如 RNG、日志输出、时间源如需）。

6.3 Determinism requirements

RNG 通过 IRandomSource（或等价接口）注入，禁止直接使用全局随机。

禁止使用非确定性来源：DateTime.Now（除非注入可控时间源）、线程竞态导致的不稳定顺序。

回放所需最小信息：seed + initial config + choice sequence。

6.4 Testing stack

单元测试框架：xUnit / NUnit / MSTest（三选一）

集成测试：无 UI 驱动跑完整结算链路并断言事件序列

Golden replay tests：固定 seed + choices，断言最终状态与关键日志序列

6.5 Recommended solution layout

SanGuoSha.Core：状态、规则、resolver、事件系统、对外 API

SanGuoSha.Core.Abstractions（可选）：接口/DTO/日志事件定义（多端引用更稳定）

SanGuoSha.Content.Base：基础牌/武将/技能实现与注册

SanGuoSha.Samples.ConsoleRunner：最小驱动示例

SanGuoSha.Tests：单元/集成/回放回归测试

7. Definition of Done（交付标准）

无 UI 环境可完成最小对战闭环：出杀→出闪→伤害→濒死→死亡

至少 1 个复杂互动样例（例如青龙偃月刀）有集成测试覆盖关键分支

回放可复现：同 seed + choices 得到一致结果

新增卡牌/技能通过“注册 + 新类/定义”完成，不修改核心结算主干