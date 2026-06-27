# SoccerBot 功能与 Bug 总览

更新时间：2026-06-27
定位：这里只放“现在做到了什么、还没做什么、bug 是什么状态”的短说明。详细设计和实现记录放在对应文档里。

## 维护规则

以后每次完成新功能、调整功能范围、修 bug 或发现新 bug，默认同步更新：

1. 本文对应的功能/bug 状态。
2. 根目录 [PLAN.md](../PLAN.md) 的进度表和下一步。
3. 对应的详细设计文档，例如玩法改动更新 [CORE_GAMEPLAY_REWORK.md](CORE_GAMEPLAY_REWORK.md)，脚部交互更新 [QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md](QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md)。

## 当前状态

| 项 | 状态 | 简短说明 | 详情 |
|---|---|---|---|
| 项目主线 | Arena 首版已实现 | 默认进入键鼠第三人称单门 Arena；旧 Training 模式通过 `F8` 保留 | [PLAN](../PLAN.md) |
| 当前重点 | P0 Quest 真机现场测试 | VR 设备当前可用，先验证头显视角、菜单、脚部追踪、脚触球、右 trigger pass intent、直接射门和进球 / 出界判定；PC Arena 键鼠传射先做只读诊断再改 | [PLAN](../PLAN.md) |
| 主要风险 | Quest 真机体验 | 物理脚感、帧率、UI 可读性和 APK 复测仍是本期最大不确定性 | [Quest 交互设计](QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md) |

## 优先级队列

| 优先级 | 项 | 状态 | 简短说明 |
|---|---|---|---|
| P0-Arena | 键鼠优先 Arena Attack | 首版已实现 / 待 Play Mode 调参 | 独立于 Training 的单门 90 秒模式；物理球轻辅助、四周回弹墙、机器人重发球、键鼠/手柄动作、轻量 AI、HUD 和 VR/XR Simulator 桥接均已接入 |
| P0 | 简易场上 AI 状态机 | 增强一版，待 Play Mode 调参 | `FieldAIController` 已让队友、对手、守门员对玩家、球、传球路线和射门轨迹产生轻量反应；新增传向队友/射门威胁识别，并把 AI 压力/支援/覆盖读数接入结果概率 |
| P0 | Quest 脚部 hitbox / 触球调校 | 进行中，已加补触发 / 诊断增强 | 让可见脚部模型、碰撞体和物理触球结果对齐；已加入 proximity 补触发、tracking overlay 和 F2 调参入口，继续减少“看见碰到但没触发” |
| P1 | LLM AI Coach 训练闭环 | 已接入 MVP / 文档已补 | 回合数据记录、TrainingSummaryJson、本地 HTTP 分析、ScorePanel AI 反馈和离线 fallback；请求/响应合约和本地服务说明已补 |
| P1.5 | 免费资源小人基础动态 / 跑步动画层 | 完成一版，待 Play Mode / Quest 复测 | `NpcAnimationPresenter` 根据根节点实际移动速度驱动 idle/walk/run，门将 Saving 叠加程序化侧扑，进球/拦截复用 wave 反应 |
| P2 | 演示画面优化 | 进行中 | 黄昏天空、草坪材质、进球反馈、看台氛围 |
| P3 | 其他事项 | 排队 | 性能专项、演示视频、智能足球、真实空间联动等 |

## 已完成 / 已接入功能

