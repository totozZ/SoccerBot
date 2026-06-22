# SoccerBot 演示画面与美术优化确认文档

日期：2026-06-22
状态：执行中；P1.5 角色基础动态已完成一版
范围：记录画面、美术、材质、灯光、角色基础动态、后处理、进球反馈与演示可读性的计划和落地状态。

---

## 1. 结论速览

### 1.1 演示强化计划完成度

当前不能视为“全部完成”。

| 计划 | 当前状态 | 判断 |
|------|----------|------|
| `PLAN.md` P1-P8 | 已勾选 | 核心场景、流程、VR 部署、基础视觉链路已完成 |
| `docs/CORE_GAMEPLAY_REWORK_V1_2_DEMO_FEEDBACK.md` | 大部分已落地 | 接球提示、接球圈、即时反馈、射门加成提示已经进入代码；评分面板解释仍不完整 |
| `PLAN.md` P1.5 免费资源小人基础动态 | 完成一版，待 Play Mode / Quest 复测 | LowPolyPeople 自带 idle/walk/run/wave；已接实际速度驱动、门将程序化侧扑和无 Animator 动态兜底 |
| `PLAN.md` P9.5 视觉演示打磨 | 未完成 | 已有灯光、观众、后处理、粒子骨架，但画面观感仍未达标 |
| P9 性能优化 | 未完成 | 仍待 Quest 72fps / Draw Call / APK 体积专项验证 |
| P10 演示视频 | 未完成 | 需等画面满意后再拍摄 |

### 1.2 当前最影响观感的问题

1. 缺少真正可见的黄昏天空与天际层次，画面容易像“黑背景 + 暖灯”。
2. 场地草坪材质资产存在，但 `GrassMat_Upgraded.mat` 看起来没有正确写入 albedo 主贴图，实际画面可能仍偏素。
3. 进球反馈剂量偏轻：纸屑 25、半径 4m、闪灯 boost 0.6、闪灯 2 次，演示高潮不够强。
4. `CrowdAnimator._crowdCheerClips` 在场景里为空，虽然 `AudioManager` 有 goal/miss/intercept SFX，但观众独立欢呼链路未填。
5. AI 小人已有位置移动和状态切换，但缺少基础待机/跑步/扑救动态，容易像“滑动棋子”。
6. `WeatherController` 代码存在，但 `Main.unity` 未检索到挂载；且只有 Sunny/Rain，没有 P9.5 文档要求的 Dusk/Dry Haze。
7. 场景里 `LightingConfigurator` 序列化值仍是旧参数：`_lightYAngle = -20`、`_shadowStrength = 0.65`，脚本默认已更新为 `-8 / 0.45`，需要统一。
8. 后处理已有 Bloom/Vignette/ACES，但当前场景参数偏保守：Bloom 0.5、threshold 1.1、postExposure 0。

### 1.3 推荐策略

先做“第一眼变好”的低风险改造，再做资产级材质升级。

优先顺序：

1. 黄昏天空 + 雾气 + 灯光参数统一
2. 草坪材质修复 + 球门/球/灯牌材质增强
3. 进球绝杀反馈加量：纸屑、闪灯、欢呼、镜头停留
4. P1.5 免费资源小人基础动态：待机、跑步、守门员侧扑、进球/失误反应
5. 看台国家配色与观众动作增强
6. Quest 性能回归验证

---

## 2. 已完成内容核对

### 2.1 V1.2 接球演示反馈

已完成或基本完成：

| 需求 | 证据 | 状态 |
|------|------|------|
| Pass 阶段提示玩家准备接球 | `ReceptionPromptPresenter.ShowPassProgress()` | 已完成 |
| PC / Quest 文案分流 | `Space / Left Click` 与 `Grip or Trigger` | 已完成 |
| 接球窗口状态变化 | READY / CATCH NOW / MISSED WINDOW | 已完成 |
| 接球圈 | `ReceptionTargetIndicator` 使用 `LineRenderer` 画圆环 | 已完成 |
| 接球质量即时反馈 | `ShowReceiveFeedback()` | 已完成 |
| Recovery 反抢提示 | `ShowRecovery()` + `RecoveryMashHUD` | 已完成 |
| 接球质量影响射门 | `receiveBias = Lerp(-penalty, bonus, quality)` | 已完成 |
| 射门时提示 First Touch 影响 | `ShowShotBias()` | 已完成 |

