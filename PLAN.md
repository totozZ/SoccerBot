# SoccerBot — VR 足球机器人 AI 推演沙盒

## TODO（速查）

- [x] **P1** 重建 Main.unity 场景（GameManager / Robot / Ball / Camera / Ground / Canvas）
- [x] **P2** 加 1 队友（蓝队）+ 1 对手（红队）GameObject，复用 CharacterBuilder 改色
- [x] **P3** 剧本系统代码：Scenario / ScenarioPlayer / ScenarioTrigger / ScenarioFactory
- [x] **P4** 生成 3 个剧本 .asset，挂到 ScenarioTrigger，监听 OnShotFired
- [x] **P5** ScorePanel 评分 UI + 慢动作回放（Time.timeScale）
- [ ] **P5.1** 球的发射起点跟随机器人当前位置（目前固定在场地原点，机器人巡逻时球会"飘"出来）
- [ ] **P6** XR Origin 替换 Main Camera，PC 端先跑通 VR 视角
- [ ] **P7** Quest 2 APK 构建 + 控制器输入（Trigger 选剧本）
- [ ] **P8** 性能优化（Quest 2 帧率）
- [ ] **P9** 演示视频拍摄 + 剪辑

---

> 版本: v4.1 | 日期: 2026-05-24 | 状态: PC 端 MVP 跑通，待优化 + VR 适配
> 参赛: **互联网+ 大学生创新创业大赛 · 萌芽赛道**

---

## 一句话项目定位

真机器人发射 → Unity 接管为虚拟球 → AI 队友/对手按预设剧本推演传球走向 → 评分 → 全程 Meta Quest 2 沉浸观看。

不是 AdvantageScope 增强版（v3.0 的旧定位已废弃）。是 **VR AR 训练沙盒**：用真硬件触发，用虚拟世界推演。

---

## 演示故事板（3 分钟视频）

| 时间 | 镜头 | 解说 |
|------|------|------|
| 0:00–0:15 | 真机器人 + 戴 Quest 2 的人 | 痛点：足球机器人训练缺对手、缺场地、缺安全空间 |
| 0:15–0:45 | 真机器人发射 → Unity/VR 同步出现虚拟球飞出 | 虚实结合：真硬件触发 + 虚拟推演 |
| 0:45–1:45 | 虚拟队友接球 → 三个剧本之一播放（射门成功 / 被拦截 / 射偏） | AI 推演比赛走向 |
| 1:45–2:15 | 评分浮窗 + 慢动作回放 | 量化训练结果 |
| 2:15–2:45 | PPT 渲染：4v4 完整阵型、真球传感器、UWB 定位 | 未来展望 |

---

## 当前进度总览（v4.0）

| 模块 | 状态 | 说明 |
|------|------|------|
| Unity 项目骨架 | ✅ 完成 | Unity 6000.4.7f1 + URP 17.4 + XR 包就绪 |
| 数据模型 + 接口 | ✅ 完成 | RobotData / IDataSource 等 |
| GameManager 中枢 | ✅ 完成 | 单例 + 事件系统，OnShotFired 事件可复用 |
| FakeDataGenerator | ✅ 完成 | 无机器人时独立运行调试 |
| 机器人 3D 渲染 | ✅ 完成 | 海绵宝宝程序化模型 + 炮台动画 |
| 球轨迹反推 | ✅ 完成 | 抛物线计算 + LineRenderer |
| 相机 / 拖尾 / UI | ✅ 完成 | 复用，无改动 |
| **Main.unity 场景** | ✅ 重建完成 | GameManager / Robot / Teammate / Opponent / Ball / ScenarioPlayer / ScenarioTrigger / Camera / Ground / Canvas |
| **虚拟队友 / 对手** | ✅ 完成 | CharacterBuilder 改色，蓝/红队 |
| **剧本系统** | ✅ 完成 | Scenario SO + Player + Trigger + Editor 工厂菜单 |
| **3 个剧本资产** | ✅ 完成 | ScoreSuccess / Intercepted / ShotMissed |
| **评分 UI** | ✅ 完成 | ScorePanel 淡入淡出 + 按 outcome 着色 |
| **球发射起点跟随机器人** | ⚠️ 已知问题 | 关键帧固定在场地原点，机器人巡逻时球会"飘"出来；待 ScenarioPlayer 加平移偏移 |
| **Quest 2 部署** | ⬜ 待做 | XR Origin + APK Build + 控制器输入 |
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
| **Quest 2** | **必做**（项目题目就是 VR 方向）。XR Origin 替换主相机，APK 部署 |
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
   │ Meta Quest 2│  ← 同一份 Unity build 部署 APK
   │ 沉浸式观看   │
   └─────────────┘
