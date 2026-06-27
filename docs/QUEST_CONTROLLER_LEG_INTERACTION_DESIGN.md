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
A: 直接出现在手柄位置比较符合直觉。Quest Link 实测后，当前手柄到脚的默认偏移量为 `(0, -0.15, 0.16)`，整体腿/脚显示缩放为 `0.56`。
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
- Current P0 tuning defaults keep foot-ball interaction enabled:
  - `enableFootBallInteraction = true`
  - `ensurePhysicalBallInteractor = true`
  - `TrackedLegController.interactionEnabled = true`
  - This lets Quest Link / APK tests validate touch, receive, pass, and physical impulse without manually enabling hidden fields first.
- Current Quest Link calibration from headset testing:
  - Left foot offset: `(0, -0.15, 0.16)`
  - Right foot offset: `(0, -0.15, 0.16)`
  - `Leg Scale = 0.56`
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

1. Foot placement and tracking now look acceptable in Quest Link testing.
   - Both `LeftTrackedLeg` and `RightTrackedLeg` are visible.
   - The likely stale-right-hand input-action issue was fixed by recreating input actions when handedness changes.
   - Keep the one-shot diagnostics in place until the next APK validation confirms left/right controls on device.
2. Default foot visual has moved past the cube placeholder.
   - `Foot` now uses a simple procedural low-poly boot mesh.
   - Physics remains a simple `BoxCollider`.
   - Further model work should wait until the physical interaction loop is validated.
3. Scene/physics migration decision is still open.
   - Existing interfaces are enough for incremental integration: `FootContactData`, `TrackedLegController.GlobalFootContact`, `PhysicalBallInteractor`, `BallController.SetPhysicalSimulation(...)`, and `MatchFlowController.HandleFootBallContact(...)`.
   - Risk is architectural concentration in `MatchFlowController`, not missing low-level plumbing.
   - Recommended next step is a contained physical-interaction mode or test scene that reuses the current player, tracked legs, ball, and match-flow hooks, then merges the proven behavior into `Main.unity`.

### Latest Fix Notes

- 2026-06-18 physical-touch diagnostics / stability pass:
  - `TrackedLegController.Configure(...)` now ensures its `Rigidbody` and tracking references are initialized even when a scene-authored or inactive leg is configured before `Awake`.
  - Runtime default boot/sock/stripe materials are reused instead of recreated on every live tuning apply, reducing material churn during F2 tuning.
  - `QuestControllerLegRig` now prefers existing `TrackedLegController` children under the current rig and skips legs owned by another rig, reducing accidental cross-binding if a second rig appears.
  - `PhysicalTouchTest` overlay now shows left/right leg tracking state and current foot speed, so Quest Link tests can distinguish "controller not bound" from "tracked but not contacting".
- 2026-06-15 proximity contact assist pass:
  - `TrackedLegController` now performs a small `Physics.OverlapBoxNonAlloc` probe around the predicted foot box during `FixedUpdate`.
  - If the predicted foot box and ball are within the configured closest-point distance, it publishes the normal `FootContactData` path with contact zone `FootBoxProximity`.
  - This is meant to cover fast controller movement or slight visual/collider mismatch where Unity trigger callbacks can miss a visible touch.
  - `QuestControllerLegRig` exposes shared assist parameters: enable probe, padding, max closest distance, and min speed.
  - `PhysicalTouchTest` / `FootBallTuningController` F2 panel now exposes `Assist padding`, `Assist distance`, and `Assist min speed` for live Quest Link tuning.
- Added collider/contact diagnostics for the current physical-touch blocker:
  - `TrackedLegController` now draws the main foot `BoxCollider`, shin `CapsuleCollider`, and toe/instep/sole contact zones as gizmos.
  - Foot contacts now report `ContactZone`, `FootClosestPoint`, `BallClosestPoint`, and `ClosestPointDistance` in `FootContactData`.
  - Trigger contacts now compute the contact sample from foot/ball closest points instead of only using the leg root transform.
  - A short per-ball contact publish interval reduces duplicate events from overlapping debug/contact zones.
  - `[TrackedLeg]` logs now include contact zone and closest-point distance so collider/model mismatch can be diagnosed from Console logs.
- Extended `PhysicalTouchTest`:
  - Overlay now shows the latest contact zone and foot-ball closest-point distance.
  - Scene debug drawing now includes the closest-point line in addition to swing and impulse rays.
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

### PhysicalTouchTest Notes

