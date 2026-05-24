# SoccerBot — VR 足球机器人 AI 推演沙盒

> **互联网+ 大学生创新创业大赛 · 萌芽赛道** 参赛项目
>
> 真机器人触发 + 虚拟世界推演 + Meta Quest 2 沉浸观看的 AR 训练沙盒。

---

## 项目定位

**痛点**：FRC 风格的足球机器人训练缺真实对手、缺安全场地、缺低成本练习方式。

**方案**：用一台真机器人作为"输入设备"，发射动作触发 Unity 内的虚拟球，虚拟队友 / 对手按预设战术剧本演绎传球走向，最后给一个评分。整个过程在 Meta Quest 2 里沉浸式观看。

**一句话**：硬件按一下，虚拟世界跑完一整场配合。

---

## 系统组成

| 部分 | 说明 |
|------|------|
| **真海绵宝宝机器人** | 旁边桌上能动就行，发射键触发 NT 信号 |
| **Unity 推演端** | 接收发射信号 → 生成虚拟球 → 选剧本 → 播放 → 评分 |
| **Meta Quest 2** | 部署同一份 Unity APK，沉浸式观看推演过程 |

---

## 数据流

```
真机器人发射键
      │
      ▼
NetworkTables: is_firing = true
      │
      ▼
┌──────────────────────────────────┐
│  Unity (PC + Quest 2 双端)         │
│                                  │
│  GameManager.OnShotFired         │
│      │                           │
│      ├─► 生成虚拟球（位置+速度）    │
│      │                           │
│      └─► ScenarioPlayer 选剧本    │
│              │                   │
│   ┌──────────┼──────────┐        │
│   ▼          ▼          ▼        │
│  成功传球    被拦截     射偏       │
│   100分     30分       50分      │
│      │       │         │         │
│      └───────┼─────────┘         │
│              ▼                   │
│        评分 UI + 慢动作回放         │
└──────────────────────────────────┘
```

---

## 核心功能

- **真机触发**：海绵宝宝机器人发射 → Unity 生成虚拟球
- **AI 剧本推演**：3 个预设剧本（成功传球 / 被拦截 / 射偏），按钮或随机选
- **虚拟队友 / 对手**：1v1 最小冲突阵型，复用海绵宝宝染色
- **评分系统**：剧本结束弹分数 + 慢动作回放
- **VR 沉浸观看**：Meta Quest 2 部署，全程第一人称
- **独立调试**：FakeDataGenerator 让 Unity 端无机器人也能跑

---

## 技术栈

| 层 | 技术 | 版本 |
|----|------|------|
| 机器人控制 | WPILib / C++ | 2026 |
| 通信 | NetworkTables v4（本期 FakeData 顶） | — |
| 3D 引擎 | Unity + URP | 6000.4.7f1 |
| VR | XR Interaction Toolkit + Oculus | 3.4.1 / 4.5.4 |
| 脚本 | C# | — |

---

## 快速开始

### 环境
- Unity 6000.4.7f1 + URP
- Meta Quest 2（VR 部署阶段）

### 独立运行（无机器人）
打开 `Assets/Scenes/Main.unity`（重建后）→ Play。
内置 [FakeDataGenerator.cs](unity/Assets/Scripts/Simulation/FakeDataGenerator.cs) 模拟真机器人发射，每 5 秒触发一次剧本。

### 连接真机器人（后期）
[NTManager.cs](unity/Assets/Scripts/Core/NTManager.cs) 中填入 RoboRIO IP，自动接管数据源。

---

## 项目目录

```
SoccerBot/
├── README.md
├── PLAN.md                                # 详细开发计划与进度
├── .gitignore
├── robot/                                 # 机器人端（本期可空）
└── unity/                                 # Unity 推演端（主战场）
    └── Assets/
        ├── Scenes/Main.unity              # ❌ 待重建
        └── Scripts/
            ├── Core/                      # ✅ 数据中枢、NT 骨架
            ├── Robot/                     # ✅ 海绵宝宝机器人
            ├── Ball/                      # ✅ 虚拟球 + 轨迹
            ├── Simulation/                # ✅ FakeData 调试源
            ├── UI/                        # ✅ 状态面板
            ├── Camera/                    # ✅ 跟随相机
            ├── Scenario/                  # ❌ 剧本系统（待写）
            └── XR/                        # ❌ VR 适配（待写）
```

---

## 开发进度

详见 [PLAN.md](PLAN.md)。

**当前状态（2026-05-24）**：v3.0 脚本资产完成，但 Unity 场景丢失，方向重定为 v4.0 比赛推演沙盒。下一步重建场景 + 写剧本系统。

---

## 未来展望

- 真智能足球（3D 打印外壳 + UWB 定位）
- 纯视觉球检测（OpenCV 俯视摄像头）
- NT 双向通信（Unity 反向控制机器人瞄准）
- 状态机 / 强化学习替代预设剧本
- 4v4 完整阵型 + 多人多机协作训练