未完全完成：

| 需求 | 当前缺口 | 建议 |
|------|----------|------|
| 评分/回放面板解释接球影响 | `ScorePanel.Show(Scenario)` 仍只接收 Scenario，没接收本轮接球质量 | 增加一行“First Touch +18% / Pressured Shot -12%”或中文解释 |
| 独立 `ReceptionFeedbackPresenter` | 没有单独类，功能合并在 `ReceptionPromptPresenter` | 可接受，不强制拆分 |
| Quest 真机提示可读性复测 | 代码是 WorldSpace，但未见最新复测记录 | 执行后需要 Quest 3S 检查 |

结论：V1.2 “玩家知道要接球”这条主目标基本完成；“结算解释接球影响”还差一小段 UI 收口。

---

## 3. 当前美术技术基线

### 3.1 Unity 与渲染管线

- Unity：6000.4.7f1
- 渲染管线：URP 17.4.0
- XR：XR Interaction Toolkit 3.4.1、XR Management、Oculus XR
- 目标设备：Meta Quest 3S

### 3.2 场景中已挂载的视觉链路

| 模块 | 当前情况 |
|------|----------|
| `FieldBuilder` | 运行时生成草坪、线、球门 |
| `StadiumBuilder` | 运行时生成看台、座席、屋顶、灯塔、六芒星顶灯 |
| `CrowdAnimator` | 已挂在 Stadium，运行时生成低模观众、进球纸屑、闪灯、欢呼入口 |
| `Global Volume` | 已挂 `PolishVolumeBuilder` |
| `LightingConfigurator` | 已挂在 Global Volume 同物体 |
| `OutcomeFx` | 已在场景中 |
| `AudioManager` | BGM 与 goal/miss/intercept SFX 已填槽 |

### 3.3 当前风险点

| 风险 | 说明 |
|------|------|
| 运行时生成对象多 | Field/Stadium/Crowd 多为运行时生成，手改子物体不持久，应改 Builder 或序列化字段 |
| 材质管线混用 | 部分导入材质仍是内置 Standard 序列化，需要转 URP/Lit 或 URP/Simple Lit |
| Quest 性能余量未知 | 增加灯、粒子、透明假光束都要控制数量 |
| 文档与场景参数不完全一致 | 脚本默认值和 `Main.unity` 序列化值有差异，执行时必须以场景为准统一 |

---

## 4. 待确认优化方案

### 4.1 第一阶段：黄昏天空与整体调色

目标画面：马拉卡纳黄昏，天际从金橙到紫蓝，场内泛光灯已亮，草坪不黑，球和角色轮廓清楚。

已完成内容：

1. 新增 `DuskSkyboxBuilder` 或扩展 `LightingConfigurator`
   - 创建程序化 Gradient Skybox 材质。
   - 顶部：深蓝紫 `#1A1040`
   - 地平线：金橙 `#E89040`
   - 地面：暗暖褐 `#0D0804`
   - 设置 `RenderSettings.skybox`
   - 保持 Quest 低成本，不上真实体积云。

2. 统一 `LightingConfigurator`
   - 主光色：偏橙红 `#FFAA60`
   - Y 角：`-8`
   - 阴影强度：`0.45`
   - Android 阴影距离保持 8m 左右。

3. 扩展 `WeatherController`
   - 新增 `DuskHaze` 模式。
   - Fog density：约 `0.004-0.006`
   - Fog color：`#332820`
   - 默认演示使用 DuskHaze，而不是 Sunny。

4. 微调 `PolishVolumeBuilder`
   - Bloom：0.8-1.1
   - Threshold：0.9-1.0
   - Post Exposure：0.1-0.25
   - Vignette：0.22-0.28
   - Saturation：10-15，避免过艳。