```

---

## 开发阶段

| Phase | 内容 | 工期估计 |
|-------|------|---------|
| **P1** | 重建 [Main.unity](unity/Assets/Scenes/Main.unity)，按上次给的 7 步在 Editor 里点出来 | 0.5 天 |
| **P2** | 加 1 队友 + 1 对手 GameObject（CharacterBuilder 改色版） | 0.5 天 |
| **P3** | **剧本系统核心**：`ScenarioPlayer.cs` + `Scenario` ScriptableObject + 3 个剧本资产 | 2 天 |
| **P4** | 虚拟球生成 + 监听 OnShotFired → 触发剧本播放 | 0.5 天 |
| **P5** | 评分 UI + 慢动作回放（Time.timeScale = 0.3） | 1 天 |
| **P6** | XR Origin 替换 Main Camera，PC 端先跑通 VR 视角 | 1 天 |
| **P7** | Quest 2 APK 构建 + 控制器输入（按 Trigger 选剧本） | 1 天 |
| **P8** | 性能优化（海绵宝宝 primitive 多，Quest 2 容易掉帧） | 1–2 天 |
| **P9** | 演示视频拍摄 + 剪辑 | 1 天 |
| **总计** | | **~9–10 天** |

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

## 文件结构（v4.0）

```
SoccerBot/
├── README.md                              # 项目说明（v4.0）
├── PLAN.md                                # 本文件
├── .gitignore
├── robot/                                 # ⬇️ 优先级降低，本期可空
└── unity/
    ├── Assets/
    │   ├── Scenes/
    │   │   └── Main.unity                 # ❌ 待重建
    │   └── Scripts/
    │       ├── Core/                      # ✅ 已完成（保持不动）
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
    │       ├── UI/
    │       │   ├── StatusPanel.cs         # ✅
    │       │   └── ScorePanel.cs          # ❌ 待写：评分 UI
    │       ├── Camera/                    # ✅ 已完成
    │       ├── Simulation/
    │       │   └── FakeDataGenerator.cs   # ✅
    │       ├── Scenario/                  # ❌ 全部待写
    │       │   ├── Scenario.cs            # ScriptableObject 定义
    │       │   ├── ScenarioPlayer.cs      # 关键帧插值播放器
    │       │   └── ScenarioTrigger.cs     # 监听 OnShotFired 触发
    │       └── XR/                        # ❌ 待写
    │           └── XRSetup.cs             # XR Origin 配置
    ├── Assets/Scenarios/                  # ❌ 待建
    │   ├── ScoreSuccess.asset
    │   ├── Intercepted.asset
    │   └── ShotMissed.asset
    └── Packages/manifest.json
```

---

## 立即下一步

**P1–P5 已完成**：场景重建好，剧本系统跑通，评分弹窗正常。

**P5.1 已知问题**：球的发射起点固定在 `(0, 0.5, -1.5)`（[ScenarioFactory.cs](unity/Assets/Scripts/Scenario/Editor/ScenarioFactory.cs) 写死的初始关键帧），但 FakeDataGenerator 让机器人 8 字巡逻，所以球经常不从机器人身上飞出。
**修法**：让 [ScenarioPlayer.cs](unity/Assets/Scripts/Scenario/ScenarioPlayer.cs) 在 `Play()` 时记录机器人当前 pose，把整套关键帧平移/旋转到以机器人为原点。改一个方法即可。

**P6 接下来**：XR Origin 替换 Main Camera，PC 端先跑通 VR 视角，再上 Quest 2。

---

## 未来展望（PPT 占位用）

下面这些列在演示视频结尾的"展望"页，当作加分项展示，但本期不做：

- **真智能足球**：3D 打印外壳 + UWB 定位传感器 + LAN 数据上报，替代虚拟球
- **纯视觉球检测**：USB 俯视摄像头 + OpenCV 颜色/ArUco → 不需要传感器
- **NT 双向通信**：Unity 把虚拟队友/对手位置回传机器人，机器人自动瞄准
- **真 AI 决策**：状态机 → 强化学习，替代预设剧本
- **4v4 完整阵型**：扩展到真实比赛规模
- **多人协作**：多台 Quest 2 + 多机器人同场训练
