# SoccerBot 核心玩法重构设计

日期：2026-06-06
范围：P8/P11 基础之后的第一版可玩竖切

## 结论

SoccerBot 不再把预设剧本当作主要玩法。

新的核心体验是：

真实/模拟机器人发球 -> VR 接球节奏判定 -> 短暂持球决策 -> 队友/对手反应 -> 回放与评分。

原有剧本系统保留，但职责从“替玩家演完整场”改为“根据玩家动作结果做演出和评分”。

## 项目基线

- Unity：6000.4.7f1
- 渲染管线：URP 17.4.0
- XR 栈：XR Interaction Toolkit 3.4.1、XR Management 4.5.4、Oculus XR 4.5.4
- 代码组织：单运行时程序集，`Assets/Scripts` 下按功能目录分组
- 当前玩法主干：`MatchFlowController` 负责比赛循环，`FPSPlayerController` 负责输入，`ScenarioPlayer` 播放结果路径，`ScenarioTrigger` 路由剧本资源
- 当前数据模式：`Scenario` ScriptableObject 保存结果演出；输入、蓄力、射门和剧本完成用事件衔接
- 当前 SmartBall 方向：已有 `SmartBallController`、Mock 数据源、BLE 占位和训练模式

## 产品原则

1. 真实触发要有意义。
   机器人或 FakeData 不只是按钮，而是“来球”的发起者。

2. 接球是第一层技术。
   第一件好玩的事不是看动画，而是在球飞来时完成正确时间、正确朝向的一脚停球。

3. AI 压迫带来重复可玩性。
   接球差，对手立刻抢；接球好，队友跑位和射门窗口更舒服。

4. 回放是奖励，不是输入。
   现有剧本继续负责慢动作、评分、精彩结果和答辩展示。

## 核心循环

1. 准备
   玩家站在进攻位置，队友、对手、守门员、HUD 和机器人可见。

2. 发球
   真实机器人或 FakeData 触发来球。

3. 接球窗口
   玩家在传球飞行过程中按下接球输入。系统计算：
   - 时间：按下时机离理想触球点有多近
   - 朝向：玩家头部/手柄方向是否对准来球

4. 持球
   接球质量足够时，玩家获得短暂持球，可以瞄准、蓄力、射门或后续扩展传球。

5. 压迫
   接球质量过低时，对手直接抢断，或显著降低后续射门成功率。

6. 结算
   射门蓄力、瞄准方向和接球质量共同决定结果分支。

7. 回放与评分
   现有 `ScenarioPlayer` 和 `ScorePanel` 展示结果。

## 输入契约

VR 手柄是手，不是脚。

手柄适合承担：
- 接球节奏输入
- 射门/传球蓄力
- 瞄准方向
- 后续假动作选择

不要依赖 Quest 3S 下半身追踪做精确脚触球。Meta 的身体追踪和生成腿更适合角色表现，不适合毫秒级、厘米级的足球碰撞判定。真实足球输入应交给机器人、SmartBall IMU、BLE 或未来外部追踪系统。

第一版输入绑定：
- PC 接球：Space 或鼠标左键
- Quest 接球：右 grip、左 grip 或接球阶段的右 trigger
- PC/Quest 射门：保留现有蓄力释放逻辑

## 第一版竖切

这一版只在现有射门路由之前增加一个真实交互：

- `FPSPlayerController`
  增加 `ReceptionEnabled` 和 `OnReceiveAttempt`。

- `MatchFlowController`
  在 `Pass` 阶段启用接球输入并记录传球进度。
  根据时间和朝向计算接球质量。
  低质量接球进入被抢断。
  高质量接球给后续射门分支加成。

- UI
  第一版暂不新增 UI 预制体。先用现有音效、节奏和评分验证手感。下一版再加世界空间接球圈或节奏提示。

## 失败反抢阶段

接球失败后不立刻判死刑，而是进入一次短暂的 Recovery 反抢窗口。

玩家目标：
- 疯狂按刚才用于接球的输入键
- 在限制时间内达到指定按键次数
- 用按键节奏把对手逼退，重新夺回球权

反馈规则：
- 每次按下，对手会被向后顶一下
- 松开后，对手会小幅回压
- 连续按键会形成总体后退趋势
- 画面有前后震动、红色危险边框和顶部动态按键提示