- Added `unity/Assets/Scripts/Ball/PhysicalTouchTest.cs` as the next validation step before a tuning UI.
- Recommended use:
  - Add `PhysicalTouchTest` to an empty scene object in `Main.unity`, or create a temporary `PhysicalTouchTest` GameObject.
  - Keep `Enter On Start` enabled for a quick Quest Link run.
  - Press `R` in Play Mode to reset the physical ball in front of the player.
- Test mode behavior:
  - Enables `QuestControllerLegRig` foot-ball interaction at runtime.
  - Ensures the current `BallController` has `PhysicalBallInteractor`.
  - Routes contacts away from `MatchFlowController` by default so every valid kick can produce a physical impulse.
  - Narrows tracked-leg collision filtering to the ball's layer and puts the temporary test ground on `Ignore Raycast` to reduce contact noise.
  - Shows a small debug overlay with left/right tracking state, latest contact speed, power, accuracy, impulse direction, and ball velocity.
  - Logs `[PhysicalTouchTest]`, `[TrackedLeg]`, and `[PhysicalBall]` lines for console capture.
- `PhysicalBallInteractor` now exposes:
  - `ConfigureRouting(...)` for runtime test/tuning modes.
  - `PhysicalImpulseApplied` so debug or tuning controllers can observe the actual impulse result.
- `QuestControllerLegRig` now exposes:
  - `SetFootBallInteraction(...)` so test/tuning components can enable collision interaction and apply a ball layer mask without editing private Inspector fields.

### Latest Test Status

- Main menu input is now usable enough to enter the game in Quest Link / Unity Play Mode.
- Right stick is bound to horizontal yaw rotation only; vertical look remains driven by mouse/head orientation paths.
- Player movement is clamped to the generated field bounds to avoid leaving the pitch during possession.
- Foot-ball interaction is no longer completely blocked:
  - `QuestControllerLegRig` enables foot-ball interaction by default.
  - `TrackedLegController` uses trigger contact against objects with `BallController`.
  - `PhysicalBallInteractor` can receive those contacts and apply impulse or route them into `MatchFlowController`.
- Current blocker:
  - Foot-ball contact reliability has a code-side mitigation via `FootBoxProximity`, but still needs Quest Link / APK validation.
  - If visible touches are still missed, likely cause remains model / collider mismatch: the procedural boot visual, controller-driven pose offset, and simple foot `BoxCollider` do not line up well enough with the visible foot and ball.
  - This should still be treated as a collider / contact-assist tuning problem before changing high-level MatchFlow.
- Recommended next debugging pass:
  - Use the new gizmos and `PhysicalTouchTest` closest-point overlay to tune `Foot Collider Center`, `Foot Size`, and pose offsets from `QuestControllerLegRig`, not from generated leg transforms.
  - Compare the visible boot against the cyan main foot box plus green/magenta/orange toe/instep/sole zones in Scene view during Quest Link Play Mode.
  - If the closest-point distance is near zero but no impulse is applied, tune `PhysicalBallInteractor` thresholds/cooldown next.
  - If the visible boot overlaps the ball but closest-point distance stays large, retune collider center/size before changing MatchFlow.
  - If the distance is slightly positive but visually acceptable, raise `Assist padding` / `Assist distance` only enough to cover that gap.
  - Keep `PhysicalTouchTest` as the main test path until contact reliability is acceptable.

### Planned Next Changes

- Validate on APK that left/right controller bindings remain distinct.
- Run `PhysicalTouchTest` through Quest Link and APK:
  - Confirm fast swings do not miss contact.
  - Confirm the ball resets reliably and does not fight scripted `MatchFlow` control.
  - Capture closest-point distance ranges for good visual touches vs missed touches.
  - Capture useful ranges for foot speed, `power01`, `accuracy01`, impulse, lift, and cooldown.
- Continue using the existing `FootBallTuningController`:
  - Runtime sliders for impulse, lift, speed thresholds, collider size, reset offset, and contact assist.
  - Optional handoff back into `MatchFlowController` once the physical touch loop is stable.
- After contact tuning, decide whether to replace the scripted pass/shot arcs inside `Main.unity` or keep them as authored presentation around physical touch points.

### Runtime Components

