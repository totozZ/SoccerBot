---
name: design-doc-sync
description: 读最近 git diff + 代码现状，自动回填某个设计文档的 Implementation Notes / Current Known Issues / Latest Fix Notes / Planned Next Changes 四段。对应文档里那句「实现功能和简易实现说明需要记录在此文档，以便于排错和后续开发」。每写完一段代码跑一次，省得手动维护。
user-invocable: true
allowed-tools:
  - Read
  - Edit
  - Glob
  - Grep
  - Bash
---

# /design-doc-sync — 把代码现状回填进设计文档

SoccerBot 项目专用。读真实改动 → 更新 `docs/` 下对应设计文档的「实现/已知问题/最近修复/下一步」四段 → 给 diff，不自动 commit。

这是配合 [impl-phase](../impl-phase/SKILL.md) 的下半场：写完代码后用它把"实际做了什么、还卡在哪"沉淀进文档，下次开工或排错时不用重新翻代码。

参数：`$ARGUMENTS`
- 第一个参数：目标文档（可省略，省略时自动按改动文件推断）。可写全路径或简称：
  - `leg` / `leg-interaction` → `docs/QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md`
  - `gameplay` → `docs/CORE_GAMEPLAY_REWORK.md`
  - `demo-feedback` → `docs/CORE_GAMEPLAY_REWORK_V1_2_DEMO_FEEDBACK.md`
  - `art` → `docs/DEMO_VISUAL_ART_OPTIMIZATION_PLAN.md`
- `--dry-run`：只输出会改成什么样的摘要，不动文件。

---

## 收集真实改动（并行）

1. **git diff**：`git diff HEAD` + `git diff --staged`，拿到本轮所有未提交改动。
2. **改了哪些脚本**：`git diff --name-only HEAD` 里 `unity/Assets/Scripts/**/*.cs` 的清单。
3. **最近 commit**：`git log --oneline -8`，注意中文 message，用来理解"这一轮在做什么"。
4. **改动脚本的关键内容**：对每个改动的 `.cs`，读 diff 区块，提炼"新增了什么字段/方法/行为"。不要逐行复述，提炼成人能看懂的一句话。

---

## 推断目标文档（没给参数时）

每个设计文档底部都有 `### Runtime Components` / `### Existing Code Changes`，里面列了它管辖的脚本路径。规则：

- 把改动脚本清单和各文档列出的脚本路径做交集，命中最多的就是目标文档。
- 例：改了 `Player/TrackedLegController.cs`、`Player/QuestControllerLegRig.cs`、`Ball/PhysicalBallInteractor.cs` → 命中 `QUEST_CONTROLLER_LEG_INTERACTION_DESIGN.md`。
- 交集为空或两个文档打平 → **不要猜**，把候选列给用户问一句再继续。

---

## 改文档（只动这四段）

定位目标文档里的这四个 H3，没有就在 `## Implementation Notes` 下补建：

| 段落 | 写什么 | 怎么写 |
|---|---|---|
| `### Current Runtime Calibration` 或 `### Runtime Components` | 本轮实现/改动的组件行为 | 在已有条目上增量更新，别整段重写。新字段、新默认值、新开关都补进去 |
| `### Current Known Issues` | 现存未解决问题 | 本轮**修复**的从这里删掉或移到 Latest Fix；新发现的加进去，编号顺延 |
| `### Latest Fix Notes` | 本轮修了什么 | 一条改动一行，写清"症状 → 根因 → 怎么修的"。从 git diff 提炼 |
| `### Planned Next Changes` | 下一步 | 删掉本轮已完成的项；补上 diff/commit 暗示的、但还没做的待办 |

写作要求（对齐文档现有风格）：
- 中英文混排照旧，技术名词（`Rigidbody`、`FixedUpdate`、handedness、impulse 等）保持原文。
- 引用代码用反引号包路径，如 `unity/Assets/Scripts/Player/TrackedLegController.cs`。
- 默认值/校准数字要准（如 `Leg Scale = 0.5`、offset `(0, -0.15, 0.1)`）——从代码里读真实值，不要照抄旧文档可能已过期的数。

---

## 绝不要碰的部分

- `## Goal` / `## Core Concept` / `## Physics Shape` / `## Interaction Model` / `## Phased Implementation` / `## Open Questions` 这些**设计意图**段落——它们是方向，不随代码变。
- `## Open Questions` 里用户手写的 `A:` 回答——那是决策记录，只读不改。
- `docs/images/` 和任何图片引用。
- 不碰 `memory/`、`PLAN.md`（那是 [plan-sync](../plan-sync/SKILL.md) 的活）。

---

## 输出

1. `git diff <目标文档>` 给用户看。
2. **不自动 commit**。
3. 如果本轮改动里有"代码做了但你不确定算不算解决了某个 Known Issue"的情况，把它单独列出来问用户，别擅自把问题标成已解决。

`--dry-run`：不写文件，只输出四段各自会怎么变的摘要。