成功结果：
- 对手被明显击退
- 球回到玩家脚下
- 玩家进入持球阶段，获得一次挽回后的射门机会

失败结果：
- 对手前压把球带走
- 触发现有 Intercepted 结果回放
- 失败不是黑屏结束，而是用抢断演出让玩家知道自己差一点救回来

## 架构

项目层级：小型游戏原型。

推荐模块：
- Flow：拥有比赛阶段和临时玩法状态。
- Player：拥有 VR/PC 输入并发出意图事件。
- Scenario：拥有演出结果资源。
- SmartBall：拥有真实球/IMU 训练数据，暂时和比赛流程分离。
- UI：展示蓄力、评分，后续展示接球节奏。
- Robot/Core：拥有真实或模拟机器人发球事件。

场景启动：
- 继续让 `MatchFlowController` 做当前组合入口。
- 暂不引入新的全局服务层。
- 自动查找引用只放在启动/准备阶段，避免热路径里反复 `Find`。

数据归属：
- 接球窗口参数放在 `MatchFlowController` 的序列化字段上。
- 结果演出继续用 `Scenario` ScriptableObject。
- 接球质量是单局运行时状态。

通信规则：
- 玩家输入使用事件。
- MatchFlow 直接命令 ScenarioPlayer。
- UI 在需要时订阅 Player 或 Flow 事件。

性能风险：
- 第一版不做 Quest 摄像头脚部识别。
- 不做每帧对象查找。
- 加机器视觉/ML 之前，Quest 目标仍然是稳定 72 FPS。

## 实施顺序

1. 给 `FPSPlayerController` 加接球输入事件。
2. 给 `MatchFlowController` 加接球时间窗口。
3. 用接球质量影响现有结果路由。
4. 需要时再加轻量状态提示。
5. 先 PC Editor 用键鼠调手感，再上 Quest 用 trigger/grip 测。

## P8.5 简易场上 AI 状态机实现记录（2026-06-14）

本轮新增 `Assets/Scripts/Flow/FieldAIController.cs`，作为 `MatchFlowController` 启动时自动挂载的轻量状态机，不引入 NavMesh、行为树或全局服务。

- 队友：持球阶段移动到玩家前方侧翼接应点；球离开玩家后转入接球状态，向传球落点/球速方向补位。
- 对手：Pass 阶段压向传球线路；Possession 阶段缓慢追球；在接近传球线路或散球时按低概率触发 `TryResolveAiInterception(...)`。
- 守门员：根据球的横向位置在门线附近移动；Shot 阶段根据球速方向、距离和概率触发 `TryResolveGoalkeeperSave(...)`，显示 `SAVED` 结算。
- Flow 接入：`MatchFlowController` 增加 AI 截断与门将扑救两个窄入口，并在 Pass 被 AI 截断时跳过后续 Possession，直接进入 Shot/Score/Cooldown 结算链。
- 状态读数：对手计算 `PassPressure01`，队友计算 `TeammateSupport01`，守门员计算 `GoalkeeperCoverage01`；这些读数会影响传球到队友后的被断概率、进球概率和门将扑救概率。
- 验证：Unity 6000.4.7f1 打开的 Editor 已完成脚本重编译且无 `error CS`；batchmode 因当前项目已有 Editor 进程占用返回失败码，未发现本次代码编译错误。当前项目测试文件仍在预定义程序集下，命令行 Test Runner 需要后续 asmdef/runtime assembly 迁移后才能真正发现并执行 EditMode 测试。

待复测：

- PC Play Mode 观察对手是否太容易截断机器人来球，以及 `PassPressure01` 高时是否真的来自对手贴近传球线/接应点。
- Quest Link 观察队友跑位和门将横移是否抢视线、是否影响脚部触球。
- 根据实测调 `_incomingPassInterceptChance`、`_loosePassInterceptChancePerSecond`、`_goalkeeperSaveChance`、移动速度和距离半径。

## 暂时不做

- 完整 AI 行为树
- 强化学习
- Quest 摄像头脚部识别
- Meta Movement SDK 角色重定向
- BLE SmartBall 真实协议解析
- 大型 UI 节奏小游戏
- Limelight/LL + AprilTag 机器人定位
- 机器人与 VR 的真实空间联动追踪

第一版只回答一个问题：

玩家会不会感觉“接住机器人传来的球”是一件可以练、可以变强的事？