| 功能 | 状态 | 简短说明 | 详情 |
|---|---|---|---|
| Arena Attack 模式 | 完成首版 / 待 Play Mode 调参 | `GameplayModeBootstrap` 默认启动 Arena，`ArenaAttackController` 管理净计时、进球/失误重置、机器人发球、AI 和 HUD；`F8` 可重载回旧 Training | [PLAN](../PLAN.md) |
| Arena 多设备动作层 | 完成首版 | 键鼠和手柄统一输出 `BallActionRequest`；Quest/XR Simulator 的 `FootContactData` 通过 `ArenaBallMotor` 进入同一控球、传球、射门结果层 | [Quest 交互设计](QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md) |
| Arena 物理球与回弹墙 | 完成首版 / 待手感调参 | 球在 live 阶段保持 Rigidbody，不挂到玩家；受限弹簧辅助带球，墙面目标保留约 70% 法向速度，门将扑出不会立刻停局 | [PLAN](../PLAN.md) |
| Unity 项目骨架 | 完成 | Unity 6000.4.7f1 + URP 17.4 + XR 栈已建立 | [README](../README.md) |
| `Main.unity` 主场景 | 完成 | GameManager、Robot、Ball、Player、Scenario、Field、Stadium、HUD、XR Origin 等核心对象已在主场景中 | [旧计划归档](PROJECT_PLAN_ARCHIVE_2026-06-14.md) |
| 数据与事件中枢 | 完成 | `GameManager`、`RobotData`、`IDataSource`、`FakeDataGenerator` 支持本地无机器人演示 | [旧计划归档](PROJECT_PLAN_ARCHIVE_2026-06-14.md) |
| 机器人与球基础表现 | 完成 | 程序化机器人、炮台动画、虚拟球、轨迹预测线已接入 | [旧计划归档](PROJECT_PLAN_ARCHIVE_2026-06-14.md) |
| 剧本系统 | 完成 | `Scenario` / `ScenarioPlayer` / `ScenarioTrigger` / `ScenarioFactory` 和三类结果资产已接入 | [旧计划归档](PROJECT_PLAN_ARCHIVE_2026-06-14.md) |
| 评分与回放 | 完成 | `ScorePanel`、慢动作回放、结果分支和 First Touch 说明已接入 | [核心玩法 V1.2](CORE_GAMEPLAY_REWORK_V1_2_DEMO_FEEDBACK.md) |
| PC 可玩闭环 | 完成 | Setup -> Pass -> Receive -> Possession / Recovery -> Shot -> Score -> Cooldown | [README](../README.md) |
| Quest 3S 部署 | 跑通 | APK 已可 sideload 启动，VR 视角、HUD、球场和基础演算链路已接入 | [README](../README.md) |
| VR 历史 bug 修复 | 完成 | 射门后视角翻转、HUD 偏移、足球过大、BGM 只能播一首按 v6.9 记录为已修 | [旧计划归档](PROJECT_PLAN_ARCHIVE_2026-06-14.md) |
| 接球与反抢玩法 | 完成一版 | 接球窗口、接球质量、即时反馈、Recovery 连按和射门概率影响已接入 | [核心玩法 V1.2](CORE_GAMEPLAY_REWORK_V1_2_DEMO_FEEDBACK.md) |
| 演示画面基础链路 | 已接入 | 程序化球场、看台、灯光、Bloom/Vignette、观众、纸屑和进球反馈骨架已存在 | [演示画面优化](DEMO_VISUAL_ART_OPTIMIZATION_PLAN.md) |
| Quest 手柄脚部原型 | 原型已接入 / 补触发已加 | 手柄位姿驱动左右脚、脚部碰撞区、物理球交互、proximity 补触发、tracking overlay、调参/测试模式已接入 | [Quest 交互设计](QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md) |
| 边界与球门判定 | 已接入 | 物理边界、球门口缺口、`OpponentGoalTrigger`、出界/射偏/进球文本已接入 | [Quest 交互设计](QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md) |
| 简易场上 AI 状态机 | 增强一版 / 待 Play Mode 调参 | `FieldAIController` 自动接入 `MatchFlowController`，提供队友接应、对手追球/截传、守门员横移跟球和概率扑救；传向队友/射门威胁识别已接入，传球压力、队友支援、门将覆盖会影响被断、射门成功率和扑救概率 | [核心玩法重构](CORE_GAMEPLAY_REWORK.md) |
| LLM AI Coach 训练闭环 | P1 / 已接入 MVP，文档已补 | `GameEventRecorder` 记录回合数据，`TrainingSummaryJson` 输出稳定 JSON，`AICoachClient` 调本地 HTTP，`ScorePanel` 显示 AI 反馈；服务离线时走 fallback | [LLM AI Coach 集成文档](LLM_AI_COACH_INTEGRATION.md) |
| 免费资源小人基础动态 | P1.5 / 完成一版 | LowPolyPeople 三套 Controller 均确认包含 idle/walk/run/wave；AI 角色运行时自动接入 `NpcAnimationPresenter`，root motion 关闭，门将成功扑救时播放程序化侧扑 | [演示画面优化](DEMO_VISUAL_ART_OPTIMIZATION_PLAN.md) |

