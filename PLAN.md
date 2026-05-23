# SoccerBot — Unity 可视化端开发计划

> 版本: v2.0 | 日期: 2026-05-23 | 状态: Unity 开发阶段

---

## 项目定位

**一句话**：AdvantageScope 的 3D VR 增强版。

已有的 AdvantageScope 通过 NetworkTables 在电脑屏幕上展示机器人 2D/3D 位姿。本项目在这个思路上更进一步：
- Unity 3D 场景接管机器人位姿渲染
- Meta Quest 2 提供沉浸式 VR 观察视角
- 根据炮台发射角度 + 速度反推足球抛物线轨迹并可视化

---

## 使用场景

| 角色 | 设备 | 操作 |
|------|------|------|
| **操作手** | 电脑 + 手柄 | 操控机器人移动 / 瞄准 / 发射 |
| **观察者** | Meta Quest 2 | 进入 VR 场景，自由观察机器人状态和足球轨迹 |

机器人通过 WPILib / C++ 控制，将 Odometry / 炮台状态通过 NetworkTables 实时发布。Unity 端订阅 NT 数据，驱动 3D 模型。

---

## 数据链路

```
┌─────────────────────────────────────────────┐
│  RoboRIO C++ (用户侧)                        │
│                                              │
│  robot/pose/x        (double)               │
│  robot/pose/y        (double)               │
│  robot/pose/rotation (double)               │
│  shooter/angle       (double)               │
│  shooter/speed       (double)               │
│  shooter/is_firing   (bool)                 │
│  ball/*  (if computed on robot side)        │
│                                              │
│  ── NetworkTables Server ──►                 │
└──────────────────────────────┬──────────────┘
                               │
            ┌──────────────────▼──────────────────┐
            │  Unity C# (本项目)                    │
            │                                      │
            │  NTManager.cs                        │
            │    ├─ 读取 robot/pose → 更新 Transform │
            │    ├─ 读取 shooter/* → 炮台动画       │
            │    └─ 检测 is_firing → 反推轨迹       │
            │                                      │
            │  轨迹反推公式:                        │
            │    x(t) = v0·cos(θ)·t                │
            │    y(t) = v0·sin(θ)·t - ½g·t²        │
            │    结合炮台朝向 + 仰角 + 轮速 → 3D 抛物线 │
            └──────────────────┬───────────────────┘
                               │
                    ┌──────────▼──────────┐
                    │   Meta Quest 2 VR   │
                    │   第一/第三人称观察   │
                    └─────────────────────┘
```

---

## 核心功能

| 功能 | 说明 | 状态 |
|------|------|------|
| NT 数据接收 | 订阅 `/SmartDashboard/robot/*` `/shooter/*` | 骨架就绪 |
| 机器人 3D 位姿 | 类似 AdvantageScope，用 Transform 渲染 Odometry | ✅ |
| 炮台动画 | 俯仰/旋转/飞轮旋转动画 | ✅ |
| 足球轨迹反推 | 角度+速度 → 抛物线 → LineRenderer | ✅ |
| 独立模拟运行 | FakeDataGenerator，无机器人也可调试 | ✅ |
| VR 观察 | Quest 2 部署，自由移动视角 | 待做 |
| 双人协作 | 操作手 PC + 观察者 VR 同时连接 NT | 待联调 |

---

## Unity 开发阶段

| Phase | 内容 | 状态 |
|-------|------|------|
| U1 | 项目骨架 + 脚本架构 | ✅ 完成 |
| U2 | FakeDataGenerator 独立运行 | ✅ 完成 |
| U3 | 轨迹反推渲染（抛物线可视化） | ✅ 完成 |
| U4 | NTManager 对接真机 NetworkTables | ⬜ 待用户机器人就绪 |
| U5 | Meta Quest 2 VR 部署 | ⬜ 待做 |
| U6 | 联调 & 性能优化 | ⬜ 待做 |

---

## 文件结构

```
SoccerBot/
├── README.md                         # 项目说明（已格式化）
├── PLAN.md                           # 本文件
├── .gitignore
├── robot/                            # C++ 机器人端（用户负责）
│   ├── build.gradle
│   ├── settings.gradle
│   ├── vendordeps/
│   │   └── Phoenix6-frc2026-latest.json
│   └── src/main/
│       ├── include/
│       │   ├── Constants.h           # CAN ID / PID / NT Key 常量
│       │   ├── Robot.h
│       │   ├── RobotContainer.h
│       │   ├── subsystems/
│       │   │   ├── DriveSubsystem.h
│       │   │   ├── ShooterSubsystem.h
│       │   │   └── VisionSubsystem.h
│       │   └── commands/
│       │       ├── DefaultDriveCommand.h
│       │       ├── ShootCommand.h
│       │       └── AutoAlignCommand.h
│       └── cpp/                      # 用户自行填充实现
│           ├── Robot.cpp
│           ├── RobotContainer.cpp
│           ├── subsystems/
│           └── commands/
└── unity/                            # Unity VR 可视化端（本项目）
    ├── Assets/
    │   └── Scripts/
    │       ├── Core/
    │       │   ├── RobotData.cs      # 数据模型
    │       │   ├── IDataSource.cs    # 数据源接口
    │       │   ├── GameManager.cs    # 单例中枢
    │       │   ├── NTManager.cs      # NetworkTables 客户端
    │       │   └── DataBuffer.cs     # 数据平滑
    │       ├── Robot/
    │       │   ├── RobotController.cs    # Transform 更新
    │       │   ├── RobotVisuals.cs       # 炮台动画
    │       │   └── RobotPathTrail.cs     # 运动拖尾
    │       ├── Ball/
    │       │   ├── BallController.cs     # 足球控制
    │       │   └── TrajectoryRenderer.cs # 轨迹线渲染
    │       ├── UI/
    │       │   └── StatusPanel.cs
    │       ├── Camera/
    │       │   ├── SmoothFollow.cs
    │       │   └── CameraSwitcher.cs
    │       └── Simulation/
    │           └── FakeDataGenerator.cs  # 独立运行模拟数据
    └── Packages/
        └── manifest.json             # Unity 6 URP + XR 依赖
```

---

## 轨迹反推算法

```
已知条件（从 NT 读取）：
  - robot_pose  (x₀, y₀, θ₀)    机器人当前位置和朝向
  - hood_angle  (α)              炮台仰角
  - flywheel_rpm (ω)             飞轮转速 → 初速度 v₀
  - firing (bool)                发射信号上升沿

计算：
  v₀ = RPM_to_velocity(ω)            // 轮速→线速度转换
  θ = θ₀ + hood_angle                // 实际发射仰角（世界坐标）

  抛物线参数方程 (t = 时间):
    x(t) = v₀ * cos(θ) * t
    y(t) = v₀ * sin(θ) * t - 0.5 * g * t²

  结合 heading 方向，转为 3D 向量。

Unity 渲染：
  TrajectoryRenderer 每帧计算 100 个轨迹点 → LineRenderer 绘制
```

---

## 下一步

1. 用户在 Unity Hub 创建 Unity 6000.3 URP 项目（路径：`SoccerBot/unity/`）
2. 选择 Platforms：Windows Build Support + Android Build Support
3. 用简单几何体搭建场景（Ground / Robot / Ball / Goal）
4. 挂载脚本 → Play → 观察 FakeDataGenerator 驱动的机器人
5. 后续接入真实 NetworkTables 联调
