# Quest Controller As Leg Interaction Design

## Goal

把 Meta Quest 3S 手柄的真实 6DoF 位姿作为玩家腿/脚的真实交互实体：

- 手柄在真实空间哪里，游戏里对应的腿/脚模型就在哪里。
- 手柄怎么旋转，腿/脚模型就按对应姿态旋转。
- 足球接球、传球、射门不再只靠按钮评分，而是由这个“手柄驱动的脚部实体”和球的碰撞、速度、方向共同决定。

这不是第一人称脚部动画，也不是根据射门状态播放一段腿部动作。上一版 `FirstPersonLegAvatar` 属于动画代理方案，不符合这个目标；确认本设计后应移除或重写。

## Core Concept

运行时创建两个 `TrackedLegController`：

- `LeftTrackedLeg`
- `RightTrackedLeg`

每个对象直接读取对应 Quest 手柄：

- position: `<XRController>{LeftHand}/devicePosition` / `<XRController>{RightHand}/devicePosition`
- rotation: `<XRController>{LeftHand}/deviceRotation` / `<XRController>{RightHand}/deviceRotation`
- velocity: `<XRController>{LeftHand}/deviceVelocity` / `<XRController>{RightHand}/deviceVelocity`
- angular velocity if available, otherwise frame-delta fallback

这些对象不是挂在摄像机前方做假显示，而是放在 XR tracking space 对应的真实位置。效果应接近 Quest 主菜单里手柄模型的位置，只是可见模型换成足球运动员腿部/脚部。

## Visual Model

第一阶段可先用简化模型验证：

- 小腿：capsule 或低模 mesh
- 脚/球鞋：box/mesh
- 左右脚区分颜色或鞋面条纹

后续替换为正式腿部模型：

- 模型 pivot 应放在“脚踝或手柄握持参考点”附近。
- 需要一个可调 `poseOffsetPosition` 和 `poseOffsetRotation`，用于把手柄坐标系校准到脚模型坐标系。
- 需要左右脚独立 offset，因为左右手柄姿态和模型镜像不一定对称。

## Physics Shape

每个腿部实体至少包含：

- `Rigidbody`
  - `isKinematic = true`
  - `collisionDetectionMode = ContinuousSpeculative`
  - `interpolation = Interpolate`
- 脚部主碰撞箱
  - 推荐 `BoxCollider`
  - 覆盖鞋底、脚背、脚尖
- 小腿辅助碰撞体
  - 推荐 `CapsuleCollider`
  - 主要用于挡球或视觉一致，不一定参与强力踢球

球必须逐步迁移为真正物理球：

- 有 `Rigidbody`
- 有 `SphereCollider`
- 在可交互阶段不应只用 `transform.position` 插值控制
- 如果剧情/传球阶段仍需脚本移动球，需要在进入玩家交互窗口时切换到物理控制

## Tracking Update

手柄跟踪对象应在 `Update` 或 `BeforeRender` 读取输入，在 `FixedUpdate` 同步物理目标。

推荐结构：

1. `Update`
   - 读取手柄 position / rotation / velocity
   - 缓存最新 tracking pose

2. `FixedUpdate`
   - 用 `Rigidbody.MovePosition`
   - 用 `Rigidbody.MoveRotation`
   - 记录上一帧物理位置，用于 fallback velocity

3. `OnCollisionEnter/Stay` 或 trigger contact
   - 根据接触点、脚部速度、球相对方向计算交互结果

## Interaction Model

### Receive

接球阶段开启脚部碰撞判定。

判定维度：

- 球是否在接球窗口内接触脚部碰撞体
- 脚部速度是否过大
- 脚面朝向是否合理
- 接触点是否接近鞋面/脚内侧，而不是小腿乱撞

输出：

- `receiveQuality`
- 好接球：降低球速，吸附/缓冲到玩家附近
- 差接球：球弹开，或进入 Recovery

### Pass

传球阶段使用脚部速度和触球方向。

判定维度：

- 脚部触球瞬间速度
- 脚面 forward/up/right 与目标方向的夹角
- 接触点在脚尖、脚背、内侧的位置

输出：

- ball impulse
- pass accuracy
- pass power

### Shoot

射门阶段不再只靠右扳机释放评分。右扳机可以作为“进入射门意图/防误触”的 gating 输入，但射门结果应来自真实碰撞。

建议：

- 右扳机按住：允许右脚进入 shoot-active 状态
- 右脚碰到球：根据脚部速度、方向、接触点给球施加 impulse
- 松开右扳机：结束 shoot-active，避免随手乱碰都变射门