## 未完成 / 进行中功能

| 功能 | 状态 | 简短说明 | 详情 |
|---|---|---|---|
| Arena Play Mode 验收 | 待人工输入复测 | 代码与 Unity 编辑器编译为 0 error；仍需实际连续跑 10 次发球，验证键鼠手感、UI、进球/被断/卡球重置和旧 Training 回归 | [PLAN](../PLAN.md) |
| Arena 操作说明 | 已补 | 已记录启动、模式切换、键鼠/手柄/VR 操作、传射异常现象、摄像机问题、静态镜头计划和下次反馈模板 | [Arena 操作说明](ARENA_ATTACK_OPERATION_GUIDE.md) |
| P8.5 简易场上 AI 状态机 | P0 / 增强一版，待 Play Mode 调参 | 对手会按玩家持球、散球、传向队友切换压迫/盯接应/截传；守门员横向跟球，并能在 Possession 物理射门和 Shot 阶段概率扑救；队友会移动到接应角度并追向传球；AI 读数已参与传球到队友后的被断/进球结算 | [核心玩法重构](CORE_GAMEPLAY_REWORK.md) |
| Quest 脚部 hitbox / 触球调校 | P0 / 进行中，待真机复测 | `TrackedLegController` 已加入最近点 proximity probe，并补了 Rigidbody 初始化、runtime 材质复用；`QuestControllerLegRig` 避免跨 rig 误绑，`PhysicalTouchTest` overlay 显示左右脚 tracking 状态；仍需 Quest Link / APK 复测脚位置、触发距离、冲量和射门辅助 | [Quest 交互设计](QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md) |
| AI Coach 本地服务联调 | P1 / 文档已补，待验证 | Unity 端已接入 `POST http://localhost:8000/analyze`；已补请求/响应合约、mock 服务示例和联调清单，需要接真实 LLM 服务验证 | [LLM AI Coach 集成文档](LLM_AI_COACH_INTEGRATION.md) |
| 免费资源小人基础动态 / 跑步动画层 | P1.5 / 完成一版，待真机复测 | 已接 idle/walk/run/wave 和程序化侧扑/静态模型兜底；需要在 Play Mode / Quest 确认跑步阈值、侧扑幅度与性能 | [演示画面优化](DEMO_VISUAL_ART_OPTIMIZATION_PLAN.md) |
| P9.5 演示画面打磨 | P2 / 进行中 | 缺少真正可见的黄昏天空、草坪材质细节、强进球反馈和大赛氛围 | [演示画面优化](DEMO_VISUAL_ART_OPTIMIZATION_PLAN.md) |
| P9 性能优化 | P3 / 未完成 | 目标 Quest 稳定 72fps、Draw Call < 200、APK < 150MB | [旧计划归档](PROJECT_PLAN_ARCHIVE_2026-06-14.md) |
| P10 演示视频 | P3 / 未完成 | 需要等画面和交互稳定后再录制 2-3 分钟视频 | [旧计划归档](PROJECT_PLAN_ARCHIVE_2026-06-14.md) |
| P11 智能足球训练模式 | P3 / 暂缓 | BS-BT91 / BLE / IMU 方向保留，当前不作为主线依赖 | [旧计划归档](PROJECT_PLAN_ARCHIVE_2026-06-14.md) |
| 真实机器人联调 | P3 / 降优先级 | 目前以 `FakeDataGenerator` 和本地流程为主，`NTManager` 仍有真实 NetworkTables TODO | [README](../README.md) |
| P12 真实空间联动定位 | P3 / 未来项 | Limelight / AprilTag / VR 空间对齐先记录方案，本期不做 | [旧计划归档](PROJECT_PLAN_ARCHIVE_2026-06-14.md) |

