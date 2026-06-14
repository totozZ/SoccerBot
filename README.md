# SoccerBot - VR 足球机器人 AI 推演沙盒

> 面向比赛演示与训练原型：真实/模拟机器人触发来球，Unity 负责比赛推演，Meta Quest 3S 提供沉浸式观看、接球、反抢、射门和脚部物理交互验证。

## 项目定位

SoccerBot 的目标是把真实足球机器人训练中昂贵、危险、难复现的对抗场景，压缩成一个可以反复练习和演示的 VR 训练沙盒。

当前版本已经从“观看预设脚本演出”推进到“玩家操作会改变比赛结果”的可玩原型：机器人发球，玩家接球；接球质量影响后续射门；接球失败会进入 Recovery 反抢；射门结果由 `ScenarioPlayer` 播放进球、被拦截或射偏等演出，并通过 UI 结算。

## 项目画面

![SoccerBot VR 足球机器人 AI 推演沙盒介绍图](docs/images/intro.png)

| 持球阶段 | 传球阶段 |
|------|------|
| ![玩家拿球后的持球画面](docs/images/possession.png) | ![机器人传球与来球推演画面](docs/images/pass.png) |

## 当前进度

截至 2026-06-13，项目主线状态如下：

| 模块 | 状态 | 说明 |
|------|------|------|
| Unity 主场景与基础流程 | 已完成 | `Main.unity` 包含 GameManager、Robot、Ball、Player、Scenario、Field、Stadium、HUD、XR Origin 等核心对象 |
| PC 可玩闭环 | 已完成 | Setup -> Pass -> Receive -> Possession / Recovery -> Shot -> Score -> Cooldown |
| Quest 3S 部署 | 已跑通 | APK 已可 sideload 启动，VR 视角、HUD、球场与基础演算链路已接入 |
| 接球与反抢玩法 | 已完成一版 | 接球窗口、接球质量、Recovery 连按、危险反馈、接球质量影响射门概率 |
| 结算解释 | 已补齐 | `ScorePanel` 会追加 First Touch 对射门机会的加成/惩罚说明 |
| AI Coach 训练反馈 | 已接入 MVP | 回合结束后生成 `TrainingSummaryJson`，POST 到本地 AI 服务，ScorePanel 显示教练反馈；服务离线时显示 fallback 建议 |
| 演示画面打磨 | 进行中 | 已有程序化球场、看台、灯光、Bloom/Vignette、观众与进球反馈骨架；黄昏天空、草坪材质、进球高潮剂量仍待加强 |
| Quest 手柄脚部交互 | 原型已接入 | 已实现 tracked leg、脚部碰撞区、物理球交互、测试模式与实时调参面板；仍需 Quest 真机校准 |
| 真实机器人联调 | 降优先级 | 当前以 `FakeDataGenerator` 和本地演示流程为主，NetworkTables / RoboRIO 作为后续接入项 |
| 性能与演示视频 | 未完成 | Quest 72fps、Draw Call、APK 体积和最终演示视频仍待专项处理 |

进度速查见 [PLAN.md](PLAN.md)，功能与 bug 总览见 [docs/PROJECT_STATUS.md](docs/PROJECT_STATUS.md)。

## 当前核心循环

1. 真实机器人或 `FakeDataGenerator` 触发来球。
2. Unity 在 `Pass` 阶段生成虚拟来球轨迹，并显示接球提示与接球圈。
3. 玩家在接球窗口内按键，或通过 Quest 手柄驱动的脚部实体触球。
4. 系统根据时机、朝向、脚部速度和触球质量计算 `receiveQuality`。
5. 接球质量足够时进入 `Possession`，玩家蓄力射门或通过脚部物理触球推进。
6. 接球质量过低时进入 `Recovery`，通过连续输入完成反抢。
7. 射门或传球判断交给 `MatchFlowController`，结果由 `ScenarioPlayer` 和 `ScorePanel` 呈现。

## 近期重点更新

