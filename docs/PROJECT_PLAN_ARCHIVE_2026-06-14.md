# 旧版 PLAN 归档（2026-06-14）

> 本文从根目录旧 `PLAN.md` 原样迁入，用于保留历史设计、里程碑、测试清单和风险记录。  
> 当前活跃进度请看 [../PLAN.md](../PLAN.md)，功能与 bug 总览请看 [PROJECT_STATUS.md](PROJECT_STATUS.md)。

---

# SoccerBot — VR 足球机器人 AI 推演沙盒

## TODO（速查）

- [x] **P1** 重建 Main.unity 场景（GameManager / Robot / Ball / Camera / Ground / Canvas）
- [x] **P2** 加 1 队友（蓝队）+ 1 对手（红队）GameObject，复用 CharacterBuilder 改色
- [x] **P3** 剧本系统代码：Scenario / ScenarioPlayer / ScenarioTrigger / ScenarioFactory
- [x] **P4** 生成 3 个剧本 .asset，挂到 ScenarioTrigger，监听 OnShotFired
- [x] **P5** ScorePanel 评分 UI + 慢动作回放（Time.timeScale）
- [x] **P5.1** 球的发射起点跟随机器人当前位置
- [x] **P6** XR Origin + PCCameraController 自由视角（PC 端右键拖拽 + WASD 浏览）
- [x] **P6.5** 视觉打磨：程序化足球场 / URP 后处理 Volume / Outcome 粒子特效 / 4 角 SpotLight / 3 静态机位（数字键 1/2/3）/ HUD 减负 + 顶部比分牌
- [x] **P7** 演示流程状态机 FlowManager（串联启动→比赛→操作→演算→评分全流程）
- [x] **P7.1** IntroManager 启动画面（黑底白字 + 比赛背景简介 + BGM 淡入）
- [x] **P7.2 (PC 原型)** MatchFlowController + FPSPlayerController + PowerBarUI（一轮一传 / 长按蓄力 / 力度+随机选剧本）
- [x] **P7.2 (VR 完整版)** Quest 手柄绑定（A/Trigger 蓄力射门 + 体感方向追踪）— ✅ VR 三 bug 已修（视角/HUD/球大小）
- [x] **P7.3** ReplayDirector 演算导演（根据玩家操作选分支剧本 + 慢动作回放）
- [x] **P7.4** AudioManager BGM 管理（背景音乐 + 音效事件触发）
- [x] **P7.5** 修 4 个已知 bug（视角/HUD/球/音频）— ✅ v6.9 已修
- [x] **P8** Quest 3S APK 构建 + 部署（已成功 sideload 启动，A 键意外可触发射门）
- [ ] **P9.5** 视觉演示打磨 — ✅ 基础挂载完成；待按“马拉卡纳黄昏 + 绝杀反馈 + 0.5 世界缩放”方案重做天空/进球剂量/音频/比例
- [ ] **P9** 性能优化 — 目标：Quest 稳定 72fps / Draw Call < 200 / APK < 150MB
- [ ] **P10** 演示视频拍摄 + 剪辑 — 分镜脚本 / 旁白文案 / VR 内录屏
- [ ] **P11** 智能足球 (BS-BT91) 训练模式 — BLE IMU 真球 → Unity 数字孪生旋转 + VR 场地内可见
- [ ] **P12（未来）** 机器人 × VR 真实空间联动定位 — 当前无 Limelight/LL，先记录方案，暂不作为本期依赖

---

> 版本: v7.1.1-plan | 日期: 2026-06-04 | 状态: P1–P8 完成；P9.5 已接线但需按新美术方向重做天空/灯光/进球反馈/缩放
> 参赛: **互联网+ 大学生创新创业大赛 · 萌芽赛道**

---

## 一句话项目定位

真机器人发射 → Unity 接管为虚拟球 → AI 队友/对手按预设剧本推演传球走向 → 评分 → 全程 Meta Quest 3S 沉浸观看。

不是 AdvantageScope 增强版（v3.0 的旧定位已废弃）。是 **VR AR 训练沙盒**：用真硬件触发，用虚拟世界推演。

---

## 演示流程（第一视角沉浸体验）

```
┌─────────────────────────────────────────────┐
│ ① 启动画面 (Intro)                           │
│ 黑底白字：比赛背景简介（经典比赛重现）          │
│ + BGM 淡入                                   │
│ 例：「2014 世界杯决赛，加时赛第 113 分钟…」     │
└──────────────────┬──────────────────────────┘
                   ▼
┌─────────────────────────────────────────────┐
│ ② 比赛关键节点切入                            │
│ 画面从黑屏过渡亮起，球员已在场上               │
│ 相机切换到第一人称视角                        │
│ 对手正在进攻 / 队友持球准备传给你              │
└──────────────────┬──────────────────────────┘
                   ▼
┌─────────────────────────────────────────────┐
│ ③ 真实机器人发球                              │
│ 海绵宝宝发射键按下 → Unity 生成虚拟球飞向你    │
│ （叙事层：队友传球给你的瞬间）                 │
└──────────────────┬──────────────────────────┘
                   ▼
┌─────────────────────────────────────────────┐
│ ④ 玩家 VR 体感操作 ★核心交互                  │
│ Quest 手柄追踪手臂动作                        │
│ → 挥臂击球 / 推杆传球                        │
│ 出球方向/力度由手柄速度&方向决定              │
└──────────────────┬──────────────────────────┘
                   ▼
┌─────────────────────────────────────────────┐
│ ⑤ 演算画面 (Replay)                          │
│ 根据玩家操作结果 + 预设剧本分支：             │
│ 进球 → 欢呼 + 庆祝动画                       │
│ 被拦截 → 对手抢断特写                        │
│ 射偏 → 遗憾慢镜                             │
└──────────────────┬──────────────────────────┘
                   ▼
┌─────────────────────────────────────────────┐
│ ⑥ 评分 + 建议 (ScorePanel)                   │
│ 得分 / 评级 / 改进建议（基于射门质量）         │
│ + BGM 淡出 + 返回主菜单                       │
└─────────────────────────────────────────────┘
```

### 剧本背景示例

| # | 场景 | 简介文案 | BGM 情绪 |
|---|------|----------|----------|
| 1 | 2014 世界杯决赛 | 格策替补绝杀，德国 1-0 阿根廷 | 史诗激昂 |
| 2 | 2022 世界杯决赛 | 姆巴佩帽子戏法，法国绝境反击 | 紧张压迫 |
| 3 | 经典德比 | 巴萨 vs 皇马，梅西一条龙 | 激情澎湃 |

---

## 当前进度总览（v7.1）