验收标准：

- 进入 Play 后抬头能看到明确黄昏天空，不再是单色背景。
- 球场内部亮度足够，草坪和球门不糊成黑块。
- Quest 端不出现明显眩晕、过曝或严重掉帧。

预计变更文件：

- `unity/Assets/Scripts/Effects/LightingConfigurator.cs`
- `unity/Assets/Scripts/Effects/WeatherController.cs`
- `unity/Assets/Scripts/Effects/PolishVolumeBuilder.cs`
- 可能新增 `unity/Assets/Scripts/Effects/DuskSkyboxBuilder.cs`
- `unity/Assets/Scenes/Main.unity` 挂载/参数持久化

---

### 4.2 第二阶段：材质升级与资产修复

目标：让“草坪、球门、足球、灯牌、角色”不再像默认 primitive。

拟改内容：

1. 修复草坪材质
   - 当前 `GrassMat_Upgraded.mat` 引用在 `FieldBuilder._grassMaterial` 上。
   - 但资产里 `_MainTex` 为空、未见 `_BaseMap`，需要重新创建 URP/Lit 或 URP/Simple Lit 材质。
   - 写入 `Grass_37_Albedo.png`、`Grass_37_Normal.png`。
   - 设置 tiling：6x6 或 8x8。
   - 适度降低 Smoothness，避免草坪像塑料。

2. 场地线与边界板
   - 线材保持 unlit/white，但提高 alpha 与宽度稳定性。
   - 广告板增加 2-3 种颜色块，模拟真实球场转播背景。

3. 球门材质
   - 白色门柱改为轻微金属/半粗糙材质。
   - 网从纯半透明 slab 改成更可见的浅灰网格提示：优先用少量 LineRenderer，不上密网格。

4. 足球材质
   - `Saritasa/Models/Sport_Balls/Soccer.prefab` 存在。
   - 可执行/整合 `BallMeshReplacer`，用真实足球 mesh/material 替换默认球。
   - 控制最终尺寸，避免此前“足球过大”问题复发。

5. 角色材质
   - `ArtUpgradeHelper` 已有替换 LowPolyPeople 模型的工具。
   - 若当前场景仍使用 CharacterBuilder 程序化角色，可确认后切换为低模人物。
   - 队服色：德国白/黑、阿根廷浅蓝白，少用纯红纯蓝。

验收标准：

- 草坪近看有纹理，远看有条带，不闪烁。
- 足球在 VR 内一眼可识别为足球。
- 球门在黄昏光下仍清楚。
- 不引入透明材质排序问题。

预计变更文件：

- `unity/Assets/Materials/GrassMat_Upgraded.mat`
- `unity/Assets/Scripts/Editor/ArtUpgradeHelper.cs`
- `unity/Assets/Scripts/Editor/BallMeshReplacer.cs`
- `unity/Assets/Scripts/Field/FieldBuilder.cs`
- `unity/Assets/Scenes/Main.unity`

---

### 4.3 第三阶段：进球绝杀反馈增强

目标：进球时从“普通得分”变成“比赛高潮”。

拟改内容：

1. `CrowdAnimator` 参数加量
   - `_confettiCount`：25 -> 90
   - `_confettiLifetime`：2.5 -> 4.0
   - `_confettiSpawnRadius`：4 -> 8
   - `_flashIntensityBoost`：0.6 -> 1.5
   - `_flashCount`：2 -> 3
   - `_cheerJumpHeight`：0.18 -> 0.26
   - `_cheerDuration`：1.2 -> 1.8

2. 音频顺序
   - 进球瞬间先播 `AudioManager._sfxGoal`。
   - 0.2-0.3 秒后触发 CrowdAnimator 的纸屑/闪灯/观众跳。
   - 若有可用 crowd cheer clip，填入 `_crowdCheerClips`；否则统一由 `AudioManager` 播放观众欢呼，避免空链路。

3. 镜头停留
   - 进球后 SideCam 停留略延长。
   - 保留慢镜，不新增复杂 Cinemachine 依赖。