## 已修 Bug

| Bug | 状态 | 简短说明 | 详情 |
|---|---|---|---|
| 射门后视角翻向背后 | 已修 | v6.9 记录为已修复并通过 VR 复测 | [旧计划归档](PROJECT_PLAN_ARCHIVE_2026-06-14.md) |
| HUD / Intro 跑到右上角 | 已修 | v6.9 记录为已修复并通过 VR 复测 | [旧计划归档](PROJECT_PLAN_ARCHIVE_2026-06-14.md) |
| 足球过大 | 已修 | v6.9 记录为已修复并通过 VR 复测 | [旧计划归档](PROJECT_PLAN_ARCHIVE_2026-06-14.md) |
| BGM 只能播一首 | 已修 | v6.9 记录为已修复；若新版本复现，再重新开 bug | [旧计划归档](PROJECT_PLAN_ARCHIVE_2026-06-14.md) |
| 左右手柄脚绑定可能串手 | 已修一版 | handedness 改变时重建 input actions，并增加绑定诊断 | [Quest 交互设计](QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md) |
| 左右脚引用可能指向同一组件 / 误绑其他 rig | 已修一版 | `QuestControllerLegRig` 增加 guard，发现同引用时清理并重绑；查找已有 leg 时优先限定在当前 rig 层级 | [Quest 交互设计](QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md) |
| 球场角落与看台/边界干涉 | 已修一版 | `StadiumBuilder` 扩大椭圆看台，减少矩形球场角落重叠 | [Quest 交互设计](QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md) |
| 球门口被边界墙挡住 | 已修一版 | 前后边界墙在球门口留缺口，并用 `OpponentGoalTrigger` 判定进球 | [Quest 交互设计](QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md) |

## 未修 / 待复测 Bug 与风险