| 模块 | 状态 | 说明 |
|------|------|------|
| Unity 项目骨架 | ✅ 完成 | Unity 6000.4.7f1 + URP 17.4 + XR 包就绪 |
| 数据模型 + 接口 | ✅ 完成 | RobotData / IDataSource 等 |
| GameManager 中枢 | ✅ 完成 | 单例 + 事件系统，OnShotFired 事件可复用 |
| FakeDataGenerator | ✅ 完成 | 无机器人时独立运行调试 |
| 机器人 3D 渲染 | ✅ 完成 | 海绵宝宝程序化模型 + 炮台动画 |
| 球轨迹反推 | ✅ 完成 | 抛物线计算 + LineRenderer |
| 相机 / 拖尾 / UI | ✅ 完成 | 复用，无改动 |
| **Main.unity 场景** | ✅ 重建完成 | GameManager / Robot / Teammate / Opponent / Ball / ScenarioPlayer / ScenarioTrigger / PC Camera / XR Origin / Ground / HUD |
| **场景优化** | ✅ 完成 | 相机重命名（PC Camera / XR Camera）、AudioListener 去重、拼写修正、Ground Static Batching |
| **虚拟队友 / 对手** | ✅ 完成 | CharacterBuilder 改色，蓝/红队 |
| **剧本系统** | ✅ 完成 | Scenario SO + Player + Trigger + Editor 工厂菜单 |
| **3 个剧本资产** | ✅ 完成 | ScoreSuccess / Intercepted / ShotMissed |
| **评分 UI** | ✅ 完成 | ScorePanel 淡入淡出 + 按 outcome 着色 |
| **球发射起点跟随机器人** | ✅ 完成 | ScenarioPlayer 偏移关键帧至机器人当前位置 |
| **XR Origin + PC 自由视角** | ✅ 完成 | PCCameraController（右键拖拽 + WASD） |
| **P6.5 视觉打磨** | ✅ 完成 | FieldBuilder 程序化球场 + PolishVolumeBuilder 后处理 + OutcomeFx 粒子 + 4 角 SpotLight + 3 静态机位 + HUD 开关 + ScoreBoard 比分牌 |
| **P7.2 PC 原型** | ✅ 完成（v6.3 重构） | 三角色架构 + FPSPlayerController + PowerBarUI + MatchFlow 一轮循环 |
| **演示流程其余项** | ✅ 脚本完成 + 已挂载 | IntroManager + ReplayDirector + AudioManager 已挂入场景。⚠️ 音频 bug 未修 |
| **VR 真机三 bug** | ✅ 已修复 | 视角/HUD/球大小 均在 v6.9 修复 |
| **Quest 3S 部署** | ✅ APK 跑通 | sideload 成功，VR 内可见球场和剧本演算 |
| **P9.5 视觉打磨** | 🔲 已接线，效果未达标 | 当前灯光链路为 `LightingConfigurator`（环境光/主方向光）+ `PolishVolumeBuilder`（Bloom/Vignette）+ `StadiumBuilder`（4 角 SpotLight）+ `CrowdAnimator`（进球时闪灯/纸屑/欢呼）。灯位当前以 `StadiumBuilder` 生成结果为准，`MatchFlowController` 不再覆写 LampHead。现状：灯头位置已认为正确，但缺少可见天空与扇形光束表现，进球反馈剂量也偏弱。 |
| NTManager / robot C++ | ⬇️ 优先级降低 | 真机联调本期不做，FakeData 顶 |

---

## 模块架构总览

### 依赖关系

```
                        ┌──────────────┐
                        │  GameManager │  单例中枢 + 事件总线
                        │  (Core)      │
                        └──────┬───────┘
                               │ IDataSource
              ┌────────────────┼────────────────┐
              ▼                ▼                ▼
      ┌───────────┐   ┌──────────────┐  ┌──────────────┐
      │FakeData   │   │ NTManager    │  │ MatchFlow    │
      │Generator  │   │ (本期不用)    │  │ Controller   │
      └───────────┘   └──────────────┘  └──────┬───────┘
                                               │ Phase 驱动
          ┌────────────────┬───────────────────┼───────────────────┬────────────────┐
          ▼                ▼                   ▼                   ▼                ▼
  ┌──────────────┐ ┌──────────────┐   ┌──────────────┐   ┌──────────────┐ ┌──────────────┐
  │IntroManager  │ │ReplayDirector│   │AudioManager  │   │ScenarioPlayer│ │ScorePanel    │
  │(启动→比赛)    │ │(Shot→慢镜)   │   │(BGM+SFX)     │   │(关键帧动画)   │ │(评分弹窗)     │
  └──────────────┘ └──────────────┘   └──────────────┘   └──────┬───────┘ └──────────────┘
                                                                │ 偏移动画
                                         ┌──────────────────────┼──────────────────────┐
                                         ▼                      ▼                      ▼
                                 ┌──────────────┐      ┌──────────────┐      ┌──────────────┐
                                 │BallController│      │RobotVisuals  │      │Character     │
                                 │(Attach/发射) │      │(挥手动画)     │      │Builder(改色) │
                                 └──────────────┘      └──────────────┘      └──────────────┘
```

### 模块职责卡片

| 模块 | 路径 | 职责 | 关键类型 |
|------|------|------|----------|
| **Core** | `Scripts/Core/` | 数据中枢、事件总线、IDataSource 抽象 | `GameManager`, `RobotData`, `IDataSource` |
| **Robot** | `Scripts/Robot/` | 海绵宝宝程序化建模 + 炮台动画 | `CharacterBuilder`, `RobotVisuals` |
| **Ball** | `Scripts/Ball/` | 虚拟球物理 + 轨迹预测线 | `BallController`, `TrajectoryRenderer` |
| **Player** | `Scripts/Player/` | FPS 玩家控制（WASD + 鼠标 look + LMB 蓄力） | `FPSPlayerController` |
| **Camera** | `Scripts/Camera/` | 跟随相机 + 3 机位切换 | `SmoothFollow`, `CameraSwitcher` |
| **Simulation** | `Scripts/Simulation/` | 无机器人时的调试数据源 | `FakeDataGenerator` |
| **Scenario** | `Scripts/Scenario/` | 剧本 SO 定义 + 关键帧播放引擎 | `Scenario`, `ScenarioPlayer`, `ScenarioTrigger` |
| **Flow** | `Scripts/Flow/` | 演示流程编排（MatchFlow → Intro → Replay → Audio） | `MatchFlowController`, `IntroManager`, `ReplayDirector`, `AudioManager` |
| **UI** | `Scripts/UI/` | 状态面板 / 比分牌 / 力度条 / 评分弹窗 | `StatusPanel`, `ScoreBoard`, `PowerBarUI`, `ScorePanel` |
| **Field** | `Scripts/Field/` | 程序化足球场生成（菜单驱动） | `FieldBuilder` |
| **Effects** | `Scripts/Effects/` | 粒子特效 + 后处理 Volume | `OutcomeFx`, `PolishVolumeBuilder` |
| **XR** | `Scripts/XR/` | VR 适配（XR Origin 配置 + 画布转换） | `XRSetup`, `PCCameraController` |
| **Editor** | `Scripts/Editor/` | 编辑器工具（构建 / 剧本工厂 / AudioManager 填槽） | `BuildAndroid`, `ScenarioFactory`, `AudioManagerWirer` |

---

## 事件总线参考

GameManager 是项目唯一的**事件中枢**。所有跨模块通信通过以下 4 个事件完成，**不做直接引用**。

| 事件 | 签名 | 触发时机 | 订阅者 |
|------|------|----------|--------|
| `OnRobotUpdated` | `Action<RobotData>` | 每帧（机器人位姿更新） | `StatusPanel`（显示坐标）、`SmoothFollow`（相机跟随） |
| `OnShooterUpdated` | `Action<ShooterData>` | 发射器状态变化 | `RobotVisuals`（炮台动画） |
| **`OnShotFired`** | `Action` | **发射键按下上升沿**（false→true） | `ScenarioTrigger`（触发剧本）、`MatchFlowController`（开始一轮）、`AudioManager`（音效） |
| `OnConnectionChanged` | `Action<bool>` | 数据源连接/断开 | `StatusPanel`（连接指示灯） |

> **设计原则**：GameManager 只负责广播，**不知道谁在听**。订阅者在 `OnEnable`/`OnDisable` 中注册/注销。

---

## 输入映射表

