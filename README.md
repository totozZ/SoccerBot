# SoccerBot - VR 足球机器人 AI 推演沙盒

> 面向大学生创新创业比赛的原型项目：真实/模拟机器人触发来球，Unity 负责比赛推演，Meta Quest 3S 提供沉浸式观看与交互。

## 项目定位

SoccerBot 的目标是把真实足球机器人训练中昂贵、危险、难复现的对抗场景，压缩成一个可以反复练习和演示的 VR 训练沙盒。

机器人发起来球，玩家在 VR 中接球、对抗、射门，系统根据动作质量给出结果演出和评分。

## 项目画面

![SoccerBot VR 足球机器人 AI 推演沙盒介绍图](docs/images/intro.png)

| 持球阶段 | 传球阶段 |
|------|------|
| ![玩家拿球后的持球画面](docs/images/possession.png) | ![机器人传球与来球推演画面](docs/images/pass.png) |

## 当前核心循环

1. 从主菜单选择 `START`，播放比赛引入文案后进入对抗。
2. 真实机器人或 `FakeDataGenerator` 触发来球。
3. Unity 在 `Pass` 阶段生成虚拟球轨迹，并显示接球提示和落点圈。
4. 玩家在接球窗口内按下接球输入，系统根据时机和朝向计算接球质量。
5. 接球质量足够时进入持球阶段，玩家可以蓄力射门；接球质量会影响射门概率。
6. 接球质量过低时进入 `Recovery` 反抢阶段，通过连续按键把球抢回来。
7. 射门结果交给 `ScenarioPlayer` 播放成功、被拦截或射偏等演出，并通过 UI 结算。

## 已实现功能

- 主菜单流程：`START`、`TRAINING`、`EXIT`，支持淡入淡出、按钮音效、菜单 BGM 和 Quest 手柄兜底按键。
- 比赛引入：`IntroManager` 可展示比赛背景文本，例如世界杯决赛加时场景。
- 接球提示：`ReceptionPromptPresenter` 在传球阶段显示 `READY TO RECEIVE`、`CATCH NOW`、`MISSED WINDOW` 等提示。
- 接球落点圈：`ReceptionTargetIndicator` 在球预计到达点显示圆环，并根据接球窗口变色、脉冲和反馈。
- 接球质量判定：`ReceptionRules` 结合传球进度和玩家朝向计算 first touch 质量。
- 反抢阶段：接球差时进入限时连按，包含危险边框、进度提示、镜头震动、对手前压/击退和争球位置反馈。
- 射门结算：接球质量会给射门机会增加奖励或惩罚，`ScorePanel` 会在结算文字中展示 first touch 影响。
- 计分板：`ScoreBoard` 统计进球、射偏、被拦截次数。
- 回放视角：`ReplayDirector` 在射门/结算阶段切到侧面镜头，再回到 FPS 视角。
- 演示氛围：程序化球场、球门、队友/对手/门将站位、攻击方向箭头、天气雾效/雨效、进球粒子和镜头震动。
- 音频反馈：比赛阶段 BGM 切换、射门/进球/拦截/射偏音效接口、哨声和接球撞击声。
- 训练模式：`TRAINING` 进入智能球训练场，隐藏比赛对象，生成小球场和球门，支持 Mock 智能球姿态/位置流。
- 智能球接口：已有 `ISmartBallSource`、`MockSmartBallSource`、`BleSmartBallSource` 和 `SmartBallController` 抽象。

更完整的设计说明见 [docs/CORE_GAMEPLAY_REWORK.md](docs/CORE_GAMEPLAY_REWORK.md) 和 [docs/CORE_GAMEPLAY_REWORK_V1_2_DEMO_FEEDBACK.md](docs/CORE_GAMEPLAY_REWORK_V1_2_DEMO_FEEDBACK.md)。

## 输入方式

| 场景 | 输入 |
|------|------|
| 主菜单 | 鼠标点击按钮；Quest 主按钮开始，副按钮进入训练 |
| 返回菜单 | `Esc`、左手柄 `menuButton` 或右手柄 `secondaryButton` |
| PC 接球 / 反抢 | `Space` 或鼠标左键 |
| Quest 接球 / 反抢 | 左右手柄 `grip`，接球阶段也支持右手 `trigger` |
| PC / Quest 射门 | 保留现有蓄力释放逻辑 |
| 训练模式重置智能球姿态 | `R` |
| 调试视角 | 保留现有键鼠和相机切换逻辑 |

## 系统组成

| 模块 | 说明 |
|------|------|
| 机器人端 | 真实机器人或模拟数据源，负责发起来球事件 |
| Unity 推演端 | 接收来球、生成球路、处理接球/反抢/射门、播放结果 |
| Meta Quest 3S | VR 观看与交互目标设备 |
| 智能球训练 | Mock 智能球数据流和 BLE 智能球接口占位 |
| Scenario 系统 | 维护结果演出资源，负责回放和评分呈现 |
| 演示包装 | 主菜单、引入文案、HUD、计分板、回放镜头、音效和天气氛围 |

## 技术栈