射门数据：

- `footSpeed`
- `footDirection`
- `contactNormal`
- `contactPoint`
- `aimDirection`
- `power01`
- `accuracy01`

最终可映射到：

- 真实物理射门
- 或先过渡到当前 `MatchFlowController.HandlePlayerShot(power, direction)` 的成功/失败路由

## Existing Code Impact

当前相关代码：

- `FPSPlayerController`
  - 现在负责右扳机 Motion Shot 输入评分
  - 后续应拆出输入 gating，不再直接根据手柄位移生成射门分数

- `MatchFlowController`
  - 现在通过 `OnShoot(power, direction)` 处理射门结果
  - 后续可以新增 `OnFootBallContact(FootContactData data)` 或 `OnPhysicalShot(data)`

- `BallController`
  - 当前有 Attach/Detach 和视觉轨迹逻辑
  - 后续要支持物理交互模式与脚本控制模式切换

- `FirstPersonLegAvatar`
  - 上一版误实现
  - 确认本方案后应删除，或改造成只负责显示真实 tracked foot pose，不再自己推导 kick animation

## Proposed Components

### `TrackedLegController`

职责：

- 读取左/右手柄真实位姿
- 同步腿/脚模型
- 同步 kinematic Rigidbody
- 输出脚部速度和触球事件

主要字段：

- `Handedness handedness`
- `Transform visualRoot`
- `Rigidbody footBody`
- `Collider footCollider`
- `Vector3 poseOffsetPosition`
- `Vector3 poseOffsetEuler`
- `float minInteractionSpeed`
- `LayerMask ballLayer`

### `FootContactData`

包含：

- `Handedness foot`
- `Vector3 footPosition`
- `Quaternion footRotation`
- `Vector3 footVelocity`
- `Vector3 contactPoint`
- `Vector3 contactNormal`
- `float contactSpeed`
- `float power01`
- `float accuracy01`

### `PhysicalBallInteractor`

职责：

- 接收脚部碰撞事件
- 根据当前比赛阶段决定这是接球、传球、射门还是普通碰撞
- 对球施加 impulse 或交给 `MatchFlowController`

## Phased Implementation

### Phase 1: Real Tracked Leg Visualization

目标：

- 玩家在 Quest 中能看到左右腿/脚模型实时跟随手柄真实位置。
- 不做球交互。
- 提供 Inspector offset，用于调到“手柄模型换成脚模型”的感觉。

验收：

- 左右脚位置与 Quest 主菜单手柄位置一致感强。
- 转动手柄时脚模型姿态实时变化。
- 低头、转身、移动时模型不漂移。

### Phase 2: Foot Colliders And Debug Contacts

目标：

- 给脚模型加 Rigidbody/Collider。
- 球也具备 Rigidbody/SphereCollider。
- 触球时只打印 debug 数据，不改变玩法。

验收：

- 能看到接触点、脚速度、接触速度。
- 快速挥动不漏碰撞。
- 不会把玩家自身或场景撞飞。

### Phase 3: Receive By Foot Contact

目标：

- 接球从按钮输入迁移为脚部接触。
- 好接球/差接球由触球时机、速度、角度决定。

验收：

- 接球窗口内碰球可以进入 Possession。
- 太早、太晚、脚速太大都会降低接球质量。

### Phase 4: Physical Shoot And Pass

目标：

- 传球/射门由脚部真实碰撞驱动。
- 右扳机只作为射门意图 gating，不直接决定球飞出去。

验收：

- 右脚实际踢中球才射门。
- 力度来自脚速度。
- 方向来自脚面/挥动方向/接触点。

## Open Questions

1. 是否两个手柄都要显示为腿，还是只把右手柄映射成主射门脚，左手柄暂时作为支撑脚？
A: 两个都是腿比较符合直觉
2. 手柄握在手里时，脚模型应该直接出现在手柄位置，还是需要一个“手柄到脚”的固定偏移，让玩家拿手柄模拟脚的位置？
A: 直接出现在手柄位置比较符合直觉。Quest Link 实测后，当前手柄到脚的默认偏移量为 `(0, -0.15, 0.1)`，整体腿/脚显示缩放为 `0.5`。
3. 射门阶段是否必须按住右扳机才允许触球，还是任何高速脚部碰撞都可触发？
A: 任何高速脚部碰撞都可触发，右扳机只是作为射门意图 gating，不直接决定球飞出去。
4. 球是否要完全物理化，还是先保持当前剧情球路，只在玩家交互窗口短暂物理化？
A: 完全物理化