| Bug / 风险 | 状态 | 简短说明 | 下一步 |
|---|---|---|---|
| Arena 左右键传射演示效果奇怪 | 已记录 / 待下一版描述 | 当前左键射门、右键传球的方向、力度、球响应和视觉区别不自然，但具体复现尚未锁定 | 下一版按 [Arena 操作说明](ARENA_ATTACK_OPERATION_GUIDE.md) 的模板补预期/实际效果，再改逻辑或参数 |
| Arena 第三人称摄像机跟踪不理想 | 已记录 / 待改静态镜头 | 当前镜头跟随玩家并由鼠标旋转，构图与观察稳定性未做好 | 改成对方球门上方朝向球场的静态透视镜头，同时将传射瞄准与 camera forward 解耦 |
| 2026-06-27 静态镜头 / 传射解耦尝试未采纳 | 已回退 / 待重新诊断 | 尝试改静态镜头后，实际试玩反馈是视角没有变化，射门 / 传球仍然按不了；本次代码和文档改动已回退，不作为当前基线 | 下个对话先只读排查真实生效的相机链路、输入事件、`GameplayEnabled`、动作 range 和 `ArenaBallMotor.Execute(...)` 失败原因，再决定修改点 |
| 队友/对手/守门员缺少实时 AI 互动 | 已缓解 / 待 Play Mode 调参 | `FieldAIController` 已补强状态事件、传向队友识别、射门威胁识别和 Possession 物理射门门将扑救入口；仍需在 Play Mode / Quest Link 调速度、距离和概率，确认不会抢走玩家主体验 | 复测 P8.5 AI 行为手感并微调参数 |
| AI 小人移动缺少动态动作 | 已缓解 / 待 Play Mode 与 Quest 复测 | `NpcAnimationPresenter` 已按实际速度切换 LowPolyPeople 动画，并给门将加入 Saving 侧扑；无 Animator 时有呼吸、起伏和前倾兜底 | 复测跑步切换、侧扑方向/幅度和 Quest 开销 |
| 脚部触球不稳定 | 已缓解 / 待 Quest 复测 | `TrackedLegController` 已用 `OverlapBoxNonAlloc` 在预测脚盒周围做最近点补触发；`PhysicalTouchTest` overlay 可直接看左右脚 tracking 是否有效；可见鞋碰到球但 trigger 漏帧时会发布 `FootBoxProximity` 接触 | 用 Quest Link / APK 验证漏触是否减少，再用 `PhysicalTouchTest`、gizmos、closest-point overlay、tracking overlay 和 F2 调参窗微调 assist padding / distance、`Foot Collider Center` / `Foot Size` / offset |
| 传球到队友附近后球偶发回中心并半陷地面 | 待定位 | 疑似物理持球、队友射门脚本、`BallController.SetPhysicalSimulation(...)` 和 transform 重定位冲突 | 复现并记录触发路径，再拆查 MatchFlow 与 BallController 状态切换 |
| Quest 真机脚部交互手感未知 | 待复测 | 当前代码编译通过，但脚感、右 trigger pass intent、出界/进球体积需要头显确认 | Quest Link 快速测，再 APK 复测 |
| VR 主菜单可见性状态不确定 | 待复核 | 旧 `PLAN.md` 记录过 VR 主菜单不可见；新文档记录 Quest Link 中菜单输入已可用，需要确认 APK 最新状态 | APK 里确认菜单是否可见、可点、可返回训练说明 |
| 美术第一眼观感不足 | 待优化 | 天空、草坪、灯光、进球反馈和观众欢呼剂量不够 | 按 [演示画面优化](DEMO_VISUAL_ART_OPTIMIZATION_PLAN.md) 第一至三阶段执行 |
| Quest 性能余量未知 | 待专项 | 粒子、透明假光束、看台和后处理增加后可能影响 72fps | P9 阶段跑 Profiler / OVR Metrics |
| `NTManager` 真实连接未实现 | 暂缓 | 代码中仍是 TODO / stub，当前以 FakeData 为主 | 后续真实机器人联调时再接 NetworkTables |

## 详细文档指向

| 文档 | 负责内容 |
|---|---|
| [ARENA_ATTACK_OPERATION_GUIDE.md](ARENA_ATTACK_OPERATION_GUIDE.md) | Arena 操作、模式切换、已知问题、静态镜头计划和反馈模板 |
| [PLAN.md](../PLAN.md) | 极简进度板和下一步 |
| [README.md](../README.md) | 项目对外说明、快速开始、关键脚本 |
| [PROJECT_PLAN_ARCHIVE_2026-06-14.md](PROJECT_PLAN_ARCHIVE_2026-06-14.md) | 旧版长计划归档，保留历史细节、里程碑、风险和测试清单 |
| [CORE_GAMEPLAY_REWORK.md](CORE_GAMEPLAY_REWORK.md) | 核心玩法从“看剧本”转向“可玩接球/射门”的设计依据 |
| [CORE_GAMEPLAY_REWORK_V1_2_DEMO_FEEDBACK.md](CORE_GAMEPLAY_REWORK_V1_2_DEMO_FEEDBACK.md) | 接球提示、接球圈、反抢提示、结算解释 |
| [LLM_AI_COACH_INTEGRATION.md](LLM_AI_COACH_INTEGRATION.md) | LLM AI Coach 请求/响应合约、本地服务、提示词约束和联调验收 |
| [DEMO_VISUAL_ART_OPTIMIZATION_PLAN.md](DEMO_VISUAL_ART_OPTIMIZATION_PLAN.md) | 黄昏天空、材质、灯光、角色动态、看台、进球反馈和性能预算 |
| [QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md](QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md) | Quest 手柄驱动脚部、物理触球、边界/球门判定和真机待测项 |
