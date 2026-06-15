# SoccerBot 功能与 Bug 总览

更新时间：2026-06-15
定位：这里只放“现在做到了什么、还没做什么、bug 是什么状态”的短说明。详细设计和实现记录放在对应文档里。

## 维护规则

以后每次完成新功能、调整功能范围、修 bug 或发现新 bug，默认同步更新：

1. 本文对应的功能/bug 状态。
2. 根目录 [PLAN.md](../PLAN.md) 的进度表和下一步。
3. 对应的详细设计文档，例如玩法改动更新 [CORE_GAMEPLAY_REWORK.md](CORE_GAMEPLAY_REWORK.md)，脚部交互更新 [QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md](QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md)。

## 当前状态

| 项 | 状态 | 简短说明 | 详情 |
|---|---|---|---|
| 项目主线 | 可玩原型已跑通 | PC / Quest 基础闭环已经形成：来球、接球、反抢、持球、射门、回放、评分 | [README](../README.md) |
| 当前重点 | P0 双主线 | 简易场上 AI 状态机与 Quest 脚部 hitbox / 触球调校并列最高优先级 | [PLAN](../PLAN.md) |
| 主要风险 | Quest 真机体验 | 物理脚感、帧率、UI 可读性和 APK 复测仍是本期最大不确定性 | [Quest 交互设计](QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md) |

## 优先级队列

| 优先级 | 项 | 状态 | 简短说明 |
|---|---|---|---|
| P0 | 简易场上 AI 状态机 | 完成一版，待复测调参 | `FieldAIController` 已让队友、对手、守门员对玩家、球、传球路线和射门轨迹产生轻量反应，并把 AI 压力/支援/覆盖读数接入结果概率 |
| P0 | Quest 脚部 hitbox / 触球调校 | 进行中，已加补触发 | 让可见脚部模型、碰撞体和物理触球结果对齐；已加入 proximity 补触发和 F2 调参入口，继续减少“看见碰到但没触发” |
| P1 | LLM AI Coach 训练闭环 | 已接入 MVP | 回合数据记录、TrainingSummaryJson、本地 HTTP 分析、ScorePanel AI 反馈和离线 fallback |
| P1.5 | 免费资源小人基础动态 / 跑步动画层 | 未开始 | 让 AI 跑位不再像滑动棋子：优先接 Animator 动画，静态模型则做程序化待机/跑步/扑救假动作 |
| P2 | 演示画面优化 | 进行中 | 黄昏天空、草坪材质、进球反馈、看台氛围 |
| P3 | 其他事项 | 排队 | 性能专项、演示视频、智能足球、真实空间联动等 |

## 已完成 / 已接入功能

| 功能 | 状态 | 简短说明 | 详情 |
|---|---|---|---|
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
| Quest 手柄脚部原型 | 原型已接入 / 补触发已加 | 手柄位姿驱动左右脚、脚部碰撞区、物理球交互、proximity 补触发、调参/测试模式已接入 | [Quest 交互设计](QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md) |
| 边界与球门判定 | 已接入 | 物理边界、球门口缺口、`OpponentGoalTrigger`、出界/射偏/进球文本已接入 | [Quest 交互设计](QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md) |
| 简易场上 AI 状态机 | 完成一版 / 待复测 | `FieldAIController` 自动接入 `MatchFlowController`，提供队友接应、对手追球/低概率截传、守门员横移跟球和概率扑救；传球压力、队友支援、门将覆盖会影响被断、射门成功率和扑救概率 | [核心玩法重构](CORE_GAMEPLAY_REWORK.md) |
| LLM AI Coach 训练闭环 | P1 / 已接入 MVP | `GameEventRecorder` 记录回合数据，`TrainingSummaryJson` 输出稳定 JSON，`AICoachClient` 调本地 HTTP，`ScorePanel` 显示 AI 反馈；服务离线时走 fallback | [README](../README.md) |

## 未完成 / 进行中功能

| 功能 | 状态 | 简短说明 | 详情 |
|---|---|---|---|
| P8.5 简易场上 AI 状态机 | P0 / 完成一版，待复测调参 | 对手缓慢追球、低概率截传球；守门员横向跟球、概率扑救；队友会移动到接应角度并追向传球；AI 读数已参与传球到队友后的被断/进球结算 | [核心玩法重构](CORE_GAMEPLAY_REWORK.md) |
| Quest 脚部 hitbox / 触球调校 | P0 / 进行中，待真机复测 | `TrackedLegController` 已加入最近点 proximity probe；`QuestControllerLegRig` 和 `PhysicalTouchTest` 调参窗已暴露 assist padding / distance / min speed，仍需 Quest Link / APK 复测脚位置、触发距离、冲量和射门辅助 | [Quest 交互设计](QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md) |
| AI Coach 本地服务联调 | P1 / 待验证 | Unity 端已接入 `POST http://localhost:8000/analyze`，需要接真实 LLM 服务验证请求/响应格式 | [PLAN](../PLAN.md) |
| 免费资源小人基础动态 / 跑步动画层 | P1.5 / 未开始 | 盘点免费小人资源是否带 Humanoid 骨骼和动画 clip；为队友、对手、守门员接入 Idle/Run/Save/Celebrate 或程序化替代动作 | [演示画面优化](DEMO_VISUAL_ART_OPTIMIZATION_PLAN.md) |
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
| 左右脚引用可能指向同一组件 | 已修一版 | `QuestControllerLegRig` 增加 guard，发现同引用时清理并重绑 | [Quest 交互设计](QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md) |
| 球场角落与看台/边界干涉 | 已修一版 | `StadiumBuilder` 扩大椭圆看台，减少矩形球场角落重叠 | [Quest 交互设计](QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md) |
| 球门口被边界墙挡住 | 已修一版 | 前后边界墙在球门口留缺口，并用 `OpponentGoalTrigger` 判定进球 | [Quest 交互设计](QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md) |