4. 对手失败反馈
   - 红队背景 NPC 做抱头/后退/转身的小动作。
   - 目标是让镜头边缘也有反应。

验收标准：

- 成功射门时，观众、纸屑、灯光、音效在 1 秒内同时给出强反馈。
- 不成功分支仍明显区分：拦截偏压迫，射偏偏遗憾。
- Quest 上粒子不遮挡主要视线，帧率无明显断崖。

预计变更文件：

- `unity/Assets/Scripts/Effects/CrowdAnimator.cs`
- `unity/Assets/Scripts/Flow/AudioManager.cs`
- `unity/Assets/Scripts/Flow/MatchFlowController.cs`
- `unity/Assets/Scripts/Flow/ReplayDirector.cs`
- `unity/Assets/Scenes/Main.unity`

---

### 4.4 第四阶段：看台配色与球场广播感

目标：让场景更像“德国 vs 阿根廷的世界杯决赛”，不是泛用竞技场。

拟改内容：

1. 看台色板
   - 阿根廷浅蓝：`#75AADB`
   - 德国白/黑：`#F2F2F2` / `#202020`
   - 少量金色：`#F2C94C`
   - 降低通用红色比例。

2. 灯牌/广告板
   - 让 perimeter boards 出现几种世界杯感颜色块。
   - 不放真实 FIFA/品牌标识，避免版权和额外素材依赖。

3. 假光束
   - 优先用少量透明 cone/quad billboard 做“灯光空气感”。
   - 不上真实体积光。
   - 只给 4 个角灯或顶灯做低透明度表现。

验收标准：

- 第一眼能看出是足球场大赛现场。
- 灯束可见但不糊屏。
- 不明显增加 Draw Call。

预计变更文件：

- `unity/Assets/Scripts/Field/StadiumBuilder.cs`
- 可能新增 `unity/Assets/Scripts/Effects/StadiumLightBeamBuilder.cs`
- `unity/Assets/Scenes/Main.unity`

---

### 4.5 第五阶段：评分面板补接球影响

目标：补齐 V1.2 演示强化计划中“结算解释接球影响”的尾巴。

拟改内容：

1. `ScorePanel` 增加一行本轮 first touch 说明
   - 优秀：`接球质量：优秀，射门机会提升`
   - 稳定：`接球质量：稳定，射门机会正常`
   - 较差：`接球质量：较差，被对手压迫`

2. `MatchFlowController` 在显示 ScorePanel 时传入本轮接球质量或接球偏置。

验收标准：

- 玩家能在最终评分中看到接球和射门结果之间的因果关系。
- 文案短，不挡住分数和结果。

预计变更文件：

- `unity/Assets/Scripts/UI/ScorePanel.cs`
- `unity/Assets/Scripts/Flow/MatchFlowController.cs`

---

### 4.6 P1.5：免费资源小人基础动态 / 跑步动画层

目标：让队友、对手、守门员跟随 `FieldAIController` 移动时有基础动势，不再只是平移滑动。

拟改内容：

1. 资源盘点
   - `normal`、`strong`、`stout` 三套 LowPolyPeople Controller 均包含 `idle`、`walk`、`run`、`wave`。
   - 角色 prefab 自带 Animator 且 `applyRootMotion = false`，适合保留 `FieldAIController` 的根节点位移。
   - 资源不含扑救 clip，因此门将侧扑采用程序化视觉叠加。

2. Animator 方案
   - 新增 `NpcAnimationPresenter`，根据角色根节点实际平面速度自动切换 `idle` / `walk` / `run`。
   - 复用已有 Controller，不复制动画资产，不新增 Animator Controller。
   - 进球和拦截成功时复用 `wave` 作为短时庆祝反应。

3. 程序化兜底方案
   - 无 Animator 的模型按速度使用呼吸、上下起伏和身体前倾。
   - 门将进入 `Saving` 时，根据来球左右方向对视觉子节点做快速位移、抬升、倾斜和恢复。
   - 程序化动作只修改 `Model` 视觉子节点，不修改 AI 根节点、碰撞体或结算位置。