| 操作 | PC（编辑器/Standalone） | Meta Quest 3S | 备注 |
|------|------------------------|---------------|------|
| 移动（Possession 阶段） | WASD | ⬚ 本期不做（固定站位） | PC 端受 `MovementEnabled` 门控 |
| 视角旋转 | 鼠标右键拖拽 | 头显 6-DoF 追踪（TrackedPoseDriver） | VR 中 PCCameraController 自动禁用 |
| 蓄力射门 | **鼠标左键按住** | **A 键按住**（暂，需改 Trigger） | 力度条显示在屏幕底部 |
| 射门方向 | 鼠标朝向（相机 yaw） | 手柄朝向（右手柄 forward） | PC 已实现；VR 待绑定 |
| 释放射门 | 松开左键 | 松开 A 键 | 力度大小决定剧本分支 |
| 跳过 Intro | 任意键 | A 键 | `IntroManager.SkipIntro()` |
| 切换机位 | 数字键 1 / 2 / 3 | ⬚ 本期不做 | PC 端 CameraSwitcher |
| 退出 | Esc | ⬚ Oculus 系统按钮 | — |
| 发射触发（调试） | F1（FakeDataGenerator） | — | 无机器人时模拟 |

---

## 已知问题

> ①–④ 已在 v6.9 修复并通过 VR 复测。⑤ 为 2026-06-04 新发现的 VR UI 问题。

| # | 症状 | 影响平台 | 根因 | 严重度 |
|---|------|----------|------|--------|
| ① | ~~射门后视角翻向背后~~ | ✅ v6.9 已修复 | — | — |
| ② | ~~FIFA 介绍/HUD 跑到右上角~~ | ✅ v6.9 已修复 | — | — |
| ③ | ~~足球过大~~ | ✅ v6.9 已修复 | — | — |
| ④ | ~~BGM 只能播一首~~ | ✅ v6.9 已修复 | — | — |
| ⑤ | **VR 主菜单不可见**（但按 A 键可进游戏） | VR only | `MainMenuPanel.EnsureRuntimeUi()` 硬编码 `ScreenSpaceOverlay`（L146），而 `IntroManager.Start()` 创建它晚于 `XRSetup.Start()` 的 Canvas 转换遍历。A 键能进游戏是因为 Quest fallback 输入绑定仍在工作（L66-97）。 | 🟡 阻塞主菜单交互 |

### ⑤ 修复方案

二选一：
- **方案 A（推荐）**：`MainMenuPanel.EnsureRuntimeUi()` 中检测 `#if UNITY_ANDROID` 时直接用 `WorldSpace` 模式 + 挂 `WorldUIFollower`
- **方案 B**：`XRSetup` 加 `StartCoroutine` 延迟一帧再跑 `ConvertOverlayCanvasesToWorldSpace()`，确保动态创建的 Canvas 也能被覆盖

---

## 测试验证清单

每次改动后，按以下步骤快速验证：

### PC 端（Unity Editor Play）

- [ ] Play 后 3 秒内 Intro 黑屏文字淡入 → BGM 开始
- [ ] 按任意键跳过 Intro → 画面亮起，视角切到 Player 第一人称
- [ ] Setup 阶段：球吸附在 Robot 上，Teammate 隐藏，Player 不可移动
- [ ] Pass 阶段：球抛物线飞向 Player 脚边
- [ ] Possession 阶段：Player WASD 可行走，鼠标右键可旋转视角
- [ ] 按住 LMB：力度条从绿→黄→红，底部可见
- [ ] 松开 LMB：球射出 + 力度决定剧本（≥70% 进球 / 40-70% 射偏 / <40% 拦截）
- [ ] Shot 阶段：相机冻结原地，Teammate 出现，球飞向球门
- [ ] Score 阶段：ScorePanel 淡入，显示分数和评级，BGM 切换为 replay
- [ ] Cooldown 3s 后回到 Setup，BGM 切回 match
- [ ] 按 1/2/3 切换机位（俯视/侧面/机器人背后）
- [ ] 按 F1 触发 FakeData 模拟发射

### Quest 3S 端（APK sideload）

- [ ] APK 安装成功，启动不闪退
- [ ] VR 内能看到球场、Robot、球门
- [ ] 头显旋转追踪正常，无抖动
- [ ] Intro 文字/HUD 在正前方，不在右上角
- [ ] A 键可触发射门（fallback 路径）
- [ ] 射门后视角不翻向背后
- [ ] 足球大小适中（非巨型）
- [ ] 帧率 ≥ 60fps（目标 72fps）
- [ ] APK 体积 < 200MB（目标 < 150MB）

---

## 性能优化（P9）

### 量化目标

| 指标 | 当前（估算） | 目标 | 测量工具 |
|------|-------------|------|----------|
| Quest 帧率 | ~45-55 fps | **稳定 72 fps** | OVR Metrics Tool / `adb logcat` |
| Draw Call | ~300-400 | **< 200** | Unity Frame Debugger |
| SetPass Call | ~150 | **< 80** | Unity Stats 面板 |
| 三角面数 | ~50K | **< 30K**（VR 端 LOD） | Rendering Profiler |
| APK 体积 | ~180MB | **< 150MB**（剥离引擎模块） | Build Report |
| 脚本 GC Alloc | 偶发 spike | **0 KB/frame**（对象池化） | Unity Profiler (Deep) |

### 优化手段

1. **静态合批**：球场地面 / 球门 / 边线 → Static Batching（已部分做）
2. **纹理压缩**：ASTC 6×6 替代 RGBA32，人物纹理降分辨率
3. **LOD**：海绵宝宝模型做 3 级 LOD（VR 中距离远时用低面数）
4. **Shader 降级**：URP Lit → URP Simple Lit
5. **粒子预算**：OutcomeFx 粒子数从 50 降到 20，关闭 collision
6. **阴影**：Quest 端阴影距离 10m → 5m，分辨率 1024 → 512
7. **脚本热路径**：`Update()` 中移除 `GameObject.Find` / `GetComponent`，启动时缓存引用
8. **对象池**：球 / 粒子 / UI 弹窗用对象池替代 Instantiate/Destroy
9. **Audio**：BGM 用 Streaming 模式，SFX 用 Compressed In Memory
10. **引擎模块剥离**：去掉 Physics 3D、Terrain、UnityEngine.UI

> P9 与 P7.5（修 bug）可穿插进行：先修 bug → 侧载复测 → 再跑 Profiler 优化。

---

## 演示视频（P10）

> 萌芽赛道评审看「概念新颖 + 演示视频」——这是项目的**最终交付物**。

### 分镜规划（建议 2-3 分钟）

| 序号 | 时长 | 内容 | 录制方式 |
|------|------|------|----------|
| 1 | 10s | 片头：项目名称 + 团队信息 | 后期叠加 |
| 2 | 15s | 痛点：FRC 训练缺对手/缺场地/成本高 | 后期叠加 |
| 3 | 20s | 真机器人触发：海绵宝宝发射 → 慢镜特写 | PC 录屏 + 实拍画中画 |
| 4 | 40s | **核心演示**：PC 端完整一轮（Setup→Pass→Possession→Shot→Score） | PC OBS 录屏 |
| 5 | 30s | **VR 演示**：Quest 内录屏或 Quest 投屏 | Quest 录屏/投屏 |
| 6 | 15s | 剧本分支展示：进球/拦截/射偏三种结果 | PC 录屏拼接 |
| 7 | 10s | 结尾：未来展望 + 致谢 | 后期叠加 |

### 旁白要点

- 用一句话解释「这不是游戏，是训练工具」
- 强调「真硬件触发 → 虚拟推演」的创新点
- 评分系统的教练价值（量化的反馈闭环）
- VR 不是噱头，是第一人称沉浸训练

---

## 视觉演示打磨（P9.5）