| 层级 | 技术 |
|------|------|
| 游戏引擎 | Unity 6000.4.7f1 |
| 渲染管线 | URP |
| VR / XR | XR Interaction Toolkit、XR Management、Oculus XR |
| 通信 | NetworkTables v4 / 本地 FakeData |
| 智能球 | Mock 数据源 / BS-BT91 BLE 接口占位 |
| 机器人控制 | WPILib / C++ |
| UI / 音频 | UGUI、TextMeshPro、AudioSource |
| 部署 | Meta Quest Developer Hub sideload |

## 快速开始

1. 使用 Unity 6000.4.7f1 打开 [unity](unity/) 项目。
2. 打开 `Assets/Scenes/Main.unity`。
3. 在 Editor 中 Play，进入主菜单。
4. 点击 `START` 进入比赛演示，或点击 `TRAINING` 进入智能球训练模式。
5. 比赛中传球飞来时按 `Space` 或鼠标左键接球。
6. 如果接球失败，继续疯狂按同一个键完成反抢。
7. 持球后按现有射门输入蓄力并释放。

连接真实机器人时，在 [NTManager.cs](unity/Assets/Scripts/Core/NTManager.cs) 中配置 RoboRIO / NetworkTables 地址，并切换到真实数据源。

## 目录结构

```text
SoccerBot/
├─ README.md
├─ PLAN.md
├─ docs/
│  ├─ CORE_GAMEPLAY_REWORK.md
│  ├─ CORE_GAMEPLAY_REWORK_V1_2_DEMO_FEEDBACK.md
│  ├─ DEMO_VISUAL_ART_OPTIMIZATION_PLAN.md
│  └─ images/
│     ├─ intro.png
│     ├─ possession.png
│     └─ pass.png
├─ robot/
└─ unity/
   └─ Assets/
      ├─ Scenes/
      └─ Scripts/
         ├─ Ball/
         ├─ Camera/
         ├─ Core/
         ├─ Editor/
         ├─ Effects/
         ├─ Field/
         ├─ Flow/
         ├─ Player/
         ├─ Robot/
         ├─ Scenario/
         ├─ Simulation/
         ├─ SmartBall/
         ├─ UI/
         └─ XR/
```

## 关键脚本

- [MatchFlowController.cs](unity/Assets/Scripts/Flow/MatchFlowController.cs)：比赛阶段、传球、接球质量、Recovery 反抢和结果路由。
- [IntroManager.cs](unity/Assets/Scripts/Flow/IntroManager.cs)：主菜单开始后播放比赛引入，再启动比赛。
- [FPSPlayerController.cs](unity/Assets/Scripts/Player/FPSPlayerController.cs)：PC / Quest 输入、接球事件、蓄力射门和移动控制。
- [ReceptionPromptPresenter.cs](unity/Assets/Scripts/UI/ReceptionPromptPresenter.cs)：传球阶段接球提示、接球反馈和射门偏置提示。
- [ReceptionTargetIndicator.cs](unity/Assets/Scripts/Flow/ReceptionTargetIndicator.cs)：接球落点圈和窗口反馈。
- [RecoveryMashState.cs](unity/Assets/Scripts/Flow/RecoveryMashState.cs)：反抢连按状态统计。
- [ScenarioPlayer.cs](unity/Assets/Scripts/Scenario/ScenarioPlayer.cs)：结果演出播放。
- [ScorePanel.cs](unity/Assets/Scripts/UI/ScorePanel.cs)：结算弹窗和 first touch 影响说明。
- [ScoreBoard.cs](unity/Assets/Scripts/UI/ScoreBoard.cs)：进球、射偏、被拦截统计。
- [ReplayDirector.cs](unity/Assets/Scripts/Flow/ReplayDirector.cs)：射门和结算阶段的回放镜头切换。
- [FakeDataGenerator.cs](unity/Assets/Scripts/Simulation/FakeDataGenerator.cs)：无真实机器人时的本地调试数据。
- [TrainingModeController.cs](unity/Assets/Scripts/SmartBall/TrainingModeController.cs)：训练模式、小球场、球门和智能球演示。
- [SmartBallController.cs](unity/Assets/Scripts/SmartBall/SmartBallController.cs)：智能球姿态/位置数据平滑和重置。
- [WeatherController.cs](unity/Assets/Scripts/Effects/WeatherController.cs)：晴天、黄昏雾效和雨效切换。
- [OutcomeFx.cs](unity/Assets/Scripts/Effects/OutcomeFx.cs)：进球、拦截、射偏的程序化粒子反馈。

## 待补齐功能

- 真实 BS-BT91 / BLE 智能球桥接还没实现；当前 `BleSmartBallSource` 是接口占位，训练模式默认使用 Mock 数据。
- 真实机器人 NetworkTables 链路需要在 RoboRIO / 实机环境中继续联调，目前 Editor 可用 `FakeDataGenerator` 演示。
- Quest 3S 真机手感还需要实测，包括接球窗口、反抢连按次数、HUD 可读性和射门奖励/惩罚幅度。
- 部分 BGM / SFX 需要在 Inspector 中接入正式音频素材；代码层已经有播放和切换接口。
- 训练模式目前偏智能球姿态展示，还没有完整的传感器指标评分、训练记录和可视化报表。

## 当前状态

截至 2026-06-11，项目已经从“观看预设脚本演出”推进到“主菜单 + 引入演出 + 玩家接球判定 + 反抢 + 射门结算 + 训练模式”的可演示原型。下一步优先级是打通真实硬件数据链路，并在 Quest 3S 上完成手感和 UI 可读性调参。
