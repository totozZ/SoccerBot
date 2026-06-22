# SoccerBot 进度速查

> 更新时间：2026-06-22
> 规则：每次功能变动或 bug 修复后，默认同步更新本文件、[docs/PROJECT_STATUS.md](docs/PROJECT_STATUS.md) 和对应详细设计文档；不需要用户额外提醒。

## 当前结论

- 主线原型已跑通：PC / Quest 基础流程、接球、反抢、射门、回放和评分已经形成闭环。
- 当前优先级：P0 主线切换为键鼠优先的 `Arena Attack`；现有 AI、角色动画和 Quest 脚部触球作为共用能力继续保留。
- Arena 默认是单门 90 秒计分赛：无越位/犯规/界外球，边墙回弹；PC 全场第三人称，VR 使用同规则的前场射手角色。
- 当前试玩已记录两项现象：左右键传射演示效果较怪、第三人称摄像机跟踪未做好；本版本先记录，等下一版补具体复现后再改传射。
- P1 LLM AI 已接 MVP；P1.5 免费资源小人的基础动态/跑步动画层已完成一版；P2 继续整体画面优化；其他事项统一降到 P3。
- 所有功能/bug 总览见 [docs/PROJECT_STATUS.md](docs/PROJECT_STATUS.md)。
- 旧版长计划已归档到 [docs/PROJECT_PLAN_ARCHIVE_2026-06-14.md](docs/PROJECT_PLAN_ARCHIVE_2026-06-14.md)。

## 优先级队列

| 优先级 | 项目 | 状态 | 说明 |
|---|---|---|---|
| P0-Arena | 键鼠优先 Arena Attack | 已实现首版，待 Play Mode 调参 | 新增独立 Arena 回合、第三人称键鼠/手柄输入、物理控球辅助、回弹墙、机器人发球、90 秒计分、轻量 AI、HUD/调试面板和 VR/XR Simulator 动作桥接；`F8` 切模式、`F7` 切控制档、`F9` 切调试面板 |
| P0 | 简易场上 AI 状态机 | 增强一版，待 Play Mode 调参 | `FieldAIController` 已接入队友接应、对手追球/截传、守门员横移/扑救；新增传向队友/射门威胁识别，读数继续影响结算 |
| P0.5 | Quest 脚部 hitbox / 触球调校 | 进行中，已加补触发 / 诊断增强 | 调整脚部碰撞体、offset、触发距离和冲量；`TrackedLegController` 已加入最近点 proximity probe，并补了 rig 绑定与 runtime 材质复用兜底 |
| P1 | LLM AI Coach 训练闭环 | 已接入 MVP，接口文档已补 | 回合数据记录、TrainingSummaryJson、本地 HTTP 分析、ScorePanel AI 反馈和离线 fallback；请求/响应合约见 LLM 文档 |
| P1.5 | 免费资源小人基础动态 / 跑步动画层 | 完成一版，待 Play Mode / Quest 复测 | 已复用 LowPolyPeople 的 idle/walk/run/wave；按实际移动速度切换动画，门将 Saving 叠加程序化侧扑，无 Animator 时自动使用低成本动态兜底 |
| P2 | 演示画面优化 | 进行中 | 黄昏天空、草坪材质、进球反馈、看台氛围 |
| P3 | 其他功能和长期方向 | 排队 | 性能专项、演示视频、智能足球、真实空间联动等 |

## 进度表

| 状态 | 编号 | 内容 | 详情 |
|---|---|---|---|
| 完成首版 | Arena M0-M5 | Training/Arena 模式隔离、键鼠/手柄动作、物理回弹、90 秒单门比赛、AI/HUD、VR 脚触球桥接 | 默认进入 Arena；命令行支持 `-training`、`-arena`、`-gamepad`、`-vrStriker`、`-xrSimulator` |
| 完成 | P1 | 重建 `Main.unity` 主场景 | 见 [项目状态总览](docs/PROJECT_STATUS.md) |
| 完成 | P2 | 蓝队队友 + 红队对手 | 见 [项目状态总览](docs/PROJECT_STATUS.md) |
| 完成 | P3-P5.1 | 剧本系统、剧本资产、评分 UI、球起点跟随机器人 | 见 [旧计划归档](docs/PROJECT_PLAN_ARCHIVE_2026-06-14.md) |
| 完成 | P6-P8 | XR Origin、PC 自由视角、演示流程、Quest 3S APK 跑通 | 见 [项目状态总览](docs/PROJECT_STATUS.md) |
| 完成一版 | Gameplay V1.2 | 接球提示、接球质量、Recovery 反抢、First Touch 结算说明 | 见 [核心玩法 V1.2](docs/CORE_GAMEPLAY_REWORK_V1_2_DEMO_FEEDBACK.md) |
| 增强一版 | P8.5 | 简易队友 / 对手 / 守门员状态机 AI | 见 [项目状态总览](docs/PROJECT_STATUS.md) |
| 已接入 MVP / 文档已补 | P8.6 | LLM AI Coach 训练闭环 | 见 [LLM AI Coach 集成文档](docs/LLM_AI_COACH_INTEGRATION.md) |
| 完成一版 | P1.5 | 免费资源小人基础动态 / 跑步动画层 | 见 [演示画面优化](docs/DEMO_VISUAL_ART_OPTIMIZATION_PLAN.md) |
| 进行中 | Quest 脚部交互 | 手柄驱动腿/脚、物理球交互、proximity 补触发、tracking overlay、F2 调参窗、边界/球门判定 | 见 [Quest 手柄当腿脚设计](docs/QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md) |
| 进行中 | P9.5 | 黄昏天空、草坪材质、进球反馈、看台氛围 | 见 [演示画面优化](docs/DEMO_VISUAL_ART_OPTIMIZATION_PLAN.md) |
| 未完成 | P9 | Quest 性能专项：72fps、Draw Call、APK 体积 | 待 P9.5 和脚部交互稳定后集中做 |
| 未完成 | P10 | 2-3 分钟演示视频拍摄和剪辑 | 等画面与交互稳定后启动 |
| 暂缓 | P11 | BS-BT91 智能足球 BLE / IMU 训练模式 | 当前只保留方向和占位 |
| 未来 | P12 | 机器人 x VR 真实空间联动定位 | 当前无 Limelight/LL，不作为本期依赖 |