> **目标**：当前版本已经有“灯光、观众、回放、天气、后处理”的代码骨架，但演示观感仍然不够像 2014 世界杯决赛第 113 分钟的马拉卡纳。下一轮不再泛泛做“材质升级”，而是集中把 **天空、黄昏光线、进球绝杀反馈、世界尺度** 四件最影响评委第一眼感受的内容做实。

### P9.5.1 当前真实状态（2026-06-04）

| 模块 | 当前实现 | 当前判断 |
|------|----------|----------|
| **主方向光** | `LightingConfigurator.cs` 在运行时寻找 `Directional Light`，设置暖橙主光、平色环境光、阴影距离与强度 | 底色方向是对的，但更像“黑背景下的暖灯”，不是“有天空的巴西黄昏” |
| **环境光** | `LightingConfigurator` 当前用 `RenderSettings.ambientMode = Flat` + 深暖色环境光 `#1A1410` 一类近黑底色 | 不是“没配环境光”，而是环境光被故意压暗了；问题在于只有底色，没有天空本体 |
| **后处理** | `PolishVolumeBuilder` 挂在 `Global Volume`，提供 Bloom / Vignette / Contrast / Saturation / ACES | 有轻度提味，但单靠后处理撑不起“马拉卡纳傍晚” |
| **球场灯** | 当前可见灯源来自 `StadiumBuilder.cs` 运行时生成的 4 角 SpotLight；`CrowdAnimator` 在进球时会闪灯 | 现在以 `StadiumBuilder` 的生成结果为准。灯头位置已视为正确，不再依赖 scene 手调 |
| **LampHead 位置** | `LampHead` 不是场景预制，是 `StadiumBuilder` 运行时 `Block(...)` 生成；`MatchFlowController` 已停止对 `LampHead` 的运行时覆写 | 之前 scene 里手调无效的根因已经确认；当前问题不再是“位置错”，而是“缺少光束与氛围表现” |
| **观众特效** | `CrowdAnimator.cs` 已挂到 `Stadium`：观众点、纸屑、闪灯、随机欢呼音频链路已接通 | 链路正确，但剂量太保守，演示里缺少“绝杀炸场”的感觉 |
| **音频** | `AudioManager.cs` 有 `_sfxGoal`、`_sfxCrowdCheer`；`CrowdAnimator` 还有独立 `_crowdCheerClips` | 当前是两条欢呼通道并存，但还没按“提前炸音 + 双层叠加”做编排 |
| **天气** | `WeatherController.cs` 已有 Sunny / Rain；当前默认是 `Sunny`，不会自动给出黄昏薄霭 | 代码存在，但“里约黄昏薄雾”这条支线还没真正启用 |

**结论**：
- 现在不是“没有灯”。
- 现在有 **暖色方向光 + 平色环境光 + 四角 SpotLight + 进球闪灯**。
- 当前缺的是三层表现：**可见天空、可感知的球场光束/广播感、足够强的绝杀反馈剂量**。

**2026-06-06 更新**：已先做 lamps 环境光第一步：`LightingConfigurator` 运行时环境光从 Flat 改为 Trilight（冷蓝天空 / 暖橙地平线 / 暗地面），并提高环境反射亮度；`StadiumBuilder` 提升灯面 emission 与 SpotLight 基础强度，同时新增更密集的 `RoofRingLights` 顶端环形 SpotLight 和半透明暖色光晕，让足球场内部更亮并带轻微朦胧感；`MatchFlowController` 已修正 teammate：Setup 时只摆一次位置，Possession 阶段只持续转向面对 player，不再持续重定位到玩家视野左侧。未手改 `Main.unity`，需在 Unity Play Mode 中做最终观感确认。

---

### P9.5.2 天空与黄昏氛围（马拉卡纳版本）

**目标画面**：2014 年世界杯决赛，里约热内卢，下午四点开球，打到加时 113 分钟时，太阳已落到看台边缘以下，天际线从金橙过渡到紫罗兰再到暗蓝，球场泛光灯全部亮起，草皮同时吃到最后一抹自然余晖与人工照明暖光。

#### 当前问题
- 头顶“没有天空”，只有相机清屏色/默认背景。
- `LightingConfigurator` 的暖光方向是对的，但因为没有天空梯度，玩家不会感知到“我站在马拉卡纳黄昏里”。
- 之前 scene 里类似“扇形发光”的感觉，本质上更像是灯光表现层；现在代码里只有 SpotLight，没有体积光束或 sky gradient 的视觉支撑。

#### 建议实现
1. **优先补天空，而不是先折腾更复杂的灯效**
   - 使用 URP 可承受的渐变天空方案。
   - 推荐路线：`Visual Environment -> Sky Type = Gradient Sky`，或程序化 Gradient Skybox。
2. **天空颜色建议**
   - 顶部颜色：`#1A1040`（深蓝紫）
   - 地平线颜色：`#E89040`（金橙）
   - 地面颜色：`#0D0804`（暗暖褐）
3. **主方向光调整**
   - `LightingConfigurator` 的主方向光色温从偏黄暖调整到更偏橙红：`#FFAA60`
   - Y 角从 `-20°` 压低到 `-8°` 左右，模拟夕阳从看台缝隙射进场地
   - `shadowStrength` 从 `0.65` 降到 `0.45`，让黄昏阴影边缘更柔和
4. **天气层辅助**
   - 用 `WeatherController` 增加一个 Dusk/Dry Haze 方向，而不是只保留 Sunny/Rain 二选一
   - 目标雾参数：density 约 `0.005`，色彩接近 `#332820`

#### 文档判断
- **环境光不是没配**，而是配成了“近黑平色底”。
- 当前问题是：**没有天空本体 + 没有黄昏层次感 + 没有可见广播氛围**。
- Quest 端优先上 **Gradient Sky / 程序化天空盒**，因为性能几乎免费。

---

### P9.5.3 进球绝杀反馈增强

#### 当前链路（已接通）
`MatchFlowController` 判定力度 ≥ 70% → `DoTeammateShot` 把球送进门 → `CrowdAnimator.OnScenarioComplete(Score)` 触发纸屑/闪灯/欢呼 → `MatchFlowController` 让队友小幅庆祝 → `ReplayDirector` 切 `SideCam` 保持 1.8 秒 → `ScorePanel` 弹评分。

**判断**：链路逻辑是对的，但每段剂量都偏轻，演示时像“进了一个普通球”，不像“格策绝杀”。

#### 建议增强项

| 维度 | 当前 | 新建议 |
|------|------|--------|
| **纸屑** | `_confettiCount = 25`，半径 4m，生命周期 2.5s | 提升到 80–100；`_confettiSpawnRadius` 扩到 8m；`_confettiLifetime` 延到 4s，确保慢镜回放期间仍在飘 |
| **闪灯** | `CrowdAnimator.FlashSpotLights()`：boost 0.6，flashCount 2 | boost 提到 1.5，flashCount 提到 3，并加入“进球瞬间 0.8s 内全场泛光灯爆一下再回落”的感觉 |
| **队友庆祝** | 当前主要是小跳 | 改成更大幅度的跳跃/举手，并在进球后追加向斜前方冲刺的 coroutine |
| **红队背景 NPC** | 当前只有站位与朝向 | 增加“抱头 / 转身 / 左右摇头”的失败反馈，演示镜头里更容易被评委看到 |
| **回放镜头** | `ReplayDirector` 当前 Shot→SideCam，Score 后保持 1.8s 再回 FPS | 在 `SideCam` 中间插一个 0.3s 的 `Cam1` 俯视切镜，再切回 `SideCam`，模拟电视转播多机位 |
| **进球音效** | `AudioManager._sfxGoal` 存在，但当前文档里仍需确认实际素材是否填入 | 必须补一个明确的命中音效；不能只靠 BGM 和欢呼 |
| **观众欢呼音频** | `AudioManager._sfxCrowdCheer` 与 `CrowdAnimator._crowdCheerClips` 是两条独立通道 | 改成双层叠加；声音比纸屑早 `0.3s` 炸出，再触发视觉特效 |

