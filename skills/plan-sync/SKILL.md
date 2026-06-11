---
name: plan-sync
description: 扫描代码 + git log + 当前文件状态，自动更新 PLAN.md 的 TODO 勾选、进度总览表和「立即下一步」段落。每个开发节点结束（完成 P5/P6/P7…）后跑一次，省得手动维护文档。
user-invocable: true
allowed-tools:
  - Read
  - Edit
  - Glob
  - Grep
  - Bash
---

# /plan-sync — 同步 PLAN.md 与真实代码状态

SoccerBot 项目专用。读真实状态 → 改 [PLAN.md](PLAN.md) → 顺手把 [README.md](README.md) 的「当前状态」一行也更新掉。

参数：`$ARGUMENTS`（一般为空；可选 `--dry-run` 只输出 diff 不改文件）

---

## 收集真实状态（并行）

1. **git log**：`git log --oneline -20`，拿最近 20 条 commit。注意中文 commit message。
2. **git status**：当前工作区是否有未提交改动。
3. **关键脚本是否存在**（用 Glob，每个独立判断「✅ 已完成 / ❌ 未做」）：
   - `unity/Assets/Scripts/Scenario/Scenario.cs`
   - `unity/Assets/Scripts/Scenario/ScenarioPlayer.cs`
   - `unity/Assets/Scripts/Scenario/ScenarioTrigger.cs`
   - `unity/Assets/Scripts/UI/ScorePanel.cs`
   - `unity/Assets/Scripts/XR/XRSetup.cs`（或任何 `XR/` 下的脚本）
   - `unity/Assets/Editor/BuildAndroid.cs`（quest-build skill 留下的）
4. **场景**：`unity/Assets/Scenes/Main.unity` 是否存在。
5. **剧本资产**：`unity/Assets/Scenarios/*.asset` 数量（应该 ≥ 3）。
6. **Quest 部署痕迹**：
   - `unity/ProjectSettings/ProjectSettings.asset` 里 `targetGroup: 7`（Android）下的 XR 配置
   - `build/SoccerBot.apk` 是否存在 + 修改时间

---

## 推断 Phase 完成状态

按 [PLAN.md](PLAN.md) 的 P1–P9 逐个判定：

| Phase | 判定规则 |
|---|---|
| P1 重建 Main.unity | `Scenes/Main.unity` 存在 |
| P2 队友/对手 | `Main.unity` grep `Teammate` 和 `Opponent` 都命中 |
| P3 剧本系统代码 | Scenario.cs / Player.cs / Trigger.cs 三个都在 |
| P4 剧本资产 + 触发 | `Scenarios/*.asset` ≥ 3 |
| P5 评分 UI | `UI/ScorePanel.cs` 存在 |
| P5.1 球起点跟随机器人 | grep `ScenarioPlayer.cs` 是否引用 `transform.position` 做平移偏移；不确定就标 `?` |
| P6 XR Origin | `XR/` 目录有脚本 **或** Main.unity grep `XROrigin` 命中 |
| P7 APK 构建 | `build/SoccerBot.apk` 存在 |
| P8 性能优化 | git log 里出现「性能 / perf / fps / Quest 优化」关键词 |
| P9 演示视频 | git log 里出现「视频 / video / demo」关键词；不确定标 `?` |

判定不确定时**标 `?` 而不是猜**。然后口头问用户该项是不是已完成，再写。

---

## 改文件

### PLAN.md 三处必改

1. **顶部 TODO 列表**（第 5–14 行）：把判定为完成的换成 `[x]`，未完成保持 `[ ]`。
2. **「当前进度总览」表格**：状态列改 ✅ / ⬜ / ⚠️。
3. **「立即下一步」段落**：删旧内容，写当前最靠前的未完成 Phase 是什么、卡在哪、下一动作是什么。简短 3–5 行。

### PLAN.md 顶部版本号 + 日期

第 18 行那种 `> 版本: vX.Y | 日期: YYYY-MM-DD | 状态: ...`：
- 日期换成今天（从环境里的当前日期，不是猜）
- 版本号小步递增（v4.1 → v4.2）
- 状态用一句话总结

### README.md 一行

倒数第二段「当前状态（YYYY-MM-DD）」那一行同步更新。日期 + 一句话。其它别动。

---

## 输出

改完后：
1. `git diff PLAN.md README.md` 给用户看一眼。
2. **不要自动 commit**。让用户自己决定。
3. 如果有 `?` 标记的项，列出来问用户。

`--dry-run` 模式：不改文件，只输出会改成什么样的摘要（哪些 phase 状态翻了、版本号换成什么、立即下一步段落新写什么）。

---

## 不要做

- 不要发明 Phase。PLAN.md 已有的 P1–P9 是固定的，新增需求让用户自己加。
- 不要重写「演示故事板」「锁定的方向决策」「数据流」「未来展望」这些静态段落——它们与代码状态无关。
- 不要碰 [memory/](memory/) 里的文件，那是 Claude 的长期记忆，归 auto-memory 管。
- 没有真实证据就别勾 `[x]`。宁可标 `?` 问一句。