## 下一步

1. P0-Arena：下一版先把比赛镜头改为固定在对方球门上方、朝球场中心/对面半场的静态透视镜头，并把传射瞄准从摄像机 forward 中拆出。
2. P0-Arena：按 [Arena 操作说明](docs/ARENA_ATTACK_OPERATION_GUIDE.md) 的模板补充左右键异常的具体预期/实际效果，再调整传射方向、力度、辅助或键位，不先盲调。
3. P0-Arena：Play Mode 连续跑至少 10 次发球，验证 `WASD / Shift / E / RMB / LMB / Space`、墙面回弹、进球、被断、门将控制、卡球重置和 90 秒净计时。
4. P0-Arena：调物理控球弹簧、墙面 70% 回弹、后卫压迫速度和门将扑救范围；重点确认球不会瞬移回中心或半陷地面。
5. P0-Arena：用 `F8` 回归旧 Training 流程；用 `F7` 验证 Gamepad 与 XR Simulator 都进入统一动作结果层。
6. P0：无 VR 期间先保留 Quest 接口级测试；拿到 Quest 后再验证双脚实体触球、延迟、舒适度和前场射手区尺度。

## Arena 操作与规则

- 键鼠：`WASD` 移动、鼠标视角、`Left Shift` 冲刺、`E` 停球、`RMB` 传球、`LMB` 射门、`Space` 抢断、`Esc` 暂停。
- 手柄：左/右摇杆移动与视角、`LT` 冲刺、`A` 停球、`X` 传球、`RT` 射门、`B` 抢断。
- 模式控制：`F8` 在 Training/Arena 间重载切换；`F7` 轮换 KeyboardMouse/Gamepad/VrStriker/XrSimulator；`F9` 显示或隐藏调试面板。
- Arena 中只有进球、对方稳定控球、球掉落/逃逸或卡死会重置；撞墙与门将单次扑出不会中断比赛。

## 文档入口

| 文档 | 用途 |
|---|---|
| [docs/ARENA_ATTACK_OPERATION_GUIDE.md](docs/ARENA_ATTACK_OPERATION_GUIDE.md) | Arena 启动方式、键鼠/手柄/VR 操作、当前已知问题、静态镜头计划和反馈模板 |
| [docs/PROJECT_STATUS.md](docs/PROJECT_STATUS.md) | 功能、未完成项、bug 的总入口 |
| [README.md](README.md) | 面向查看项目的人，说明项目是什么、怎么跑 |
| [docs/CORE_GAMEPLAY_REWORK.md](docs/CORE_GAMEPLAY_REWORK.md) | 核心玩法重构方向 |
| [docs/CORE_GAMEPLAY_REWORK_V1_2_DEMO_FEEDBACK.md](docs/CORE_GAMEPLAY_REWORK_V1_2_DEMO_FEEDBACK.md) | 接球提示、反抢和演示可读性 |
| [docs/LLM_AI_COACH_INTEGRATION.md](docs/LLM_AI_COACH_INTEGRATION.md) | LLM AI Coach 请求/响应合约、本地服务和联调清单 |
| [docs/DEMO_VISUAL_ART_OPTIMIZATION_PLAN.md](docs/DEMO_VISUAL_ART_OPTIMIZATION_PLAN.md) | 美术、灯光、材质、角色动态、进球反馈 |
| [docs/QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md](docs/QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md) | Quest 手柄驱动腿/脚与物理触球 |
| [docs/PROJECT_PLAN_ARCHIVE_2026-06-14.md](docs/PROJECT_PLAN_ARCHIVE_2026-06-14.md) | 旧版长 `PLAN.md` 归档，保留历史细节 |