#### 推荐时间顺序
1. 进球判定成立
2. **0.0s**：先播 `AudioManager.PlayCrowdCheer()` / `PlayGoal()`
3. **0.3s**：`CrowdAnimator.TriggerConfetti()`
4. **同帧或稍后**：`FlashSpotLights()` 增强版
5. 随后进入多机位回放

---

### P9.5.4 世界缩放改为 0.5（不是 0.7）

#### 当前实际尺度
- `MatchFlowController`：`_backgroundNpcScale = 0.56`、`_goalkeeperScale = 0.64`、`_robotVisualLocalScale = 0.576`
- `FieldBuilder`：球场 12m × 18m（`_halfWidth = 6`、`_halfLength = 9`）
- `BallController`：拖尾起始宽度 `_trailStartWidth = 0.18`
- 文档旧方案写的是 **0.7x 缩小**，但用户现要求是 **0.5x**

#### 新结论
这次不再按 30% 缩放处理，统一按 **世界尺度因子 0.5** 规划。

#### 为什么 0.5 可行
- 球场保持 12m × 18m 不变时，人物缩到约 0.65m 高，球约 0.22–0.25m 直径，空间感会明显变大。
- 从演示视频视角看，会更接近“俯瞰一个完整战术回合”的感觉。
- Quest 第一人称会带一点“教练视角 / 上帝视角”感，这对训练沙盒反而是加分项。

#### 必须同步缩放的量
不能只改角色和球的 `Transform.localScale`，以下偏移量也必须一起乘 0.5：
- `_ballOffsetRobot`
- `_ballOffsetPlayer`
- `_teammateSetupOffset`
- `_opponentSetupOffset`
- `_teammateRunDistance`
- `_passApex`
- `_shotApex`
- `_blueBackgroundNpcPositions`
- `_redBackgroundNpcPositions`
- `_goalkeeperPosition`
- `_goalkeeperGoalCenter`
- 以及其他一切与“角色相对空间”绑定的演出参数

#### 推荐实现
在 `MatchFlowController` 新增：
- `[SerializeField] private float _worldScaleFactor = 0.5f;`

然后在 `ApplyDemoOverrides()` 中，把上述所有偏移和演出量统一乘 `_worldScaleFactor`，而不是继续手动在 Inspector 里一个个改。

#### 还要同步跟着缩的内容
- `BallController._trailStartWidth` / `_trailEndWidth`
- 任何 HUD、箭头、球员间距、抛物线高度中和人物尺度强关联的参数
- 如不希望 VR 里过度俯视，可把 XR Origin / Camera Y Offset 从约 1.6m 下调到约 1.0m

#### 不需要跟着改的内容
- `FieldBuilder` 的球门 3.5m × 1.5m 真实比例可以保留
- 角色变小后球门显得更大，反而更像足球比赛

---

### P9.5.5 其他演示细节

#### Intro 气氛
`IntroManager` 现在三行文案本身没问题：
- `2014 FIFA World Cup Final`
- `Extra Time · 113' · Germany 0 – 0 Argentina`
- `The ball is at your feet. Make history.`

建议在第三行出现前增加约 `0.8s` 停顿，让 `0-0` 的比分多停一会，先把紧张感拉满。

#### Crowd Palette 国家感
`StadiumBuilder._crowdPalette` 当前是通用蓝/红/白/金/灰。
建议改成更接近 **德国 vs 阿根廷** 的看台配色：
- 阿根廷蓝：`#1A3A8A`
- 德国白：偏亮白灰
- 保留少量金色/亮点，模拟手机闪光灯或现场灯反射

#### 灯光与光束的文档结论
- **现在灯的位置问题先视为已解决**。
- **现在真正缺的是“可见天空”和“看起来像球场广播光”的表现层，不是单纯缺一个 Light 组件。**
- 若后续一定要恢复“扇形发光”的视觉印象，优先顺序应是：
  1. Gradient Sky / 黄昏天空
  2. 更低角度的夕阳主光
  3. 更强一点的 SpotLight 闪灯
  4. 最后才考虑便宜的假光束 mesh / cookie / billboard，而不是直接上高成本体积光

---

### 当前建议的实施优先级（新版）

```
P9.5.2  天空与黄昏氛围   ← 第一优先级，最影响第一眼观感
P9.5.3  进球绝杀反馈     ← 第二优先级，最影响演示高潮
P9.5.4  世界缩放 0.5     ← 第三优先级，影响所有站位和节奏
P9.5.5  其他演示细节     ← 第四优先级，负责“像不像那场比赛”
```

预计总工时：2–3 天；其中天空+绝杀反馈应先做，先把评委第一眼和高潮段落打穿。

---

## 锁定的方向决策

| 维度 | 决策 |
|---|---|
| **真球 / 摄像头 / 智能足球** | **完全砍掉**。机器人发射时 Unity 直接生成虚拟球 |
| **真机器人** | **保留**。海绵宝宝当输入设备，发射触发 NT `is_firing=true` → Unity 生成虚拟球 |
| **虚拟球员** | 1 队友（蓝队）+ 1 对手（红队）。复用 CharacterBuilder 改颜色 |
| **AI 推演** | 3 个**预设剧本**切换。不做状态机/RL |
| **球场** | 3×3m 室内最小尺寸，胶带框出来即可 |
| **Quest 3S** | **必做**。XR Origin 替换主相机，APK 部署 |
| **NT SDK** | 本期不集成，FakeDataGenerator 占位 |

> 方向锁定原因详见 `memory/project_competition_context.md`。萌芽赛道评审看的是"概念新颖 + 演示视频"，不是产品成熟度。

---

## 数据流（v4.0）

```
┌─────────────────────────────┐
│  真海绵宝宝机器人（旁边桌上）   │
│  发射键按下 → NT is_firing=true│
└──────────────┬──────────────┘
               │
   ┌───────────▼──────────────┐
   │  Unity (PC build)        │
   │                          │
   │  GameManager.OnShotFired │
   │     │                    │
   │     ├─► 生成虚拟球 ───┐   │
   │     │                ▼   │
   │     │  ScenarioPlayer    │
   │     │  随机选 1/3 剧本    │
   │     │  ├─ 成功传球        │
   │     │  ├─ 被拦截          │
   │     │  └─ 射偏            │
   │     │     │              │
   │     │     ▼              │
   │     │  虚拟队友/对手关键帧动画│
   │     │     │              │
   │     │     ▼              │
   │     │  评分 UI + 回放       │
   └─────┼────┬────────────────┘
         │    │
         ▼    ▼
   ┌─────────────┐
   │ Meta Quest 3S│  ← 同一份 Unity build 部署 APK
   │ 沉浸式观看   │
   └─────────────┘
```

---

## 开发阶段