- `MatchFlowController` 接入 `Pass / Recovery / Possession / Shot / Score` 阶段流转。
- `ReceptionRules`、`ReceptionPromptPresenter`、`ReceptionTargetIndicator` 完成接球窗口、接球圈和即时反馈。
- `RecoveryMashState` 完成反抢连按判定。
- `ScorePanel.SetFirstTouchContext()` 已能显示 First Touch 对射门概率的影响。
- `GameEventRecorder` / `AICoachClient` 已接入低频 AI Coach 闭环：记录一轮训练数据、生成 JSON、请求本地 `/analyze`、回填 ScorePanel。
- `QuestControllerLegRig` 会运行时创建 `LeftTrackedLeg` / `RightTrackedLeg`。
- `TrackedLegController` 读取 Quest 手柄 position、rotation、velocity，生成脚/小腿可视模型和触球数据。
- `PhysicalBallInteractor` 支持脚部触球后给球施加 impulse，并可路由给 `MatchFlowController`。
- `PhysicalTouchTest` 提供脚部物理触球测试模式，`FootBallTuningController` 支持运行时调脚部 offset、碰撞体和 impulse 参数。
- `StadiumBuilder`、`CrowdAnimator`、`LightingConfigurator`、`PolishVolumeBuilder` 已形成演示画面基础链路。

相关设计文档：

- [核心玩法重构](docs/CORE_GAMEPLAY_REWORK.md)
- [核心玩法 V1.2 演示反馈](docs/CORE_GAMEPLAY_REWORK_V1_2_DEMO_FEEDBACK.md)
- [演示画面与美术优化确认](docs/DEMO_VISUAL_ART_OPTIMIZATION_PLAN.md)
- [Quest 手柄当腿脚的交互设计](docs/QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md)

## 输入方式

| 场景 | PC / Editor | Quest 3S |
|------|-------------|----------|
| 接球 | `Space` 或鼠标左键 | 左右手柄 `grip`，接球阶段也支持右手 `trigger`；脚部原型可通过手柄位姿触球 |
| Recovery 反抢 | `Space` 或鼠标左键连按 | `grip` / `trigger` 连按，脚部触球也可转为反抢输入 |
| 持球射门 | 保留现有蓄力释放逻辑 | 右手柄 trigger 可作为射门意图 gating；物理脚触球路线正在验证 |
| 调试视角 | WASD、鼠标、数字键机位切换 | 头显 6DoF 视角 |
| 物理触球测试 | `R` 重置球，`F2` 开关调参面板 | 同测试模式，需真机校准手柄到脚模型的 offset |

## 快速开始

1. 使用 Unity 6000.4.7f1 打开 [unity](unity/) 项目。
2. 打开 `Assets/Scenes/Main.unity`。
3. 在 Editor 中 Play，使用 `FakeDataGenerator` 无机器人调试来球。
4. 传球飞来时按 `Space` 或鼠标左键接球。
5. 如果接球失败，继续连按同一个键完成 Recovery 反抢。
6. 持球后按现有射门输入蓄力并释放，查看 `ScorePanel` 的结果与 First Touch 说明。

如需验证 Quest 手柄脚部触球：

1. 确认 Player 或场景中存在 `QuestControllerLegRig`，它会创建左右 tracked leg。
2. 给 Ball 挂载或由 rig 自动补 `PhysicalBallInteractor`。
3. 使用 `PhysicalTouchTest` 进入物理触球测试模式。
4. 在 Quest 中观察脚模型是否贴合手柄真实位置，再用 `FootBallTuningController` 调整 offset、脚大小、碰撞体和 impulse。

连接真实机器人时，在 [NTManager.cs](unity/Assets/Scripts/Core/NTManager.cs) 中配置 RoboRIO / NetworkTables 地址，并切换到真实数据源。

## 系统组成

| 模块 | 说明 |
|------|------|
| 机器人端 | 真实机器人或模拟数据源，负责发起来球事件 |
| Unity 推演端 | 接收来球、生成球路、处理接球/反抢/射门、播放结果 |
| Meta Quest 3S | VR 观看与交互目标设备 |
| Scenario 系统 | 维护结果演出资源，负责回放和评分呈现 |
| 脚部物理交互 | 用 Quest 手柄位姿驱动脚/小腿实体，并与物理球碰撞 |
| SmartBall 训练模式 | BS-BT91 / BLE IMU 方向的后续训练模式探索 |

## 技术栈

| 层级 | 技术 |
|------|------|
| 游戏引擎 | Unity 6000.4.7f1 |
| 渲染管线 | URP 17.4.0 |
| VR / XR | XR Interaction Toolkit 3.4.1、XR Management、Oculus XR |
| UI | UGUI、TextMesh Pro |
| 通信 | NetworkTables v4 / 本地 FakeData |
| 机器人控制 | WPILib / C++ |
| 部署 | Meta Quest Developer Hub sideload / Android APK |
| 测试 | Unity Test Framework，当前有 `ReceptionRules` 和 `RecoveryMashState` EditMode 测试 |