实现功能和简易实现说明需要记录在此文档，以便于排错和后续开发。

## Implementation Notes

### Current Runtime Calibration

- `QuestControllerLegRig` now uses render-camera-relative tracking:
  - It resolves `Player/FpsAnchor/FpsCamera` first, then falls back to `Camera.main`.
  - It reconstructs controller world pose from controller local pose plus HMD local pose, avoiding the earlier issue where the foot appeared in the upper-right sky when XR Origin space did not match the actual render camera space.
- Runtime-created legs are parented under `Player`, not under XR Origin, so they are easier to find in Hierarchy.
- Default visual-only safety remains enabled:
  - `enableFootBallInteraction = false`
  - `ensurePhysicalBallInteractor = false`
  - `TrackedLegController.interactionEnabled = false`
  - With these defaults, foot visuals do not affect ball physics, receive/pass/shoot, or MatchFlow.
- Current Quest Link calibration from headset testing:
  - Left foot offset: `(0, -0.15, 0.1)`
  - Right foot offset: `(0, -0.15, 0.1)`
  - `Leg Scale = 0.5`
  - `Live Update In Play Mode = true`
- Runtime tuning should be done on `Player -> QuestControllerLegRig`, not directly on generated `LeftTrackedLeg` / `RightTrackedLeg` transforms. Generated leg transforms are continuously driven by tracking and will override manual Transform edits.
- `QuestControllerLegRig` now exposes shared size controls:
  - `Leg Scale`
  - `Foot Collider Center`
  - `Foot Size`
  - `Shin Collider Center`
  - `Shin Radius`
  - `Shin Height`
- `Live Update In Play Mode` pushes changed Inspector values from `QuestControllerLegRig` into both generated legs while Play Mode is running.
- Quest Link / Air Link is the recommended fast test path for foot placement:
  - Use Unity Play Mode through Quest Link for real HMD/controller tracking.
  - Use APK only for final Android/standalone validation.
  - Purple materials in Quest Link PC Play Mode can be ignored for now because APK testing previously showed resources loading correctly.

### Current Known Issues

1. Left foot is not visible in the current Play/Quest Link test.
   - In Scene view, using Shift+F on left and right foot appears to frame nearly the same object or same-looking object.
   - Next session should first diagnose whether `LeftTrackedLeg` and `RightTrackedLeg` are truly distinct GameObjects with distinct handedness, input bindings, visual roots, and positions.
   - Check that `FindExistingLeg(handedness)` does not accidentally bind both references to the same `TrackedLegController`.
   - Check whether left controller input actions have valid controls and whether `_hasTracking` becomes false for the left leg, which would hide its visual/colliders.
2. Default foot visual is still a cube-like placeholder.
   - It currently reads visually like a brick, even after scale reduction.
   - Next visual pass should replace the cube foot with a small football-boot-like shape: compact, tapered, water-drop/teardrop silhouette, slightly wider at heel/midfoot and narrower at toe.
   - Keep it procedural/simple for now unless importing a real boot mesh is explicitly chosen.
   - Preserve clear left/right color distinction until the tracking issue is fixed.

### Latest Fix Notes

- Fixed a likely left-foot tracking cause in `TrackedLegController`:
  - New components were enabled with the default `Right` handedness before `QuestControllerLegRig.Configure(...)` changed the left leg to `Left`.
  - This could leave `LeftTrackedLeg` named/colored as left while its input actions still read `<XRController>{RightHand}`.
  - `Configure(...)` now recreates input actions whenever handedness changes while the component is active.
- Added one-shot Play Mode diagnostics:
  - `QuestControllerLegRig` logs left/right leg reference hashes and hierarchy paths, including whether both references point to the same component.
  - `TrackedLegController` logs handedness, expected hand, visual root path, and first bound position/rotation/velocity controls.
  - If left tracking still fails, check for `[TrackedLeg] Diagnostics LeftTrackedLeg ... positionControl='none'` in Console.
- Added a guard for accidental same-component binding:
  - If `_leftLeg == _rightLeg`, the rig clears and rebinds the right leg.
- Replaced the cube-like default `Foot` visual with a small procedural low-poly boot mesh:
  - Narrow toe, wider heel/midfoot, low height profile.
  - Physics remains a simple `BoxCollider`.
  - Left/right boot colors and the strike stripe remain visible for debugging.

### Planned Next Changes