| Phase | 内容 | 工期估计 |
|-------|------|---------|
| **P1** | 重建 Main.unity | 0.5 天 |
| **P2** | 加 1 队友 + 1 对手 GameObject | 0.5 天 |
| **P3** | 剧本系统核心 + 3 个剧本资产 | 2 天 |
| **P4** | 虚拟球生成 + 监听 OnShotFired | 0.5 天 |
| **P5** | 评分 UI + 慢动作回放 | 1 天 |
| **P6** | XR Origin + PCCameraController PC 自由视角 | 1 天 |
| **P7** | 演示流程：FlowManager + Intro + VRShoot + Replay + Audio | 3–4 天 |
| **P7.5** | 修 4 个已知 bug（视角 / HUD / 球 / 音频） | 0.5 天 |
| **P8** | Quest 3S APK 构建 + 手柄输入适配 | 1 天 |
| **P9** | 性能优化：Quest 72fps / Draw Call <200 / APK <150MB | 1–2 天 |
| **P10** | 演示视频拍摄 + 剪辑（分镜 + VR 录屏 + 旁白） | 1–2 天 |
| **总计** | | **~14–17 天** |

---

## 术语表

| 术语 | 全称 | 解释 |
|------|------|------|
| **NT** | NetworkTables | WPILib 的分布式键值存储，机器人 ↔ PC 通信协议 |
| **RoboRIO** | — | NI 出品的 FRC 机器人主控器，运行 WPILib C++ / Java |
| **WPILib** | Worcester Polytechnic Institute Library | FRC 官方机器人控制库 |
| **FRC** | FIRST Robotics Competition | 国际高中生机器人竞赛 |
| **URP** | Universal Render Pipeline | Unity 的可编程渲染管线 |
| **MQDH** | Meta Quest Developer Hub | Meta 官方 Quest 开发工具（调试/侧载/性能分析） |
| **sideload** | — | 不经商店，通过 USB/Wi-Fi 直装 APK 到 Quest |
| **APK** | Android Package Kit | Android 应用安装包格式（Quest 本质是 Android 设备） |
| **6-DoF** | 6 Degrees of Freedom | 头显可追踪 XYZ 位移 + 旋转 |
| **TrackedPoseDriver** | — | Unity XR 组件，自动将设备追踪位姿同步到 Camera Transform |
| **ScriptableObject** | — | Unity 数据容器，可存为 `.asset` 文件，适合配置/剧本 |
| **Draw Call** | — | CPU 向 GPU 发送的一次渲染指令，过多会导致性能下降 |
| **SetPass Call** | — | Shader 通道切换次数，比 Draw Call 更准确的性能指标 |
| **LOD** | Level of Detail | 根据距离切换模型精度的优化技术 |
| **ASTC** | Adaptive Scalable Texture Compression | Android/Quest 端推荐的纹理压缩格式 |
| **BGM** | Background Music | 背景音乐 |
| **SFX** | Sound Effects | 音效（射门/进球/拦截/UI 点击） |

---

## 版本里程碑

| 版本 | 日期 | 关键变更 |
|------|------|----------|
| v6.0 | 2026-05-20 | P1-P6 全完成：场景重建 + 剧本系统 + 评分 UI + XR Origin + PC 自由视角 + 视觉打磨 |
| v6.1 | 2026-05-22 | 场景优化（相机重命名 / AudioListener 去重 / 拼写修正 / Ground Static Batching） |
| v6.3 | 2026-05-25 | **P7.2 PC 原型重构**：三角色架构 + FPSPlayerController + PowerBarUI + MatchFlow 一轮循环 |
| v6.5 | 2026-05-27 | P7.1/P7.3/P7.4 脚本完成并挂入场景（commit: fa466f4） |
| v6.6 | 2026-05-29 | 角色身高微调 + 力度条 UI 调整；定位音频不能切换 bug |
| v6.7 | 2026-05-30 | Quest 3S APK sideload 成功；VR 真机暴露三个 bug，根因全部查明 |
| **v6.8** | **2026-05-31** | **文档充实**：新增架构总览/事件总线/输入映射/已知问题/测试清单/术语表/风险登记/版本里程碑 |
| v6.9 | 2026-05-31 | 修 4 个 bug（视角/HUD/球/音频）+ VR 复测通过；P7.2 VR 完整版完成 |
| v7.1 | 🔲 | P9.5 视觉演示打磨：WeatherController / CrowdAnimator / LightingConfigurator / Audio SFX / PostProcess 参数 — 代码完成，待 Unity Editor 挂载 |
| v7.0 | 🔲 | P9 性能优化完成 + Quest 稳定 72fps |
| v8.0 | 🔲 | P10 演示视频完成 + 项目交付 |

---

## 风险与应对

| 风险 | 概率 | 影响 | 应对预案 |
|------|------|------|----------|
| Quest 3S 演示当天没电 | 中 | 🔴 整个 VR 部分无法展示 | 备充电宝 + 长 USB 线；PC 端 Standalone 可独立跑完整流程（非 VR 模式） |
| APK sideload 失败/闪退 | 低 | 🟡 VR 演示掉链子 | 至少 2 台 Quest 3S 各装一份 APK；MQDH 录好 VR 内录屏作为视频备用 |
| 真机器人现场故障（电机/通信） | 中 | 🟡 「真机触发」环节缺位 | FakeDataGenerator 一键切换（按 F1 手动触发）；演示脚本说明「本次用模拟模式」 |
| Unity Editor Play 卡顿/崩溃 | 低 | 🟡 现场演示中断 | PC 提前 Build Standalone exe；关闭后台程序 |
| 视频拍摄效果不理想 | 中 | 🟡 评审印象打折 | 多录 3-4 条备选；VR 内录屏 + PC OBS + 实拍三路素材 |
| 音频系统 BGM 切换仍未修复 | 中 | 🟢 氛围打折但不影响核心功能 | 提前录好带 BGM 的演示视频；现场静音演示 + 口头说明 |
| Draw Call 过高导致 Quest 掉帧 | 中 | 🟡 VR 体验眩晕 | P9 优先做静态合批 + LOD；降低粒子预算 + 阴影距离 |

---

## P7.2 PC 原型架构（v6.3 重构后）

### 三角色责任表

| 角色 | GameObject | 控制者 | 何时可见 | 何时移动 |
|---|---|---|---|---|
| **Robot** | `Robot` | 静态 / FakeData | 全程 | 开局抛球时挥臂 |
| **Player** | `Player`（独立 GO，无 mesh） | FPSPlayerController | 全程（隐形化身） | 仅 Possession 阶段（MovementEnabled） |
| **Teammate** | `Teammate`（蓝色） | ScenarioPlayer | 仅 Shot 阶段 | 剧本关键帧插值 |

### 一轮玩法循环（MatchFlowController）

```
[Setup 1.5s]   球 attach Robot；Player MovementEnabled=false；Teammate hidden
   ↓
[Pass 1.0s]    球抛物线 Robot → Player 脚边（Coroutine lerp + 抛物线 Y）
   ↓
[Possession]   球 attach Player；MovementEnabled=true、ShootingEnabled=true
   ↓ (松开 LMB)
[Shot]         FpsCamera SetParent(null) 视角原地冻结
               ScenarioPlayer.SetOrigin(Player.transform)
               Teammate SetActive(true) → 剧本以 Player.position + yaw 为原点播放
               力度路由 ForcePlay(idx)：≥0.7 进球 / 0.4-0.7 射偏 / <0.4 拦截
   ↓
[Score]        FpsCamera reparent 回 Player/FpsAnchor；ScorePanel 弹窗
   ↓ 3s
[Cooldown]     Teammate SetActive(false) → 回 Setup
```

### 关键脚本清单

| 文件 | 职责 |
|---|---|
| `FPSPlayerController.cs` | 玩家输入：WASD（受 MovementEnabled 门控）、鼠标右键 look、LMB 蓄力 |
| `PowerBarUI.cs` | 屏幕底部力度条，绿→黄→红渐变 |
| `MatchFlowController.cs` | 一轮循环主控；AutoResolveRefs 强制按名称解析 Player / Teammate / FpsCamera |
| `ScenarioPlayer.cs` | 剧本关键帧插值 + 慢动作；`SetOrigin(Transform)` API + posOffset Y 强制 0 |
| `BallController.cs` | `AttachTo(parent, localOffset)` / `Detach()` 用于持球切换 |