- `unity/Assets/Scripts/Player/TrackedLegController.cs`
  - 直接读取 `<XRController>{LeftHand|RightHand}/devicePosition`、`deviceRotation`、`deviceVelocity`、`deviceAngularVelocity`。
  - `Update` 缓存最新 tracking pose，`FixedUpdate` 使用 `Rigidbody.MovePosition` / `MoveRotation` 同步 kinematic 脚部刚体。
  - 默认偏移为 `(0, -0.15, 0.16)`，左右脚可以分别在 Inspector 调整 `poseOffsetPosition` / `poseOffsetEuler`。
  - 自动创建简化可视模型：脚部 box、小腿 capsule、左右脚颜色区分。
  - 自动配置脚部 `Rigidbody`、`BoxCollider` 和小腿 `CapsuleCollider`，接触球时发送 `FootContactData`。
  - runtime 默认材质在调参过程中复用同一组实例，避免频繁滑动 F2 面板时不断创建新材质。

- `unity/Assets/Scripts/Player/QuestControllerLegRig.cs`
  - 运行时创建 `LeftTrackedLeg` / `RightTrackedLeg`。
  - 手柄位置默认按“手柄相对 HMD 的 tracking pose”映射到实际渲染相机 `Player/FpsAnchor/FpsCamera` 附近，避免 XR Origin 与玩家相机空间不一致时脚飘到天上。
  - 当前 P0 默认 `enableFootBallInteraction = true` 且自动给球挂 `PhysicalBallInteractor`，用于优先验证脚部触球链路。
  - 当前默认 `Leg Scale = 0.56`，并支持 Play Mode 中从 `QuestControllerLegRig` 实时同步 offset/scale/size 到左右脚。
  - 查找已有左右脚时优先使用当前 rig 层级内的 leg，避免多 rig 或临时测试对象互相抢绑定。
  - 如需回到纯显示调试，可在 Inspector 关闭 `enableFootBallInteraction` 和 `ensurePhysicalBallInteractor`。

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

## 2026-06-12 Current Runtime State

### Implemented

- Foot-ball contact is now treated as continuous physical control by default.
  - During `MatchFlowController.Phase.Possession`, normal foot contact returns `false` from `HandleFootBallContact(...)`.
  - This lets `PhysicalBallInteractor` continue applying passive control or physical impulse.
  - A small foot movement should not lock the match into pass / shot judgement.
- Right trigger is now only a pass-intent marker.
  - Pressing right trigger while contacting the ball records a pending pass judgement.
  - The ball still remains physically controlled after that touch.
  - The actual pass result is delayed until the ball reaches the teammate area.
- Current judgement events are:
  - Right trigger contact: mark pass intent only.
  - Ball reaches teammate radius after pass intent: resolve intercepted / successful / missed teammate shot by pass power and arrival speed.
  - Ball leaves field bounds or hits the invisible boundary wall: resolve miss / out of play.
  - Ball enters the opponent goal volume: resolve score.
- Direct physical shooting is allowed while still in possession.
  - The player can freely kick the ball toward the opponent goal.
  - Match flow resolves only when the goal volume or out-of-bounds condition is detected.
- Boundary protection exists in both the match flow and physical touch test path.
  - `MatchFlowController` creates an invisible `MatchFlowBoundary` around the generated field.
  - `PhysicalTouchTest` creates its own invisible test boundary when running that mode.
- Foot height and control were tuned from the previous test pass.
  - Feet can be locked near the ground plane with a small sole clearance.
  - Low-speed contacts no longer disappear before they reach passive ball control.
  - Ball rolling into the foot should be controllable / stoppable without requiring a large swing.
- Stadium / field corner interference was addressed in `StadiumBuilder`.
  - The stadium oval now expands enough to clear the rectangular pitch corners.
  - This keeps the pitch size while reducing corner overlap with stadium boards / stands.
- `PhysicalTouchTest` avoids creating extra ground when a real `FieldBuilder` field exists.
  - This prevents the temporary test ground from visibly fighting the real pitch or field edge.

### Current Intended Player Feel

- Free control is the default state after receiving the ball.
- The player can dribble, stop, nudge, and kick the ball without holding right trigger.
- Holding / pressing right trigger does not freeze play by itself.
- Right trigger only means: treat this touch as a pass attempt if the ball later reaches the teammate.
- The match should only leave free-control possession when one of these happens:
  - pass-intent ball reaches teammate,
  - ball goes out of bounds,
  - ball hits the invisible boundary,
  - ball enters the opponent goal.

### Known Issues / Needs Verification

- Quest device validation is still required.
  - The current changes compile, but physical feel must be checked in Quest Link / APK.
  - Verify that ordinary touches remain responsive without right trigger.
  - Verify that right trigger pass intent does not prematurely lock movement.