- Diagnose left-foot invisibility before changing the model:
  - Log or inspect left/right `TrackedLegController.Handedness`.
  - Confirm `_leftLeg != _rightLeg`.
  - Confirm generated hierarchy contains separate `LeftTrackedLeg/TrackedLegVisual/Foot` and `RightTrackedLeg/TrackedLegVisual/Foot`.
  - Confirm left controller input bindings are live in Quest Link and APK paths.
- Improve foot mesh shape after left/right tracking is reliable:
  - Replace the current cube `Foot` primitive with a low-poly procedural boot mesh or a small set of scaled primitives.
  - Suggested procedural mesh points:
    - Longer local Z axis for shoe length.
    - Narrower toe than midfoot.
    - Slightly rounded/tapered toe cap.
    - Low height profile, not a block.
  - Keep collider simple (`BoxCollider`) for now; visual mesh can be prettier than the physics shape.

### Runtime Components

- `unity/Assets/Scripts/Player/TrackedLegController.cs`
  - 直接读取 `<XRController>{LeftHand|RightHand}/devicePosition`、`deviceRotation`、`deviceVelocity`、`deviceAngularVelocity`。
  - `Update` 缓存最新 tracking pose，`FixedUpdate` 使用 `Rigidbody.MovePosition` / `MoveRotation` 同步 kinematic 脚部刚体。
  - 默认偏移为 `(0, -0.15, 0.1)`，左右脚可以分别在 Inspector 调整 `poseOffsetPosition` / `poseOffsetEuler`。
  - 自动创建简化可视模型：脚部 box、小腿 capsule、左右脚颜色区分。
  - 自动配置脚部 `Rigidbody`、`BoxCollider` 和小腿 `CapsuleCollider`，接触球时发送 `FootContactData`。

- `unity/Assets/Scripts/Player/QuestControllerLegRig.cs`
  - 运行时创建 `LeftTrackedLeg` / `RightTrackedLeg`。
  - 手柄位置默认按“手柄相对 HMD 的 tracking pose”映射到实际渲染相机 `Player/FpsAnchor/FpsCamera` 附近，避免 XR Origin 与玩家相机空间不一致时脚飘到天上。
  - 默认 `enableFootBallInteraction = false` 且不自动给球挂 `PhysicalBallInteractor`；先用于调试显示位置，不影响现有 MatchFlow/球流程。
  - 当前默认 `Leg Scale = 0.5`，并支持 Play Mode 中从 `QuestControllerLegRig` 实时同步 offset/scale/size 到左右脚。
  - 需要进入脚触球阶段时，再在 Inspector 打开 `enableFootBallInteraction` 和 `ensurePhysicalBallInteractor`。

- `unity/Assets/Scripts/Ball/PhysicalBallInteractor.cs`
  - 确保球有 `Rigidbody` 和 `SphereCollider`。
  - 优先把脚触球事件转交给 `MatchFlowController.HandleFootBallContact`。
  - 如果当前不在比赛流程可消费的阶段，则按脚速/脚面方向给球施加真实物理 impulse。

### Existing Code Changes

- `FPSPlayerController`
  - `_showFirstPersonLegs` 现在创建 `QuestControllerLegRig`。
  - 不再自动创建旧的 `FirstPersonLegAvatar`；如果旧组件已经挂在 Player 上，会在运行时禁用。

- `MatchFlowController`
  - 新增 `HandleFootBallContact(FootContactData data)`。
  - `Pass` 阶段：脚触球会根据传球进度、脚速、脚面方向计算接球质量。
  - `Possession` 阶段：达到最小脚部力量后，用脚速 `power01` 和挥动方向触发现有射门路由。
  - `Recovery` 阶段：脚触球暂时等同一次 recovery press。

- `BallController`
  - 新增物理控制开关。
  - 脚本挂载/插值控制球时保持 `Rigidbody.isKinematic = true`。
  - 物理脚触球时切到非 kinematic，并启用连续碰撞检测。

### Debug Checklist

- Play Mode 中应出现 `LeftTrackedLeg` 和 `RightTrackedLeg`。
- Quest 真机中脚/鞋位置应跟随左右手柄真实位置，转动手柄时脚模型同步旋转。
- 默认 visual-only 时脚碰球不会改球和比赛流程。打开脚触球后，Console 会打印 `[TrackedLeg]`；比赛流程消费触球时会打印 `[MatchFlow] Foot receive` 或 `[MatchFlow] Foot shot`。
- 如 `http://localhost:8090/health` 返回 Invalid host，用 `http://127.0.0.1:8090/health` 访问 UnitySkills。