### 修过的三个 bug（v6.3）

1. **视角飞走** → Player 拆成独立 GO，Shot 阶段 SetParent(null) 保证相机原地冻结
2. **球陷地下** → ScenarioPlayer 算 `posOffset` 时 Y 强制 0
3. **方向不对球门** → 改用 Player.transform.rotation（相机 yaw）作为剧本旋转

---

## 剧本系统设计

```csharp
[CreateAssetMenu(menuName = "SoccerBot/Scenario")]
public class Scenario : ScriptableObject
{
    public string scenarioName;        // "成功传球"
    public ScenarioOutcome outcome;    // Score / Intercepted / Missed
    public int finalScore;             // 100 / 30 / 50
    public List<Keyframe> keyframes;
}

[Serializable]
public struct Keyframe
{
    public float t;
    public Target target;              // Ball / Teammate / Opponent
    public Vector3 position;
    public Quaternion rotation;
    public string action;              // "shoot" / "intercept" / "score"
}
```

| 剧本 | 时长 | 关键事件 | 评分 |
|---|---|---|---|
| 成功传球 | 4s | 球→队友→射门→进球 | 100 |
| 被拦截 | 3s | 球飞行中被对手抢截 | 30 |
| 队友射偏 | 5s | 球→队友→射门→偏出 | 50 |

---

## 演示流程状态机设计（P7）

```
States:
  Intro → KickOff → Playing → Shooting → Replay → Scoring → End

Transitions:
  Intro ──(introDone)──▶ KickOff
  KickOff ──(ballFired)──▶ Playing
  Playing ──(swingDetected)──▶ Shooting
  Shooting ──(ballHit)──▶ Replay
  Replay ──(replayDone)──▶ Scoring
  Scoring ──(dismiss)──▶ End
```

---

## 文件结构（v6.8）

```
SoccerBot/
├── README.md
├── PLAN.md
├── .gitignore
├── robot/                                 # ⬇️ 优先级降低，本期可空
└── unity/
    ├── Assets/
    │   ├── Scenes/
    │   │   └── Main.unity                 # ✅ 已重建 + 优化
    │   └── Scripts/
    │       ├── Core/                      # ✅
    │       │   ├── RobotData.cs
    │       │   ├── IDataSource.cs
    │       │   ├── GameManager.cs
    │       │   ├── NTManager.cs           # ⬇️ 本期不用
    │       │   ├── DataBuffer.cs
    │       │   └── WhistleGenerator.cs
    │       ├── Robot/                     # ✅
    │       │   ├── RobotController.cs
    │       │   ├── RobotVisuals.cs
    │       │   ├── RobotPathTrail.cs
    │       │   └── CharacterBuilder.cs
    │       ├── Ball/                      # ✅
    │       │   ├── BallController.cs
    │       │   └── TrajectoryRenderer.cs
    │       ├── Player/                    # ✅ P7.2
    │       │   └── FPSPlayerController.cs
    │       ├── Camera/                    # ✅
    │       │   ├── SmoothFollow.cs
    │       │   ├── CameraSwitcher.cs
    │       │   └── PCCameraController.cs
    │       ├── Simulation/                # ✅
    │       │   └── FakeDataGenerator.cs
    │       ├── Scenario/                  # ✅
    │       │   ├── Scenario.cs
    │       │   ├── ScenarioPlayer.cs
    │       │   ├── ScenarioTrigger.cs
    │       │   └── ScenarioFactory.cs
    │       ├── XR/                        # ✅
    │       │   └── XRSetup.cs
    │       ├── Flow/                      # ✅（脚本挂载，音频槽待填）
    │       │   ├── MatchFlowController.cs
    │       │   ├── IntroManager.cs
    │       │   ├── ReplayDirector.cs
    │       │   └── AudioManager.cs
    │       ├── UI/                        # ✅
    │       │   ├── StatusPanel.cs
    │       │   ├── ScorePanel.cs
    │       │   ├── ScoreBoard.cs
    │       │   ├── PowerBarUI.cs
    │       │   └── IntroPanel.cs
    │       ├── Field/                     # ✅
    │       │   └── FieldBuilder.cs
    │       ├── Effects/                   # ✅ P9.5
    │       │   ├── OutcomeFx.cs
    │       │   ├── PolishVolumeBuilder.cs
    │       │   ├── WeatherController.cs          # NEW
    │       │   ├── CrowdAnimator.cs              # NEW
    │       │   └── LightingConfigurator.cs       # NEW
    │       └── Editor/                    # ✅
    │           ├── BuildAndroid.cs
    │           ├── ScenarioFactory.cs
    │           └── AudioManagerWirer.cs
    ├── Assets/Scenarios/                  # ✅
    │   ├── ScoreSuccess.asset
    │   ├── Intercepted.asset
    │   └── ShotMissed.asset
    └── Packages/manifest.json
```

---

## 立即下一步

### 当前主线 — 视觉演示打磨（P9.5）

> 原则：**演示效果是比赛评分的唯一依据**。技术完备后，把所有精力投入到「看得到」的东西上。

1. **材质升级**：Grass 贴图草坪换高清纹理 + 球门金属 PBR + 足球皮革反射 + 角色球衣质感
2. **光影增强**：主方向光调暖色（黄昏/比赛日氛围）+ 四角聚光灯加体积光 + 阴影质量调优（Quest 端上限内）
3. **天气粒子**：轻度雨丝 + 地面潮湿暗色 + 雾气（URP Fog）+ 可切换晴天/雨天
4. **观众氛围**：增强 StadiumBuilder 看台 → 加浮动小色块模拟人群挥手 + 进球时 crowd cheer 音频（已有素材）+ 纸屑粒子
5. **场地比例**：评估当前 12m×18m 球场 → 相对机器人/球员尺寸是否偏小 → 统一缩放（例如球+人物缩小 0.7×，或场地扩大 1.4×）

### 后续（等 P9.5 画面满意后再启动）

- **P9 性能优化**：Quest 稳定帧率、减少 Draw Call、控制 APK 体积
- **P10 演示视频**：画面 + 节奏 + 音频稳定后录屏
- **P11 智能足球**：暂缓，演示主线收口后再决定

---

## P11 — 智能足球训练模式（BS-BT91 BLE IMU，当前暂缓）

> **目标**：将包裹了 3D 打印足球外壳的 BS-BT91 九轴 IMU 模块接入 Unity，在 VR 训练模式中实时显示足球的数字孪生（旋转同步）。
>
> BS-BT91 是一款 BLE 5.0 无线九轴姿态传感器（陀螺仪 + 加速度计 + 磁力计），内置卡尔曼滤波，输出欧拉角 / 四元数 / 加速度 / 角速度，数据率默认 10Hz（最高 200Hz），空旷传输距离 90m。模块重量约 9g，非常适合同步封装在 3D 打印足球壳内。

### 核心挑战

| 挑战 | 说明 |
|------|------|
| **IMU 无位置信息** | IMU 只能输出**姿态（旋转）**，不能输出空间位置。位置跟踪需要 UWB/视觉等外部系统（不在本期范围） |
| **BLE 跨平台** | Quest (Android) 可用 Java BLE API；PC (Windows) 需要 WinRT BLE 或中转方案 |
| **蓝牙协议细节缺失** | 本地资料只有产品规格书，具体数据帧格式在语雀外链页面 `cm730dbi9gggsged`，需要单独获取或通过上位机逆向 |