- Pass judgement tuning is still rough.
  - Current result uses stored pass power plus ball arrival speed near teammate.
  - Thresholds may need adjustment after headset testing:
    - `_teammatePassSlowPower`
    - `_teammatePassFastPower`
    - `_teammatePassArrivalRadius`
    - `_teammatePassGoodScoreChance`
- Direct shot feel still depends on physical impulse tuning.
  - If shots feel weak or too strong, tune `PhysicalBallInteractor` impulse / lift / cooldown before changing match flow again.
- Boundary and goal judgement are still coarse volumes.
  - Opponent goal is checked as a simple field-local box.
  - Boundary is checked by field extents and invisible cube walls.
  - If corner or post cases feel unfair, replace with explicit goal-mouth trigger colliders later.
- Goal-mouth boundary and out-of-bounds text have been addressed in code and still need Quest Link / APK verification.
  - Front/back boundary walls now leave a goal-mouth gap.
  - `OpponentGoalTrigger` handles direct shots through the opponent goal.
  - Sideline exits show `OUT OF BOUNDS`; goal-line exits show `JUST MISSED`.
  - Verify that the ball no longer sinks or catches near the goal-mouth edge.
- Pass arrival has an intermittent ball reset bug.
  - Sometimes after a pass is caught / reaches the teammate area, the ball refreshes to the exact field center.
  - In that state the ball can appear half sunk into the pitch.
  - Suspect conflict between physical possession, scripted teammate-shot setup, `BallController.SetPhysicalSimulation(...)`, and transform repositioning.
  - Repro to capture: press right trigger for pass intent, get ball near teammate, observe whether ball teleports to center before the teammate-shot / result sequence.
- `MatchFlowController` is becoming too large.
  - Next cleanup should split physical possession judgement into a smaller helper component or service after behavior stabilizes.

### Latest Verification

- `dotnet build unity/Assembly-CSharp.csproj --no-restore` passed with 0 errors.
- Remaining warnings are existing Unity obsolete API / serialized-field warnings and are not blocking this pass.

## 2026-06-12 Goal-Mouth Boundary Pass

### Implemented

- `MatchFlowController` now leaves a physical gap in the generated front/back boundary walls around the goal mouth.
- Added a runtime `OpponentGoalTrigger` so direct physical shots through the opponent goal mouth resolve as score without fighting the boundary wall.
- Boundary hits are now classified as `Sideline`, `GoalLine`, or `Fall`.
- Sideline exits show `OUT OF BOUNDS`; goal-line exits show `JUST MISSED`.
- `ScorePanel.Show(...)` now accepts optional text overrides for immediate physical-result labels while preserving the existing `Show(Scenario)` path.

### Verification

- `dotnet build unity/Assembly-CSharp.csproj --no-restore` passed with 0 errors.
- Remaining warnings are existing Unity obsolete API / serialized-field warnings.

### Needs Quest Link / APK Check

- Kick straight into the opponent goal mouth: should resolve as `GOAL`.
- Kick wide past the front/back goal line: should resolve as `JUST MISSED`.
- Kick over the left/right sideline: should resolve as `OUT OF BOUNDS`.
- Confirm the ball no longer catches or sinks at the goal-mouth edge.

## 2026-06-12 Feel Tuning Pass

### Implemented

- Feet moved slightly forward: default left/right pose offset is now `(0, -0.15, 0.16)`.
- Boots / foot colliders are longer: default foot collider center is `(0, -0.03, 0.18)` and default foot size is `(0.2, 0.11, 0.58)`.
- Right-stick horizontal yaw speed doubled from `95` to `190`.
- Passive low-speed ball control is disabled by default in `PhysicalBallInteractor`, so low-speed / sole-like contact no longer directly brakes and holds the ball.
- Physical impulse force was reduced:
  - min impulse `1.2 -> 0.9`
  - max impulse `7.0 -> 5.2`
  - lift `0.12 -> 0.09`
  - spin torque `0.08 -> 0.06`
- `PhysicalTouchTest` / `FootBallTuningController` defaults were updated to match the runtime defaults.

### Verification

- `dotnet build unity/Assembly-CSharp.csproj --no-restore` passed with 0 errors.
- Remaining warnings are existing Unity obsolete API / serialized-field warnings.

## 2026-06-12 Instep Shot Assist Pass

### Intended Player Action

- For a normal instep shot with hand controllers, the closest physical metaphor is:
  - rotate the controller / "foot" slightly across the ball instead of pointing straight through it,
  - keep the elbow / hand path slightly outside,
  - swing hard forward toward the opponent goal.
