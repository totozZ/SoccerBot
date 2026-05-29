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
- [ ] **P7.2 (VR 完整版)** Quest 手柄绑定（A/Trigger 蓄力射门 + 体感方向追踪）
- [x] **P7.3** ReplayDirector 演算导演（根据玩家操作选分支剧本 + 慢动作回放）
- [x] **P7.4** AudioManager BGM 管理（背景音乐 + 音效事件触发）
- [x] **P8** Quest 3S APK 构建 + 部署（已成功 sideload 启动，A 键意外可触发射门）
- [ ] **P9** 性能优化（Quest 帧率 / Draw Call / 脚本热点）
- [ ] **P10** 演示视频拍摄 + 剪辑

---

> 版本: v6.5.0 | 日期: 2026-05-29 | 状态: P7.1 IntroManager + P7.3 ReplayDirector + P7.4 AudioManager 脚本完成，待 Unity 场景挂载；P7.2 VR 完整版手柄绑定为下一步
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

## 当前进度总览（v6.1）

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
| **P6.5 视觉打磨** | ✅ 完成 | FieldBuilder 程序化球场（绿地/条纹/边线/中圈/双球门）+ PolishVolumeBuilder（Bloom/Vignette/ColorAdj/ACES）+ OutcomeFx（金/红/灰三种粒子）+ 4 角 SpotLight + 3 静态机位（OverheadCam/SideCam/BehindRobotCam，1/2/3 数字键直切）+ HUD `_showInDemo` 开关 + ScoreBoard 顶部比分牌 |
| **P7.2 PC 原型** | ✅ 完成（v6.3 重构） | **三角色架构**：Robot（黄色海绵宝宝，`(0,0,-6)`，开场传球 NPC）/ Player（**新独立 GO**，`(0,0,2)` Y=180，挂 FpsAnchor+FpsCamera+FPSPlayerController，**隐形**，玩家化身）/ Teammate（蓝色，剧本驱动 NPC，仅 Shot 阶段 SetActive）。FPSPlayerController 加 `MovementEnabled` 门控（仅 Possession 开）。MatchFlowController 拆出 `_playerTransform`/`_teammateTransform`，HandlePlayerShot 改为 detach FpsCamera 冻结视角 + ScenarioPlayer.SetOrigin(Player) + 玩家 yaw 决定射门方向。ScenarioPlayer.cs 加 SetOrigin(Transform) API + posOffset Y 强制 0（修球陷地）。PowerBarUI / 力度阈值 / 一轮一传循环全保留 |
| **演示流程其余项** | ✅ 脚本完成，待挂载 | IntroManager + IntroPanel（P7.1）+ ReplayDirector（P7.3）+ AudioManager（P7.4）脚本已写，未提交，需在 Unity 场景中挂载组件 |
| **Quest 3S 部署** | ✅ APK 跑通 | sideload 后 VR 内能看到球场 / Robot / 剧本演算；A 键已可触发射门（fallback 路径，需正式绑定）|
| NTManager / robot C++ | ⬇️ 优先级降低 | 真机联调本期不做，FakeData 顶 |

---

## 锁定的方向决策

| 维度 | 决策 |
|---|---|
| **真球 / 摄像头 / 智能足球** | **完全砍掉**。机器人发射时 Unity 直接生成虚拟球 |
| **真机器人** | **保留**。海绵宝宝当输入设备，发射触发 NT `is_firing=true` → Unity 生成虚拟球 |
| **虚拟球员** | 1 队友（蓝队）+ 1 对手（红队）。复用 [CharacterBuilder](unity/Assets/Scripts/Robot/CharacterBuilder.cs) 改颜色 |
| **AI 推演** | 3 个**预设剧本**切换：①成功传球得分 ②被对手拦截 ③队友射偏。按按钮选/随机播。不做状态机/RL |
| **球场** | 3×3m 室内最小尺寸，胶带框出来即可 |
| **Quest 3S** | **必做**（项目题目就是 VR 方向）。XR Origin 替换主相机，APK 部署 |
| **NT SDK** | 本期不集成，FakeDataGenerator 占位。真机联调列入展望 |