4. 与 AI 状态机绑定
   - locomotion 不依赖状态枚举硬编码，而是读取实际移动速度，兼容 AI 移动和 `DoTeammateShot` 演出位移。
   - `GoalkeeperState.Saving` 显式触发侧扑。
   - `RoundResolved` 在进球或拦截时触发对应角色庆祝。

验收标准：

- AI 小人移动时不再明显脚底滑动成“棋子平移”。
- 没有动画资源的小人也至少有可读的跑动假动作。
- 不影响 `FieldAIController` 的位置控制和碰撞逻辑。
- Quest 上不显著增加 CPU/GPU 压力。

预计变更文件：

- `unity/Assets/Scripts/Flow/FieldAIController.cs`
- `unity/Assets/Scripts/Flow/NpcAnimationPresenter.cs`
- `unity/Assets/Tests/EditMode/FieldAIControllerTests.cs`

验证状态：

- Unity 6000.4.7f1 脚本编译通过，Console 0 error。
- 三套 Animator Controller 的四个状态和 Trigger 已用 Unity Animator 查询确认。
- EditMode 测试用例已补；当前 Unity Skills 处于 Auto 模式，TestRunner 仅允许 Bypass，待切换模式或关闭编辑器后执行。

---

## 5. 执行计划

### 5.1 建议分支/提交顺序

1. `视觉优化文档`
   - 只提交本文档。

2. `黄昏天空和灯光`
   - Gradient Skybox / DuskHaze / Lighting / PostProcess。

3. `材质修复`
   - 草坪 URP 材质、球门、足球替换。

4. `进球反馈增强`
   - CrowdAnimator、音频触发、镜头停留。

5. `P1.5 小人基础动态`
   - 盘点免费小人动画资源；接 Animator 或程序化动态 presenter。

6. `评分面板补接球解释`
   - ScorePanel 与 MatchFlowController。

7. `Quest 性能回归`
   - Profiler / OVR Metrics / 截图或录屏确认。

### 5.2 每阶段验证

PC Editor：

- Play 后天空可见。
- 草坪纹理可见。
- Pass 阶段提示不遮球。
- AI 小人跑位时有基础跑动/待机动势。
- 进球反馈强烈且不乱。
- ScorePanel 能解释接球影响。

Quest 3S：

- 启动不闪退。
- 头显视角里提示可读。
- 天空/灯光不过曝。
- 小人动态不造成明显掉帧或晕动。
- 纸屑和灯光不造成明显掉帧。
- 目标帧率优先接近 72fps，最低不低于 60fps。

### 5.3 性能预算

| 项 | 建议上限 |
|----|----------|
| 实时阴影灯 | 只保留主方向光阴影；场灯不投影 |
| 纸屑粒子 | 80-100，一次性 burst |
| 假光束 | 4-8 个透明 mesh，不叠太多 |
| 草坪贴图 | Android ASTC，尺寸不超过 1K/2K 视效果决定 |
| 看台观众 | 保持 GPU instancing，不改成大量独立 Renderer |

---

## 6. 不建议现在做的事

1. 不引入 HDRP。
2. 不上真实体积光方案。
3. 不下载大型体育场资产包替换当前场景。
4. 不做复杂天气系统。
5. 不在未确认 Quest 性能前大量增加透明粒子。
6. 不手改运行时生成出来的 Stadium/Field 子物体，应修改 Builder 或序列化配置。

---

## 7. 待确认事项

请确认以下执行口径后再开始改 Unity 文件：

1. 美术方向是否锁定为“2014 世界杯决赛马拉卡纳黄昏”。
2. 是否允许新增 1-2 个小脚本：`DuskSkyboxBuilder`、可选 `StadiumLightBeamBuilder`。
3. 是否执行 `BallMeshReplacer`，把默认球替换为已有 Soccer prefab。
4. 是否执行角色替换，把程序化角色换成 LowPolyPeople 模型。
5. 进球反馈是否按“更炸场”的方向加量，接受 Quest 上再做性能回调。

确认后推荐先执行阶段 1 和阶段 2；这两项对第一眼观感提升最大。
