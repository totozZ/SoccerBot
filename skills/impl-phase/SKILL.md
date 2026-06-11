---
name: impl-phase
description: 按设计文档实现指定 Phase。输入「文档 + 阶段号」（如 leg P3），读出该阶段目标 / Proposed Components / Existing Code Impact / 验收标准，照文档描述的结构实现（不自创架构），完成后逐条对照「验收」自检并报告还需哪些真机验证。把分阶段开发流程固化下来。
user-invocable: true
allowed-tools:
  - Read
  - Write
  - Edit
  - Glob
  - Grep
  - Bash
---

# /impl-phase — 按设计文档落地一个 Phase

SoccerBot 项目专用。你的设计文档都是 `Phase 1 → 4` + 每阶段「验收」的结构，这个 skill 把"读阶段 → 照着实现 → 对验收自检"这套流程固定下来。

参数：`$ARGUMENTS`，形如 `<文档> <Phase>`：
- `leg P3` / `leg-interaction Phase 3` → `docs/QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md` 的 Phase 3
- `demo-feedback P0` → `docs/CORE_GAMEPLAY_REWORK_V1_2_DEMO_FEEDBACK.md` 的 P0
- 文档简称同 [design-doc-sync](../design-doc-sync/SKILL.md)。

---

## 1. 读懂这个 Phase（先读，别急着写）

从目标文档里抽取，缺哪块就回到文档找，**不要凭空补**：

1. **该 Phase 的「目标」和「验收」**：这是成败判据，先记下来。
2. **`## Proposed Components` / `### Runtime Components`**：要新建/改的组件、它们的职责和主要字段。文档怎么定义就怎么建。
3. **`## Existing Code Impact` / `### Existing Code Changes`**：会牵动哪些现有脚本（如 `FPSPlayerController`、`MatchFlowController`、`BallController`），以及文档要求的接入方式（如新增 `HandleFootBallContact(FootContactData)`）。
4. **`## Open Questions` 里已有的 `A:` 决策**：这些是拍过板的方向（如"两个手柄都做成腿""球完全物理化""offset `(0,-0.15,0.1)`、scale `0.5`"），实现必须遵守，不要重开。

把组件名映射到真实文件：先 Glob `unity/Assets/Scripts/**/<组件名>.cs` 看是新建还是改已有。

---

## 2. 实现

原则：

- **照文档的架构走**，不自创新设计。文档说 `Update` 缓存 pose、`FixedUpdate` 用 `MovePosition`/`MoveRotation`，就这么写；文档给了字段清单，就按那个清单。
- 跟着现有代码风格（中文注释密度、命名、`[TrackedLeg]`/`[MatchFlow]` 日志 tag 习惯）。
- Unity C# 改动：新建 `.cs` 后 Unity 会自动生成 `.meta`，别手写 `.meta`；但要留意改名/移动脚本会让旧 `.meta` 引用失效。
- 涉及运行时安全：保持文档约定的默认值（如脚交互默认 `enableFootBallInteraction = false`，先 visual-only，不擅自打开影响现有 MatchFlow/球流程）。
- 编译验证：改完让用户在 Unity 里看 Console 是否编译通过，或如果有命令行编译路径就跑一遍。**不要假装编译过了**。

---

## 3. 对验收自检

逐条列出该 Phase 文档里的「验收」条目，每条标注：

- ✅ **代码已满足**：指出由哪个文件/逻辑保证。
- 🎮 **需真机/Quest Link 验证**：VR 体感类验收（"脚位置跟手感强""快速挥动不漏碰撞""转身不漂移"）代码层面给不了结论，**老实说明这条要上头显测**，并建议用 [quest-build](../quest-build/SKILL.md) 部署、用 [vr-diagnose](../vr-diagnose/SKILL.md) 排查。
- ❌ **本轮没做到**：说明缺口和原因，别勾。

不要把"代码写了"等同于"验收过了"。VR 项目大量验收只能真机确认。

---

## 4. 收尾

1. `git diff` 给用户看改动。
2. **不自动 commit。**
3. 提示用户跑 [design-doc-sync](../design-doc-sync/SKILL.md) `leg`（或对应文档），把这一轮的 Implementation Notes / 新 Known Issues 回填进文档。
4. 如果实现过程中发现文档里的设计与现状有矛盾（例如某 `Open Questions` 的决策已不适用），**停下来问用户**，不要擅自改设计方向。

---

## 不要做

- 不跳 Phase。要求做 P3 就只做 P3，不顺手把 P4 的物理射门也写了——除非用户明说。
- 不删 `FirstPersonLegAvatar` 这类"文档说待确认后移除"的东西，除非该 Phase 明确要求且用户确认。
- 不动设计意图段落和 `Open Questions` 的既有决策。