> 方向锁定原因详见 [memory/project_competition_context.md](memory/project_competition_context.md)。萌芽赛道评审看的是"概念新颖 + 演示视频"，不是产品成熟度。

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
| **P1** | 重建 [Main.unity](unity/Assets/Scenes/Main.unity) | 0.5 天 |
| **P2** | 加 1 队友 + 1 对手 GameObject（CharacterBuilder 改色版） | 0.5 天 |
| **P3** | **剧本系统核心**：Scenario / ScenarioPlayer / ScenarioTrigger + 3 个剧本资产 | 2 天 |
| **P4** | 虚拟球生成 + 监听 OnShotFired → 触发剧本播放 | 0.5 天 |
| **P5** | 评分 UI + 慢动作回放（Time.timeScale = 0.3） | 1 天 |
| **P6** | XR Origin + PCCameraController PC 自由视角 | 1 天 |
| **P7** | **演示流程**：FlowManager + IntroManager + VRShootController + ReplayDirector + AudioManager | 3–4 天 |
| **P8** | Quest 3S APK 构建 + 手柄输入适配 | 1 天 |
| **P9** | 性能优化（Quest 帧率 / Draw Call / 脚本热点） | 1–2 天 |
| **P10** | 演示视频拍摄 + 剪辑 | 1 天 |
| **总计** | | **~12–14 天** |

---

## P7.2 PC 原型架构（v6.3 重构后）

### 三角色责任表

| 角色 | GameObject | 控制者 | 何时可见 | 何时移动 |
|---|---|---|---|---|
| **Robot** | `Robot` | 静态 / FakeData | 全程 | 开局抛球时挥臂 |
| **Player** | `Player`（独立 GO，无 mesh） | FPSPlayerController（WASD+鼠标） | 全程隐形 | 仅 Possession 阶段 |
| **Teammate** | `Teammate`（CharacterBuilder 蓝色） | ScenarioPlayer 关键帧 | 仅 Shot 阶段 SetActive | 剧本驱动 |

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
| [FPSPlayerController.cs](unity/Assets/Scripts/Player/FPSPlayerController.cs) | 玩家输入：WASD（受 MovementEnabled 门控）、鼠标右键 look、LMB 蓄力、`OnShoot(power, direction)` 事件 |
| [PowerBarUI.cs](unity/Assets/Scripts/UI/PowerBarUI.cs) | 屏幕底部力度条，绿→黄→红渐变，订阅 OnChargeChanged |
| [MatchFlowController.cs](unity/Assets/Scripts/Flow/MatchFlowController.cs) | 一轮循环主控；AutoResolveRefs 强制按名称解析 Player / Teammate / FpsCamera |
| [ScenarioPlayer.cs](unity/Assets/Scripts/Scenario/ScenarioPlayer.cs) | 剧本关键帧插值 + 慢动作；`SetOrigin(Transform)` API 让 MatchFlow 把原点切到 Player；posOffset Y 强制 0 防球陷地 |
| [BallController.cs](unity/Assets/Scripts/Ball/BallController.cs) | `AttachTo(parent, localOffset)` / `Detach()` 用于持球切换 |

### 修过的三个 bug（v6.3）

1. **视角飞走** → Player 拆成独立 GO 后 FpsCamera 不再是 Teammate 子物体；Shot 阶段 SetParent(null) 进一步保证相机原地冻结
2. **球陷地下** → ScenarioPlayer 算 `posOffset` 时 Y 强制 0，球关键帧（Y 0.3-0.9）原样保留地面相对高度
3. **方向不对球门** → 改用 Player.transform.rotation（已经等于相机 yaw）作为剧本旋转，玩家瞄哪儿球飞哪儿

---

每个剧本是一个 ScriptableObject，包含一串关键帧：

```csharp
// Scenario.cs (待写)
[CreateAssetMenu(menuName = "SoccerBot/Scenario")]
public class Scenario : ScriptableObject
{
    public string scenarioName;        // "成功传球"
    public ScenarioOutcome outcome;    // Score / Intercepted / Missed
    public int finalScore;             // 100 / 30 / 50
    public List<Keyframe> keyframes;   // 时间轴关键帧
}

[Serializable]
public struct Keyframe
{
    public float t;                    // 时间 (秒)
    public Target target;              // Ball / Teammate / Opponent
    public Vector3 position;
    public Quaternion rotation;
    public string action;              // "shoot" / "intercept" / "score"
}
```