## 目录结构

```text
SoccerBot/
├─ README.md
├─ PLAN.md
├─ docs/
│  ├─ PROJECT_STATUS.md
│  ├─ PROJECT_PLAN_ARCHIVE_2026-06-14.md
│  ├─ CORE_GAMEPLAY_REWORK.md
│  ├─ CORE_GAMEPLAY_REWORK_V1_2_DEMO_FEEDBACK.md
│  ├─ DEMO_VISUAL_ART_OPTIMIZATION_PLAN.md
│  ├─ QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md
│  └─ images/
├─ robot/
└─ unity/
   └─ Assets/
      ├─ Scenes/
      ├─ Scripts/
      │  ├─ Ball/
      │  ├─ Camera/
      │  ├─ Core/
      │  ├─ Effects/
      │  ├─ Field/
      │  ├─ Flow/
      │  ├─ Player/
      │  ├─ Robot/
      │  ├─ Scenario/
      │  ├─ Simulation/
      │  ├─ SmartBall/
      │  ├─ UI/
      │  └─ XR/
      └─ Tests/
```

## 关键脚本

| 脚本 | 职责 |
|------|------|
| [MatchFlowController.cs](unity/Assets/Scripts/Flow/MatchFlowController.cs) | 比赛阶段、传球、接球质量、Recovery、脚部触球路由和结果判断 |
| [FPSPlayerController.cs](unity/Assets/Scripts/Player/FPSPlayerController.cs) | PC / Quest 输入、接球事件、蓄力射门和移动控制 |
| [TrackedLegController.cs](unity/Assets/Scripts/Player/TrackedLegController.cs) | 读取 Quest 手柄位姿，驱动脚/小腿模型和触球数据 |
| [QuestControllerLegRig.cs](unity/Assets/Scripts/Player/QuestControllerLegRig.cs) | 创建、绑定、配置左右 tracked leg，并补齐物理球交互组件 |
| [PhysicalBallInteractor.cs](unity/Assets/Scripts/Ball/PhysicalBallInteractor.cs) | 接收脚部触球，施加物理 impulse，或交给 MatchFlow 路由 |
| [PhysicalTouchTest.cs](unity/Assets/Scripts/Ball/PhysicalTouchTest.cs) | 物理触球测试模式、重置球、边界、调参面板 |
| [ScorePanel.cs](unity/Assets/Scripts/UI/ScorePanel.cs) | 结果弹窗、评分文案、First Touch 影响说明 |
| [GameEventRecorder.cs](unity/Assets/Scripts/AICoach/GameEventRecorder.cs) | 记录一轮训练数据并生成 `TrainingSummaryJson` |
| [AICoachClient.cs](unity/Assets/Scripts/AICoach/AICoachClient.cs) | 向本地 AI Coach HTTP 服务发送分析请求并处理 fallback |
| [ScenarioPlayer.cs](unity/Assets/Scripts/Scenario/ScenarioPlayer.cs) | 结果演出播放 |
| [FakeDataGenerator.cs](unity/Assets/Scripts/Simulation/FakeDataGenerator.cs) | 无真实机器人时的本地调试数据 |
| [StadiumBuilder.cs](unity/Assets/Scripts/Field/StadiumBuilder.cs) | 程序化看台、灯塔、顶灯和球场包围结构 |
| [CrowdAnimator.cs](unity/Assets/Scripts/Effects/CrowdAnimator.cs) | 观众、纸屑、闪灯和欢呼反馈 |

## 下一步

1. 在 Quest 3S 真机上校准 `QuestControllerLegRig` 的左右脚 offset、脚大小和 ground lock。
2. 把脚部物理触球从测试模式稳定接入正式 `Pass / Possession / Shot` 流程。
3. 按 [演示画面与美术优化确认](docs/DEMO_VISUAL_ART_OPTIMIZATION_PLAN.md) 优先补黄昏天空、草坪材质和进球反馈剂量。
4. 跑 Quest 性能专项：72fps、Draw Call、SetPass、APK 体积。
5. 画面与交互稳定后录制 2-3 分钟演示视频。