### 分阶段实施计划

```
P11.1  独立 Training 场景语义
       ├─ Training Mode 不再依赖 Robot / 海绵宝宝 / 比赛流程站位
       ├─ 进入 Training 后只保留训练场、训练球、训练 UI
       └─ 训练对象由 SmartBall 子系统统一生成/管理

P11.2  数据模型 + Mock 驱动（可 PC 独立调试）
       ├─ SmartBallData.cs：封装四元数 / 角速度 / 时间戳
       ├─ ISmartBallSource：独立于 IDataSource 的训练数据接口
       ├─ MockSmartBallSource：输出可见的训练态假数据
       └─ SmartBallController.cs：同时驱动球的旋转与训练用位置表现

P11.3  训练场集成
       ├─ 运行时或场景内生成独立训练草地 / 中线 / 中心圈 / 球门
       ├─ SmartBall 默认摆在训练场核心区域，不再落在海绵宝宝脚下
       ├─ UI 只保留训练状态卡，不再显示主菜单遮罩
       └─ Mock 数据变化时，球在画面中也要有对应运动表现

P11.4  Android BLE 插件（Quest 端真机联调）
       ├─ 扫描 BS-BT91 设备（按名称/服务 UUID 过滤）
       ├─ 连接 → 订阅 Notify Characteristic → 接收数据帧
       ├─ 数据帧解析器（按 BS-BT91 协议格式）
       └─ 真实数据替换 Mock source，不改 Training Mode 上层逻辑

P11.5  VR 内展示打磨
       ├─ 球大小匹配真实足球（直径 ~22cm）
       ├─ 旋转平滑滤波（减少 IMU 抖动）
       ├─ 训练视角、场地、UI 可读性调整
       └─ 视需求增加踢球检测 / 轨迹表现
```

### 架构图

```
┌──────────────────────────────────────────────────────┐
│  真足球（3D 打印壳 + BS-BT91）                          │
│  BLE 5.0 广播 → 欧拉角 / 四元数 / 加速度 (10Hz)        │
└────────────────────┬─────────────────────────────────┘
                     │
         ┌───────────▼───────────┐
         │  ISmartBallSource     │  同 IDataSource 模式
         │  ├─ MockSmartBallSource│  PC 调试：鼠标拖拽
         │  └─ BleSmartBallSource │  Quest：Android BLE
         └───────────┬───────────┘
                     │ SmartBallData { rotation, accel }
                     ▼
         ┌───────────────────────┐
         │  SmartBallController  │  MonoBehaviour
         │  transform.rotation = │  挂在 SmartBall.prefab
         │    Quaternion.Slerp() │
         └───────────┬───────────┘
                     │
         ┌───────────▼───────────┐
         │  SmartBall GameObject │
         │  ├─ MeshRenderer      │  足球 3D 模型
         │  ├─ 拖尾特效 (可选)     │
         │  └─ 踢球检测 (可选)     │
         └───────────────────────┘
```

### 新增文件清单

| 文件 | 路径 | 职责 |
|------|------|------|
| `SmartBallData.cs` | `Scripts/SmartBall/` | 数据结构：欧拉角、四元数、加速度、时间戳 |
| `ISmartBallSource.cs` | `Scripts/SmartBall/` | 数据源接口 |
| `MockSmartBallSource.cs` | `Scripts/SmartBall/` | PC 调试：鼠标拖拽旋转 + 键盘模拟 |
| `BleSmartBallSource.cs` | `Scripts/SmartBall/` | Android BLE 连接 + 数据帧解析 |
| `SmartBallController.cs` | `Scripts/SmartBall/` | 接收数据 → 驱动 Transform 旋转 |
| `SmartBall.prefab` | `Assets/Prefabs/` | 足球 3D 模型预制体 |
| `BlePlugin.aar` | `Assets/Plugins/Android/` | Android BLE 原生插件（可选第三方） |

### 实施顺序建议

1. **先 P11.1 + P11.2**：把足球模型放进场景，用 Mock 数据让球转起来。**30 分钟内可在 PC Editor 看到效果**。
2. **再 P11.3**：挂到 Training 菜单入口，打通 UI 流程。
3. **最后 P11.4**：需要 BS-BT91 真机 + 蓝牙协议文档。拿到数据帧格式后一个下午可完成。

### 风险与应对

| 风险 | 概率 | 应对 |
|------|------|------|
| BS-BT91 蓝牙协议文档缺失 | 中 | 用官方上位机抓 BLE 数据包逆向；或先用 Mock 模式演示 |
| Android BLE 插件兼容性 | 低 | 用 Unity 官方 `AndroidJavaObject` 直接调 Android BLE API，不依赖第三方 |
| IMU 抖动导致球旋转抽搐 | 中 | `Quaternion.Slerp` + 低通滤波平滑 |
| 足球 3D 模型没有现成的 | 低 | Unity Asset Store 免费足球模型 / 用 Sphere + 贴图凑合 |

---

## 未来展望（PPT 占位用）

### 感知层 — 让机器人有「眼睛」

- **真智能足球**：3D 打印外壳 + UWB 定位传感器 + LAN 数据上报，替代 Unity 虚拟球生成。真球在物理世界飞行，Unity 实时渲染球的数字孪生
- **纯视觉球检测**：USB 俯视摄像头 + OpenCV 颜色/ArUco 标记追踪，不需要在球里装传感器。成本更低，部署更快
- **多摄像头融合**：2-3 个廉价 USB 摄像头交叉覆盖 3×3m 球场，三角测量提升定位精度到 ±2cm
- **机器人 × VR 真实空间联动定位（未来）**：当前手头没有 Limelight/LL，本期不接入 AprilTag 视觉定位。后续如果补上 LL，可让机器人先通过固定场地 AprilTag 获取全场初始位姿，VR 端通过启动校准把 Quest 本地空间对齐到同一球场坐标系，再通过 NetworkTables / UDP / WebSocket 同步 `player_pose_in_field` 与 `robot_pose_in_field`，让机器人计算玩家相对位置。固定球门中点启动可以作为低成本初始定位备选；球衣 AprilTag 可作为锁定真人位置的增强方案，但不作为主定位链路。

### 决策层 — 让队友有「脑子」

- **真 AI 决策**：从预设剧本升级到有限状态机 → 行为树 → 强化学习（PPO/SAC）。训练环境 = Unity ML-Agents，奖励函数 = 进球 +1 / 被拦截 -1 / 传球成功率
- **对手自适应**：根据玩家历史数据调整对手难度（拦截半径 / 反应延迟 / 跑位速度）
- **阵型战术库**：预设 4-5 种战术（高位逼抢 / 防守反击 / 边路突破），AI 根据场上态势动态切换

### 交互层 — 让体验更「真实」

- **NT 双向通信**：Unity 反向控制机器人瞄准电机——虚拟世界算出的最佳传球路线，真机器人自动对齐并发射，形成「虚拟→物理」闭环
- **触觉反馈**：Quest 手柄振动 + 可选 haptic 手套，射门/拦截时有力度差异的触觉提示
- **语音教练**：TTS 实时播报战术建议（"往右传！""射门角度不够！"）

### 扩展层 — 从 1v1 到全场

- **4v4 完整阵型**：扩展到真实 FRC 比赛规模，8 个虚拟球员 + 1 个真人操作的真机器人
- **多人协作**：多台 Quest 3S + 多机器人同场训练，VR 内看到队友的第一人称视角
- **云端回放**：比赛数据上传云端，教练/队友可在 VR 内回放任意视角的比赛录像，标注战术要点