**3 个剧本预设值（粗略）**：

| 剧本 | 时长 | 关键事件 | 评分 |
|---|---|---|---|
| 成功传球 | 4s | 球→队友→射门→进球 | 100 |
| 被拦截 | 3s | 球飞行中被对手抢截 | 30 |
| 队友射偏 | 5s | 球→队友→射门→偏出 | 50 |

`ScenarioPlayer.cs` 只做关键帧插值，不需要状态机。简单到不能再简单。

---

## 演示流程状态机设计（P7）

### FlowManager — 流程状态机

整个演示由 `FlowManager` 驱动的有限状态机串联：

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

```csharp
// FlowManager.cs (待写)
public enum DemoState { Intro, KickOff, Playing, Shooting, Replay, Scoring, End }

public class FlowManager : MonoBehaviour
{
    public DemoState CurrentState;
    public event Action<DemoState> OnStateChanged;
    
    // 每个状态对应一个 IPhaseController 组件
    public void TransitionTo(DemoState next);
}
```

### P7.1 IntroManager — 启动画面

| 功能 | 说明 |
|------|------|
| 黑底 Canvas | 全屏黑色背景 + 居中白色文字 |
| 文案显示 | 逐行淡入：比赛名称 → 时间节点 → 关键球员 |
| BGM | AudioManager 协同，淡入播放 |
| 过渡 | 3-4 秒后文字淡出 → 相机黑屏 → 亮起切入比赛视角 |
| 跳过 | 任意按键跳过 intro |

### P7.2 VRShootController — 体感射门 ★核心

| 功能 | 说明 |
|------|------|
| 输入源 | Quest 右手柄位置 + 速度（`XR Controller.velocity`） |
| 触发判定 | 手柄速度 > 阈值 → 判定为挥臂射门 |
| 方向计算 | 手柄瞬时速度方向 = 出球方向 |
| 力度计算 | 速度大小映射到球初速（clamp 合理范围） |
| 传球 | 左手柄触发（或按键 B/Y）→ 传给队友方向 |
| PC 模拟 | 鼠标左键按住拖拽 → 松开 = 射门（方向 = 拖拽向量） |
| 反馈 | 击中瞬间手柄振动 + 音效 |

```csharp
// VRShootController.cs (待写)
public class VRShootController : MonoBehaviour
{
    public float swingThreshold = 3.0f;     // 最小挥臂速度 m/s
    public float maxShotSpeed = 25.0f;      // 最大出球速度
    
    public event Action<Vector3, float> OnShot;  // 方向, 速度
    
    void Update()
    {
        // 1. 从 XR Controller 读速度
        // 2. 速度 > 阈值 → 触发 OnShot
        // 3. 短暂禁用防止连发
    }
}
```

### P7.3 ReplayDirector — 演算导演

| 功能 | 说明 |
|------|------|
| 输入 | 玩家射门结果（方向 + 力度 + 命中目标？） |
| 分支 | 进球 → ScoreSuccess / 被拦截 → Intercepted / 射偏 → ShotMissed |
| 播放 | 调用现有 ScenarioPlayer 播放选中的剧本 |
| 慢动作 | Time.timeScale = 0.3，关键帧慢放 |
| 相机 | 自动切换到最佳观看角度（侧面/俯视） |

### P7.4 AudioManager — 音频管理

| 功能 | 说明 |
|------|------|
| BGM | 根据 DemoState 切换背景音乐 |
| 音效 | 射门、进球、拦截、UI 点击等事件音效 |
| 淡入淡出 | 状态切换时平滑过渡 |
| 音量 | 可配置的主音量 / BGM 音量 / SFX 音量 |

---

## 文件结构（v6.0）

