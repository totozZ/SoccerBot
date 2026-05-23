# SoccerBot — FRC 足球机器人 VR 可视化系统

> 类似 AdvantageScope 的 3D/VR 增强版，通过 NetworkTables 实时渲染机器人位姿与足球轨迹。

---

## 项目简介

| 部分 | 说明 | 负责 |
|------|------|------|
| **机器人端** | FRC 底盘 + 炮台，WPILib / C++，RoboRIO 2.0 | 用户 |
| **可视化端** | Unity 3D + Meta Quest 2，接收 NT 数据实时渲染 | 本项目 |

**协作模式**：一人电脑操控机器人，一人佩戴 Quest 2 进入 VR 场景观察机器人位姿、炮台朝向、足球发射轨迹。

---

## 机器人结构

```
        ┌──────────────┐
        │    炮台       │
        │ • 俯仰 (Hood) │
        │ • 旋转 (Yaw)  │
        │ • 加旋 (Flywheel) │
        │ • 发射 (Fire) │
        └──────┬───────┘
               │
   ┌───────────┴───────────┐
   │     FRC 底盘          │
   │  • Mecanum / Diff    │
   │  • Falcon 500 × 4    │
   │  • Odometry          │
   └───────────────────────┘
```

---

## 数据流

```
[RoboRIO C++] ──NetworkTables Server──► [Unity C#] ──► [Quest 2 VR]
      │                                       │
  发布数据:                              接收后渲染:
  • robot/pose/x,y,θ          →       机器人 3D 模型位姿
  • shooter/angle,speed       →       炮台俯仰 + 飞轮旋转
  • ball/*                    →       足球轨迹抛物线
```

**核心逻辑**：
1. 机器人通过 NT 发布 Pose 数据
2. Unity 订阅后更新 3D 模型 Transform（类似 AdvantageScope 3D 视图）
3. 发射时根据角度 + 速度反推抛物线轨迹并渲染

---

## 功能

- NetworkTables v4 实时通信
- 机器人 Pose 3D 渲染（AdvantageScope 风格）
- 炮台俯仰 / 旋转 / 发射动画
- 足球轨迹反推与可视化（从角度+速度→抛物线）
- Meta Quest 2 VR 观察
- 内置模拟数据（无机器人时可独立运行）

---

## 技术栈

| 层 | 技术 |
|----|------|
| 机器人控制 | WPILib 2026 / C++ |
| 电机 | CTRE Phoenix 6 (Falcon 500) |
| 视觉 | Limelight 3A |
| 通信 | NetworkTables v4 |
| 3D 引擎 | Unity 6000 LTS + URP |
| VR | XR Interaction Toolkit + Quest 2 |
| 脚本 | C# |

---

## 快速开始

### 环境
- Unity 6000.3 LTS + URP
- Meta Quest 2（VR 阶段）
- WPILib 2026 + JDK 17（机器人端）

### 独立运行（无机器人）
Unity 内置 `FakeDataGenerator`：
- 模拟机器人在场地巡逻
- 每 5 秒自动发射足球
- 完整轨迹可视化

### 连接真实机器人
`NTManager.cs` 中填入 RoboRIO IP，Unity 自动连接 NT Server 同步数据。

---

## 项目目录

```
SoccerBot/
├── README.md
├── PLAN.md
├── .gitignore
├── robot/                # 机器人端 C++（用户负责）
│   └── src/main/
│       └── include/subsystems/
└── unity/                # Unity VR 可视化端（本项目）
    └── Assets/Scripts/
        ├── Core/         # GameManager, NTManager, DataBuffer
        ├── Robot/        # RobotController, RobotVisuals
        ├── Ball/         # BallController, TrajectoryRenderer
        ├── Simulation/   # FakeDataGenerator
        ├── UI/           # StatusPanel
        └── Camera/       # SmoothFollow, CameraSwitcher
```