- In-game this should read as "斜脚背抽射", not a toe poke and not a sole stop.

### Implemented

- `MatchFlowController` now evaluates whether a possession-phase physical kick looks like an instep shot.
- The check uses:
  - `ContactZone` (`InstepZone` scores highest),
  - foot/controller forward angle,
  - swing direction toward the opponent goal,
  - kick power.
- If the shot already points roughly toward goal, the final physical impulse direction is gently biased toward the opponent goal center.
- Max assist is capped at `0.28`, so this is correction / forgiveness rather than full auto-aim.
- `[PhysicalBall]` logs now include `assist=0.xx` so Quest Link tests can confirm whether the action was recognized.

### Verification

- `dotnet build unity/Assembly-CSharp.csproj --no-restore` passed with 0 errors.
- Remaining warnings are existing Unity obsolete API / serialized-field warnings.

## 2026-06-12 Leg Scale / Motion Note

### Implemented

- Legs were slightly enlarged for Quest readability:
  - default `Leg Scale` is now `0.56`.
  - default `Shin Collider Center` is now `(0, 0.29, -0.08)`.
  - default `Shin Height` is now `0.68`.
- The same defaults were applied to `QuestControllerLegRig`, `TrackedLegController`, and `PhysicalTouchTest` / `FootBallTuningController`, so model size and hitbox size stay aligned.

### Recorded Issue

- The controller angle is already read from `<XRController>{LeftHand|RightHand}/deviceRotation` and applied to each tracked leg.
- The current visual still feels fixed because the generated leg is one rigid boot/shin assembly. It has no separate ankle/knee articulation, so the whole model rotates as one piece instead of showing a natural lower-leg pose.
- Next visual pass should split the display into at least:
  - foot / boot driven directly by controller rotation,
  - shin segment with a softer derived orientation,
  - optional knee anchor or bend hint so controller roll/pitch reads as leg motion rather than a stiff prop.

### Debug Checklist

- Play Mode 中应出现 `LeftTrackedLeg` 和 `RightTrackedLeg`。
- Quest 真机中脚/鞋位置应跟随左右手柄真实位置，转动手柄时脚模型同步旋转。
- 默认 visual-only 时脚碰球不会改球和比赛流程。打开脚触球后，Console 会打印 `[TrackedLeg]`；比赛流程消费触球时会打印 `[MatchFlow] Foot receive` 或 `[MatchFlow] Foot shot`。
- 如 `http://localhost:8090/health` 返回 Invalid host，用 `http://127.0.0.1:8090/health` 访问 UnitySkills。

## 2026-06-27 VR Handoff Notes

当前 VR 设备在身边，下一步优先做 Quest / VR 现场测试，而不是继续改 PC Arena 镜头。

### Record These During Test

- 启动方式：Unity Play Mode + Quest Link / APK / XR Simulator。
- 当前模式：Training / ArenaAttack；控制档：KeyboardMouse / Gamepad / VrStriker / XrSimulator。
- 头显里是否能看到主菜单、球场、球、玩家脚 / 鞋。
- `LeftTrackedLeg` / `RightTrackedLeg` 是否都出现，是否分别跟随左右手柄。
- 转动手柄时脚 / 鞋是否同步旋转，是否仍像一个僵硬整体。
- 可见脚碰到球时，球是否有反应。
- 右 trigger 是否只作为 pass intent，是否会提前锁死或打断自由触球。
- 高速触球是否能形成直接射门。
- 球进对方球门是否判 `GOAL`，踢出边线 / 底线是否判 `OUT OF BOUNDS` / `JUST MISSED`。
- 如果球没反应，记录 overlay 中的 tracking、contact zone、closest-point distance、foot speed、ball velocity。
- 如果球突然回中心或半陷地面，记录触发前最后一个动作：普通触球 / 右 trigger pass intent / 到队友附近 / 进球口 / 出界。

### Next Chat Starting Point

- 如果 VR 脚触球可用：优先调手感参数，包括 impulse、lift、cooldown、assist distance、foot collider center / size。
- 如果 VR 脚可见但触球不可用：先查 `TrackedLegController` contact 发布、ball layer mask、`PhysicalBallInteractor` 是否挂载、overlay closest-point distance。
- 如果 VR 脚不可见或左右绑定错：先查 `QuestControllerLegRig` 是否生成左右腿、handedness 绑定、input action diagnostics。
- PC Arena 的“视角没有变化、射门 / 传球按不了”另开只读诊断，不和 VR 现场测试混在一起修。
