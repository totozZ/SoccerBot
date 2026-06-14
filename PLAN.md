# SoccerBot 进度速查

> 更新时间：2026-06-14  
> 规则：每次功能变动或 bug 修复后，默认同步更新本文件、[docs/PROJECT_STATUS.md](docs/PROJECT_STATUS.md) 和对应详细设计文档；不需要用户额外提醒。

## 当前结论

- 主线原型已跑通：PC / Quest 基础流程、接球、反抢、射门、回放和评分已经形成闭环。
- 当前优先级：P0 同时推进“简易场上 AI 状态机”和“Quest 脚部 hitbox / 触球调校”。
- P1 预留给LLM AI；P2 才是画面优化；其他事项统一降到 P3。
- 所有功能/bug 总览见 [docs/PROJECT_STATUS.md](docs/PROJECT_STATUS.md)。
- 旧版长计划已归档到 [docs/PROJECT_PLAN_ARCHIVE_2026-06-14.md](docs/PROJECT_PLAN_ARCHIVE_2026-06-14.md)。

## 优先级队列

| 优先级 | 项目 | 状态 | 说明 |
|---|---|---|---|
| P0 | 简易场上 AI 状态机 | 完成一版，待复测调参 | `FieldAIController` 已接入队友接应、对手追球/低概率截传、守门员横移/概率扑救 |
| P0 | Quest 脚部 hitbox / 触球调校 | 进行中 | 调整脚部碰撞体、offset、触发距离和冲量，让“看见碰到球”稳定变成有效触球 |
| P1 | LLM AI Coach 训练闭环 | 已接入 MVP | 回合数据记录、TrainingSummaryJson、本地 HTTP 分析、ScorePanel AI 反馈和离线 fallback |
| P2 | 演示画面优化 | 进行中 | 黄昏天空、草坪材质、进球反馈、看台氛围 |
| P3 | 其他功能和长期方向 | 排队 | 性能专项、演示视频、智能足球、真实空间联动等 |

## 进度表

| 状态 | 编号 | 内容 | 详情 |
|---|---|---|---|
| 完成 | P1 | 重建 `Main.unity` 主场景 | 见 [项目状态总览](docs/PROJECT_STATUS.md) |
| 完成 | P2 | 蓝队队友 + 红队对手 | 见 [项目状态总览](docs/PROJECT_STATUS.md) |
| 完成 | P3-P5.1 | 剧本系统、剧本资产、评分 UI、球起点跟随机器人 | 见 [旧计划归档](docs/PROJECT_PLAN_ARCHIVE_2026-06-14.md) |
| 完成 | P6-P8 | XR Origin、PC 自由视角、演示流程、Quest 3S APK 跑通 | 见 [项目状态总览](docs/PROJECT_STATUS.md) |
| 完成一版 | Gameplay V1.2 | 接球提示、接球质量、Recovery 反抢、First Touch 结算说明 | 见 [核心玩法 V1.2](docs/CORE_GAMEPLAY_REWORK_V1_2_DEMO_FEEDBACK.md) |
| 完成一版 | P8.5 | 简易队友 / 对手 / 守门员状态机 AI | 见 [项目状态总览](docs/PROJECT_STATUS.md) |
| 已接入 MVP | P8.6 | LLM AI Coach 训练闭环 | 见 [项目状态总览](docs/PROJECT_STATUS.md) |
| 原型接入 | Quest 脚部交互 | 手柄驱动腿/脚、物理球交互、边界/球门判定 | 见 [Quest 手柄当腿脚设计](docs/QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md) |
| 进行中 | P9.5 | 黄昏天空、草坪材质、进球反馈、看台氛围 | 见 [演示画面优化](docs/DEMO_VISUAL_ART_OPTIMIZATION_PLAN.md) |
| 未完成 | P9 | Quest 性能专项：72fps、Draw Call、APK 体积 | 待 P9.5 和脚部交互稳定后集中做 |
| 未完成 | P10 | 2-3 分钟演示视频拍摄和剪辑 | 等画面与交互稳定后启动 |
| 暂缓 | P11 | BS-BT91 智能足球 BLE / IMU 训练模式 | 当前只保留方向和占位 |
| 未来 | P12 | 机器人 x VR 真实空间联动定位 | 当前无 Limelight/LL，不作为本期依赖 |

## 下一步

1. P0：Play Mode / Quest Link 复测 `FieldAIController` 的追球速度、截传概率、门将扑救概率和队友接应位置。
2. P0：用 Quest Link / APK 复测脚部触球，优先解决“可见脚碰到球但触发不稳定”和“传球后球偶发回中心/半陷地面”。
3. P1：联调本地 `POST http://localhost:8000/analyze` AI Coach 服务，确认 JSON 请求/响应格式。
4. P2：执行演示画面优化，先做黄昏天空、草坪材质、进球反馈。
5. P3：性能回归、演示视频、智能足球和真实空间联动排队处理。

## 文档入口

| 文档 | 用途 |
|---|---|
| [docs/PROJECT_STATUS.md](docs/PROJECT_STATUS.md) | 功能、未完成项、bug 的总入口 |
| [README.md](README.md) | 面向查看项目的人，说明项目是什么、怎么跑 |
| [docs/CORE_GAMEPLAY_REWORK.md](docs/CORE_GAMEPLAY_REWORK.md) | 核心玩法重构方向 |
| [docs/CORE_GAMEPLAY_REWORK_V1_2_DEMO_FEEDBACK.md](docs/CORE_GAMEPLAY_REWORK_V1_2_DEMO_FEEDBACK.md) | 接球提示、反抢和演示可读性 |
| [docs/DEMO_VISUAL_ART_OPTIMIZATION_PLAN.md](docs/DEMO_VISUAL_ART_OPTIMIZATION_PLAN.md) | 美术、灯光、材质、进球反馈 |
| [docs/QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md](docs/QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md) | Quest 手柄驱动腿/脚与物理触球 |
| [docs/PROJECT_PLAN_ARCHIVE_2026-06-14.md](docs/PROJECT_PLAN_ARCHIVE_2026-06-14.md) | 旧版长 `PLAN.md` 归档，保留历史细节 |