## 未修 / 待复测 Bug 与风险

| Bug / 风险 | 状态 | 简短说明 | 下一步 |
|---|---|---|---|
| 队友/对手/守门员缺少实时 AI 互动 | 已缓解 / 待复测 | 已新增 `FieldAIController` 轻量状态机和 AI 读数结算权重；仍需在 Play Mode / Quest Link 调速度、距离和概率，确认不会抢走玩家主体验 | 复测 P8.5 AI 行为手感并微调参数 |
| AI 小人移动缺少动态动作 | 已列入 P1.5 | 当前 AI 已会移动和影响结算，但免费资源小人可能仍像滑动棋子，需要基础待机、跑步、侧扑、庆祝动作层 | 先盘点模型骨骼/动画资源，再接 Animator 或程序化替代动作 |
| 脚部触球不稳定 | 已缓解 / 待 Quest 复测 | `TrackedLegController` 已用 `OverlapBoxNonAlloc` 在预测脚盒周围做最近点补触发；可见鞋碰到球但 trigger 漏帧时会发布 `FootBoxProximity` 接触 | 用 Quest Link / APK 验证漏触是否减少，再用 `PhysicalTouchTest`、gizmos、closest-point overlay 和 F2 调参窗微调 assist padding / distance、`Foot Collider Center` / `Foot Size` / offset |
| 传球到队友附近后球偶发回中心并半陷地面 | 待定位 | 疑似物理持球、队友射门脚本、`BallController.SetPhysicalSimulation(...)` 和 transform 重定位冲突 | 复现并记录触发路径，再拆查 MatchFlow 与 BallController 状态切换 |
| Quest 真机脚部交互手感未知 | 待复测 | 当前代码编译通过，但脚感、右 trigger pass intent、出界/进球体积需要头显确认 | Quest Link 快速测，再 APK 复测 |
| VR 主菜单可见性状态不确定 | 待复核 | 旧 `PLAN.md` 记录过 VR 主菜单不可见；新文档记录 Quest Link 中菜单输入已可用，需要确认 APK 最新状态 | APK 里确认菜单是否可见、可点、可返回训练说明 |
| 美术第一眼观感不足 | 待优化 | 天空、草坪、灯光、进球反馈和观众欢呼剂量不够 | 按 [演示画面优化](DEMO_VISUAL_ART_OPTIMIZATION_PLAN.md) 第一至三阶段执行 |
| Quest 性能余量未知 | 待专项 | 粒子、透明假光束、看台和后处理增加后可能影响 72fps | P9 阶段跑 Profiler / OVR Metrics |
| `NTManager` 真实连接未实现 | 暂缓 | 代码中仍是 TODO / stub，当前以 FakeData 为主 | 后续真实机器人联调时再接 NetworkTables |

## 详细文档指向

| 文档 | 负责内容 |
|---|---|
| [PLAN.md](../PLAN.md) | 极简进度板和下一步 |
| [README.md](../README.md) | 项目对外说明、快速开始、关键脚本 |
| [PROJECT_PLAN_ARCHIVE_2026-06-14.md](PROJECT_PLAN_ARCHIVE_2026-06-14.md) | 旧版长计划归档，保留历史细节、里程碑、风险和测试清单 |
| [CORE_GAMEPLAY_REWORK.md](CORE_GAMEPLAY_REWORK.md) | 核心玩法从“看剧本”转向“可玩接球/射门”的设计依据 |
| [CORE_GAMEPLAY_REWORK_V1_2_DEMO_FEEDBACK.md](CORE_GAMEPLAY_REWORK_V1_2_DEMO_FEEDBACK.md) | 接球提示、接球圈、反抢提示、结算解释 |
| [DEMO_VISUAL_ART_OPTIMIZATION_PLAN.md](DEMO_VISUAL_ART_OPTIMIZATION_PLAN.md) | 黄昏天空、材质、灯光、角色动态、看台、进球反馈和性能预算 |
| [QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md](QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md) | Quest 手柄驱动脚部、物理触球、边界/球门判定和真机待测项 |