```
SoccerBot/
├── README.md                              # 项目说明
├── PLAN.md                                # 本文件
├── .gitignore
├── robot/                                 # ⬇️ 优先级降低，本期可空
└── unity/
    ├── Assets/
    │   ├── Scenes/
    │   │   └── Main.unity                 # ✅ 已重建 + 优化
    │   └── Scripts/
    │       ├── Core/                      # ✅ 已完成
    │       │   ├── RobotData.cs
    │       │   ├── IDataSource.cs
    │       │   ├── GameManager.cs
    │       │   ├── NTManager.cs           # ⬇️ 本期不用
    │       │   └── DataBuffer.cs
    │       ├── Robot/                     # ✅ 已完成
    │       │   ├── RobotController.cs
    │       │   ├── RobotVisuals.cs
    │       │   ├── RobotPathTrail.cs
    │       │   └── CharacterBuilder.cs
    │       ├── Ball/                      # ✅ 已完成
    │       │   ├── BallController.cs
    │       │   └── TrajectoryRenderer.cs
    │       ├── UI/                        # ✅ 已完成
    │       │   ├── StatusPanel.cs
    │       │   ├── ScorePanel.cs
    │       │   ├── ScoreBoard.cs
    │       │   ├── PowerBarUI.cs          # ✅ P7.2 力度条
    │       │   └── IntroPanel.cs          # 🔲 P7.1 待写：启动画面
    │       ├── Player/                    # ✅ P7.2 新增
    │       │   └── FPSPlayerController.cs
    │       ├── Camera/                    # ✅ 已完成
    │       │   ├── SmoothFollow.cs
    │       │   ├── CameraSwitcher.cs
    │       │   └── PCCameraController.cs
    │       ├── Simulation/                # ✅ 已完成
    │       │   └── FakeDataGenerator.cs
    │       ├── Scenario/                  # ✅ 已完成
    │       │   ├── Scenario.cs
    │       │   ├── ScenarioPlayer.cs      # P7.2 加 SetOrigin(Transform) + posOffset Y=0
    │       │   ├── ScenarioTrigger.cs
    │       │   └── ScenarioFactory.cs
    │       ├── XR/                        # ✅ 已完成
    │       │   └── XRSetup.cs
    │       ├── Flow/                      # ✅ P7.2 PC 原型已就位
    │       │   ├── MatchFlowController.cs # ✅ 一轮循环主控
    │       │   ├── FlowManager.cs         # 🔲 P7 待写：总状态机
    │       │   ├── IntroManager.cs        # 🔲 P7.1 待写：启动画面
    │       │   ├── VRShootController.cs   # 🔲 P7.2 VR 完整版
    │       │   ├── ReplayDirector.cs      # 🔲 P7.3 演算导演
    │       │   └── AudioManager.cs        # 🔲 P7.4 BGM + 音效
    │       └── Editor/                    # ✅ 已完成
    │           ├── BuildAndroid.cs
    │           └── ScenarioFactory.cs
    ├── Assets/Scenarios/                  # ✅ 已完成
    │   ├── ScoreSuccess.asset
    │   ├── Intercepted.asset
    │   └── ShotMissed.asset
    └── Packages/manifest.json
```

---

## 立即下一步

**P1–P7.4 脚本全部完成**：IntroManager / IntroPanel / ReplayDirector / AudioManager 四个脚本今天新写，未提交，需在 Unity 场景挂载。

**立即下一步：在 Unity 场景挂载新脚本**

1. 创建全屏黑色 Canvas → 挂 `IntroPanel`，加 3 个 TMP 文字子物体
2. 在 `MatchFlowController` 同 GO 上挂 `IntroManager`，填写三行文案
3. 任意 GO 挂 `ReplayDirector`（自动找 CameraSwitcher）
4. 任意 GO 挂 `AudioManager`，Inspector 里拖入 BGM/SFX 音频素材

**P7.2 VR 完整版**（脚本已有多源绑定，Quest A/Trigger 已接入）：在 Quest 上测试手柄蓄力射门是否稳定，确认头显朝向作为射门方向是否正确。

**P9 性能优化** 可穿插，Quest 帧率有问题先看。
**P10 演示视频** 等 VR 输入跑通后录屏。

---

## 未来展望（PPT 占位用）

下面这些列在演示视频结尾的"展望"页，当作加分项展示，但本期不做：

- **真智能足球**：3D 打印外壳 + UWB 定位传感器 + LAN 数据上报，替代虚拟球
- **纯视觉球检测**：USB 俯视摄像头 + OpenCV 颜色/ArUco → 不需要传感器
- **NT 双向通信**：Unity 把虚拟队友/对手位置回传机器人，机器人自动瞄准
- **真 AI 决策**：状态机 → 强化学习，替代预设剧本
- **4v4 完整阵型**：扩展到真实比赛规模
- **多人协作**：多台 Quest 3S + 多机器人同场训练
