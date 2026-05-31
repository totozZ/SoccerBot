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
- [ ] **P7.2 (VR 完整版)** Quest 手柄绑定（A/Trigger 蓄力射门 + 体感方向追踪）— ⚠️ 真机暴露三 bug，根因已查明待修：①视角翻向背后 ②介绍/HUD 右上角 ③球过大缩 80%
- [x] **P7.3** ReplayDirector 演算导演（根据玩家操作选分支剧本 + 慢动作回放）
- [x] **P7.4** AudioManager BGM 管理（背景音乐 + 音效事件触发）
- [ ] **P7.5** 修 4 个已知 bug（详见「已知问题」节）— 约 0.5 天
- [x] **P8** Quest 3S APK 构建 + 部署（已成功 sideload 启动，A 键意外可触发射门）
- [ ] **P9** 性能优化 — 目标：Quest 稳定 72fps / Draw Call < 200 / APK < 150MB
- [ ] **P10** 演示视频拍摄 + 剪辑 — 分镜脚本 / 旁白文案 / VR 内录屏

---

> 版本: v6.8.0 | 日期: 2026-05-31 | 状态: 文档充实——新增架构总览/事件总线/输入映射/已知问题/测试清单/术语表/风险登记/版本里程碑 8 个段落；代码未动，VR 三 bug + 音频 bug 待下次修
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

## 当前进度总览（v6.8）

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
| **VR 真机三 bug** | 🔲 根因已查明待修 | 视角翻向背后 / HUD 右上角 / 球过大 |
| **Quest 3S 部署** | ✅ APK 跑通 | sideload 成功，VR 内可见球场和剧本演算 |
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

> 以下 4 个 bug 均在 v6.7.0 诊断中确认根因，**待下次实施修复**。

| # | 症状 | 影响平台 | 根因 | 严重度 |
|---|------|----------|------|--------|
| ① | **射门后视角翻向背后** | VR only | `MatchFlowController` Shot 阶段 `SetParent(null)` detach 相机，触发 `FPSPlayerController` 的 world-space 旋转分支，与头显 TrackedPoseDriver 打架 | 🔴 阻塞 VR 体验 |
| ② | **FIFA 介绍/HUD 跑到右上角** | VR only | `XRSetup` 转换 Overlay→World 时未重置 RectTransform pivot | 🟡 影响 UI 可用性 |
| ③ | **足球过大** | 双端 | 场景 Ball Transform `m_LocalScale: 2.2` | 🟢 纯数值 |
| ④ | **BGM 只能播一首，切不动** | 双端 | 场景有两份 AudioManager GO，matchBGM/replayBGM/sfxShoot 槽为空 → `PlayBGM(null)` 直接 return | 🟡 影响演示氛围 |

### 修复方案摘要

**① 视角翻向背后** — 删 detach：
- `MatchFlowController.cs:319-324` 删 `_fpsCamera.SetParent(null, true)` 及 `_camRestPos/_camRestRot` 缓存
- `MatchFlowController.cs:445-453` 删 reparent + `localPosition=zero`
- `FPSPlayerController.cs:150-155` 删 `parent == null` 的 detached 旋转分支

**② HUD 右上角** — `XRSetup.cs` 转换后重置 pivot：
```csharp
rt.pivot = new Vector2(0.5f, 0.5f);
rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
rt.anchoredPosition3D = Vector3.zero;
```

**③ 球大小** — 手动改 Main.unity L6468：`m_LocalScale: 2.2` → `1.76`

**④ 音频** — `AudioManager.cs` Awake 加 `Destroy(this)` 去重 + 公开 `SuppressPhaseBGM` 开关供 IntroManager 门控 + 跑 `SoccerBot/Wire AudioManager Clips` 填槽

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
| v6.9 | 🔲 下次 | 修 4 个 bug（视角/HUD/球/音频）+ 复测 |
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
    │       ├── Effects/                   # ✅
    │       │   ├── OutcomeFx.cs
    │       │   └── PolishVolumeBuilder.cs
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

### 下次实施（v6.9）— 约 0.5 天

1. **修 4 个已知 bug**（详见「已知问题」节修复方案摘要）
2. **编辑器填槽**：跑 `SoccerBot/Wire AudioManager Clips` → 存场景
3. **Quest 复测**：sideload 跑一轮，确认 3 个 VR bug 消失 + BGM 正常切换
4. **如时间允许**：换 Opponent 模型（改 `ArtUpgradeHelper.cs` → 跑菜单 → 存场景）

### 后续

- **P9 性能优化** 穿插进行，先跑 Quest Profiler 看瓶颈
- **P10 演示视频** 等 VR 输入跑通后录屏

---

## 未来展望（PPT 占位用）

### 感知层 — 让机器人有「眼睛」

- **真智能足球**：3D 打印外壳 + UWB 定位传感器 + LAN 数据上报，替代 Unity 虚拟球生成。真球在物理世界飞行，Unity 实时渲染球的数字孪生
- **纯视觉球检测**：USB 俯视摄像头 + OpenCV 颜色/ArUco 标记追踪，不需要在球里装传感器。成本更低，部署更快
- **多摄像头融合**：2-3 个廉价 USB 摄像头交叉覆盖 3×3m 球场，三角测量提升定位精度到 ±2cm

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
