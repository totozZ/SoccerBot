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
- [ ] **P7** 演示流程状态机 FlowManager（串联启动→比赛→操作→演算→评分全流程）
- [ ] **P7.1** IntroManager 启动画面（黑底白字 + 比赛背景简介 + BGM 淡入）
- [ ] **P7.2** VRShootController 体感射门（手柄速度/方向追踪 → 射门/传球判定）
- [ ] **P7.3** ReplayDirector 演算导演（根据玩家操作选分支剧本 + 慢动作回放）
- [ ] **P7.4** AudioManager BGM 管理（背景音乐 + 音效事件触发）
- [ ] **P8** Quest 3S APK 构建 + 手柄输入适配（代码已写，待 Android 环境）
- [ ] **P9** 性能优化（Quest 帧率 / Draw Call / 脚本热点）
- [ ] **P10** 演示视频拍摄 + 剪辑

---

> 版本: v6.1.1 | 日期: 2026-05-27 | 状态: P1–P6.5 完成 + 修复轮（1/2/3 切到 InputSystem / 球场放大到 8×12m / Robot 等重摆位 / HUD 精简显示），P7 演示流程设计已明确，开发中
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
| **演示流程** | 🔲 P7 开发中 | FlowManager 状态机 + Intro 启动画面 + VR 体感射门 + 演算导演 + BGM |
| **Quest 3S 部署** | ⚠️ 代码就绪 | BuildAndroid.cs + 手柄 Trigge 输入已写；Android 环境待网络安装 |
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

## 剧本系统设计

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
    │       │   └── IntroPanel.cs          # 🔲 P7.1 待写：启动画面
    │       ├── Camera/                    # ✅ 已完成
    │       │   ├── SmoothFollow.cs
    │       │   ├── CameraSwitcher.cs
    │       │   └── PCCameraController.cs
    │       ├── Simulation/                # ✅ 已完成
    │       │   └── FakeDataGenerator.cs
    │       ├── Scenario/                  # ✅ 已完成
    │       │   ├── Scenario.cs
    │       │   ├── ScenarioPlayer.cs
    │       │   ├── ScenarioTrigger.cs
    │       │   └── ScenarioFactory.cs
    │       ├── XR/                        # ✅ 已完成
    │       │   └── XRSetup.cs
    │       ├── Flow/                      # 🔲 P7 待写：演示流程
    │       │   ├── FlowManager.cs         # 状态机主控
    │       │   ├── IntroManager.cs        # 启动画面
    │       │   ├── VRShootController.cs   # 体感射门
    │       │   ├── ReplayDirector.cs      # 演算导演
    │       │   └── AudioManager.cs        # BGM + 音效
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

**P1–P6.5 已完成**：场景优化完毕，剧本 + 评分 + PC 自由视角 + 视觉打磨（球场/后处理/特效/灯光/多机位/HUD）全跑通。

**P7 演示流程** 是当前主战场，不依赖 Android：

1. **P7.1 IntroManager** — 启动画面 Canvas + 文字淡入淡出（当天可完成）
2. **P7.2 VRShootController** — 体感射门（PC 端用鼠标模拟先跑通逻辑）
3. **P7.3 ReplayDirector** — 演算导演（对接现有 ScenarioPlayer）
4. **P7.4 AudioManager** — BGM + 音效（准备音频素材）

**PC 端优先策略**：所有 P7 逻辑先在 PC 上用键盘/鼠标模拟跑通，Quest 手柄适配是最后一步。

**P8（APK 构建）** 继续阻塞：Android SDK 待网络安装。
**P9（性能优化）** 可穿插进行，不阻塞 P7。
**P10（演示视频）** P7 跑通后录屏即可。

---

## 未来展望（PPT 占位用）

下面这些列在演示视频结尾的"展望"页，当作加分项展示，但本期不做：

- **真智能足球**：3D 打印外壳 + UWB 定位传感器 + LAN 数据上报，替代虚拟球
- **纯视觉球检测**：USB 俯视摄像头 + OpenCV 颜色/ArUco → 不需要传感器
- **NT 双向通信**：Unity 把虚拟队友/对手位置回传机器人，机器人自动瞄准
- **真 AI 决策**：状态机 → 强化学习，替代预设剧本
- **4v4 完整阵型**：扩展到真实比赛规模
- **多人协作**：多台 Quest 3S + 多机器人同场训练
