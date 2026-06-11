---
name: vr-diagnose
description: 一键诊断 Quest 真机/Quest Link 运行时问题。拉 adb logcat 过滤 Unity 异常 + 项目自定义日志 tag（[TrackedLeg] / [MatchFlow]），并通过 UnitySkills REST（127.0.0.1:8090）查 Hierarchy/组件，自动比对设计文档的 Debug Checklist，定位「左脚不可见」「脚飘到天上」「触球不响应」这类问题的根因。
user-invocable: true
allowed-tools:
  - Read
  - Bash
  - Grep
  - Glob
---

# /vr-diagnose — VR 运行时问题诊断

SoccerBot 项目专用。脚追踪/球交互/比赛流程在 Quest 上不对劲时，先跑这个，少在头显里反复试。

输出方向：**给出最可能的根因 + 下一步动作**，而不是把日志倒给用户自己看。

参数：`$ARGUMENTS`
- 症状关键词（可选），如 `left-foot`、`shoot`、`tracking`、`material`，用来聚焦排查方向。
- `--logcat-only`：只看真机日志，不查 Editor 场景。
- `--scene-only`：只查 Editor/Quest Link 场景，不连 adb。

---

## 前置检查（并行）

1. **adb**：`adb devices`。没设备 → 提示"确认 Quest 已连接并允许调试"，转 `--scene-only` 路径或停。
2. **UnitySkills REST**：`curl -s http://127.0.0.1:8090/health`。
   - **必须用 `127.0.0.1` 而不是 `localhost`**——localhost 会返回 `Invalid host`（设计文档已记录此坑）。
   - 不通 → 提示用户在 Unity 里确认 UnitySkills 服务已启动（Editor 开着 + 项目装了 `com.besty.unity-skills`），转 `--logcat-only` 路径或停。
3. 读对应设计文档的 `### Debug Checklist` / `### Current Known Issues` 段落，作为本次比对基准。脚部相关默认读 `docs/QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md`。

---

## A. 真机日志路径（adb logcat）

```bash
adb logcat -d -v time -s Unity OVRPlugin DEBUG AndroidRuntime
```

`-d` 一次性 dump 不阻塞。重点抓：

- **项目自定义 tag**：grep `[TrackedLeg]`、`[MatchFlow]`、`[BuildAndroid]`。
  - `[TrackedLeg] Diagnostics ... positionControl='none'` → 该手柄输入绑定没生效，是"脚不动/不可见"的直接证据。
  - `[MatchFlow] Foot receive` / `[MatchFlow] Foot shot` → 脚触球已被比赛流程消费（说明 `enableFootBallInteraction` 已开）。
- **异常**：`Exception`、`NullReferenceException`、`IL2CPP` 崩溃栈。
- **帧率/XR**：`OVRPlugin` 的 `App framerate dropped`、XR 未启用导致的黑屏线索。

要持续跟（边操作边看）时改用 `run_in_background: true` 的 `adb logcat -s Unity` 或直接用 [quest-build](../quest-build/SKILL.md) `--logcat`。

---

## B. 场景路径（UnitySkills REST / unity-skills skill）

用已安装的 `unity-skills` skill 或直接 REST 查 Hierarchy 与组件。**具体 endpoint 以 unity-skills skill 的 schema 为准，不要硬编码猜测的路径。** 要查的事实：

按设计文档 Debug Checklist 逐条核对：

1. **左右腿是不是两个独立对象**：Hierarchy 里应有 `Player/LeftTrackedLeg` 和 `Player/RightTrackedLeg` 两个不同 GameObject。
   - 若只有一个、或两个引用指向同一 component → 命中 Known Issue「`_leftLeg == _rightLeg`」。查 `unity/Assets/Scripts/Player/QuestControllerLegRig.cs` 的 `FindExistingLeg(handedness)` 是否把两个引用绑到了同一个 `TrackedLegController`。
2. **handedness 是否正确**：`LeftTrackedLeg` 上的 `TrackedLegController.Handedness` 应为 `Left`，且输入绑定应读 `<XRController>{LeftHand}`。
   - 若 left 腿仍读 `{RightHand}` → 命中「component 在 `Configure(...)` 改 handedness 前就以默认 `Right` 启用」的旧 bug，查 `Configure(...)` 是否在 handedness 变化时重建了 input actions。
3. **可视子物体在不在**：两条腿各自的 `TrackedLegVisual/Foot` 子物体应分别存在、位置不同。
4. **交互开关状态**：默认应是 visual-only（`enableFootBallInteraction = false`、`ensurePhysicalBallInteractor = false`、`TrackedLegController.interactionEnabled = false`）。排"触球不响应"时先确认这些是否按预期打开。

---

## 已知问题速查表（命中即给结论）

| 症状 | 最可能根因 | 去哪验证 |
|---|---|---|
| 左脚不可见 / Shift+F 两脚框到同一物体 | `_leftLeg == _rightLeg`，或 left 腿 `positionControl='none'` | logcat 抓 `[TrackedLeg] Diagnostics`；场景查两腿是否独立 + handedness |
| 脚飘到右上方天空 | XR Origin 空间与渲染相机空间不一致 | `QuestControllerLegRig` 是否走 render-camera-relative（解析 `Player/FpsAnchor/FpsCamera` 再回退 `Camera.main`） |
| 触球不改变球/流程 | visual-only 默认未打开交互 | 确认 `enableFootBallInteraction` / `ensurePhysicalBallInteractor` 已开 |
| Quest Link 里材质发紫 | PC Play Mode 已知现象 | 可忽略——APK 实测资源加载正常（文档已记录） |
| 装上启动黑屏 | XR Plug-in 没启用 | ProjectSettings → XR Plug-in Management → Android → 勾 Oculus |

---

## 输出

1. 一句话结论：最可能的根因是什么。
2. 支撑证据：引用到的具体日志行 / 场景事实。
3. 下一步：要么给出代码改动方向（指到具体文件:符号），要么让用户做某个真机/Editor 验证动作。
4. 排查中如果发现的是文档 Known Issues 里**没记**的新问题，提示用户跑 [design-doc-sync](../design-doc-sync/SKILL.md) 把它记进去。
