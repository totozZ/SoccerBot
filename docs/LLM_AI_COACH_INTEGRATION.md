# LLM AI Coach 集成文档

更新时间：2026-06-18

## 目标

LLM AI Coach 的目标不是替代比赛逻辑，而是在每轮训练结束后，用低频、可解释、可离线降级的方式给玩家一段教练反馈：

1. Unity 记录一轮训练过程。
2. 生成稳定的 `TrainingSummaryJson`。
3. 通过 HTTP POST 发给本地 LLM 服务。
4. LLM 服务返回结构化反馈。
5. `ScorePanel` 显示总结、主要问题、建议和下一轮训练方向。

当前设计刻意把 LLM 放在 Unity 外部：Unity 不直接持有模型 API key，不处理流式输出，不把比赛流程阻塞在模型推理上。模型服务可以是本地规则 mock、本地小模型、云端 LLM 代理，或后续接入的正式教练服务。

## 当前实现状态

| 模块 | 文件 | 状态 | 职责 |
|---|---|---|---|
| 回合记录 | `unity/Assets/Scripts/AICoach/GameEventRecorder.cs` | 已接入 MVP | 订阅 `MatchFlowController` 和物理触球事件，记录回合数据 |
| 请求客户端 | `unity/Assets/Scripts/AICoach/AICoachClient.cs` | 已接入 MVP | POST `TrainingSummaryJson` 到本地 `/analyze`，解析响应或 fallback |
| JSON 数据结构 | `unity/Assets/Scripts/AICoach/TrainingSummaryJson.cs` | 已接入 MVP | 定义请求、响应和下一轮配置结构 |
| UI 展示 | `unity/Assets/Scripts/UI/ScorePanel.cs` | 已接入 MVP | 在结算面板显示 AI Coach Feedback |
| 自动挂载 | `unity/Assets/Scripts/Flow/MatchFlowController.cs` | 已接入 MVP | 运行时确保存在 `AICoachClient` 和 `GameEventRecorder` |

默认端点：

```text
POST http://localhost:8000/analyze
Content-Type: application/json
Accept: application/json
Timeout: 3s
```

如果服务离线、超时、返回空内容或 JSON 无法解析，Unity 会显示本地 fallback 建议，不阻塞训练流程。

## 运行时链路

```text
MatchFlowController
  ├─ PhaseChanged / PassStarted / ReceiveResolved / Recovery... / ShotAttempted / FootContactRecorded / RoundResolved
  ↓
GameEventRecorder
  ├─ 记录 pass / receive / recovery / shot / foot contact / phase transitions
  ├─ RoundResolved 时 BuildSummary()
  ↓
TrainingSummaryJson
  ├─ ToJson()
  ↓
AICoachClient
  ├─ POST /analyze
  ├─ ParseResponse()
  └─ Fallback()
  ↓
ScorePanel
  └─ ShowAICoachFeedback()
```

## 请求 JSON

Unity 会发送 `TrainingSummaryJson`。字段名必须保持和 C# 类一致，因为当前使用 `JsonUtility`。

示例：

```json
{
  "schemaVersion": "1.0",
  "project": "SoccerBot",
  "roundId": "4de67f3c7cb6407ca568860b751b43ab",
  "startedAtUtc": "2026-06-18T02:30:15.1200000Z",
  "endedAtUtc": "2026-06-18T02:30:23.4800000Z",
  "durationSeconds": 8.36,
  "dataSource": "FakeDataGenerator",
  "currentScenarioName": "Physical Result",
  "shotResult": "SAVED",
  "finalScore": 50,
  "grade": "C",
  "passDirection": { "x": 0.0, "y": 0.0, "z": 1.0 },
  "passDistance": 5.4,
  "estimatedPassBallSpeed": 3.375,
  "ballSpeedAtResult": 2.1,
  "receiveTimingError": 0.18,
  "receiveQuality": 0.62,
  "receiveByFootContact": true,
  "footVelocityAtTouch": 1.85,
  "footContactPower": 0.46,
  "footContactAccuracy": 0.71,
  "footContactZone": "InstepZone",
  "recoveryTriggered": false,
  "recoverySucceeded": false,
  "shotPower": 0.58,
  "shotDirection": { "x": 0.12, "y": 0.08, "z": 0.99 },
  "phaseTransitions": [
    "12.31:Setup",
    "14.34:Pass",
    "16.00:Possession",
    "20.62:Shot",
    "22.18:Score"
  ]
}
```

字段解释：

| 字段 | 含义 | LLM 使用建议 |
|---|---|---|
| `shotResult` | 本轮结算结果，例如 `GOAL` / `SAVED` / `INTERCEPTED` / `OUT OF BOUNDS` | 作为反馈第一句的上下文 |
| `finalScore` / `grade` | 当前评分 | 只做强弱判断，不要过度解释 |
| `receiveTimingError` | 接球时机误差，越小越好 | 用于判断“早/晚接球” |
| `receiveQuality` | First Touch 质量，0-1 | 教练反馈核心字段 |
| `receiveByFootContact` | 是否由脚部物理触球完成 | 区分按钮接球和真实触球 |
| `footVelocityAtTouch` | 触球瞬间脚速 | 判断是否过猛/过轻 |
| `footContactPower` | 归一化触球力量 | 判断射门/传球力度 |
| `footContactAccuracy` | 归一化触球方向质量 | 判断脚面方向/触球角度 |
| `footContactZone` | 触球区域，例如 `ToeZone` / `InstepZone` / `SoleZone` / `FootBoxProximity` | 判断脚尖捅、脚背抽、鞋底停等 |
| `recoveryTriggered` | 是否进入反抢 | 如果为 true，说明 first touch 带来压力 |
| `recoverySucceeded` | 反抢是否成功 | 用于区分“补救成功”还是“失误扩大” |
| `shotPower` | 射门或传球意图力量 | 判断最终动作是否过轻/过猛 |
| `phaseTransitions` | 阶段流转记录 | 调试用，不建议逐条念给玩家 |

## 响应 JSON

本地服务必须返回 `AICoachFeedbackResponse` 兼容结构。

最小可用响应：

```json
{
  "summary": "You controlled the first touch well, but the shot was easy for the goalkeeper to read.",
  "mainProblem": "The strike had enough power, but the foot angle sent the ball too close to the goalkeeper lane.",
  "advice": "Keep the first touch calm, then rotate the instep slightly across the ball before striking forward.",
  "nextDrillSuggestion": "Repeat a medium straight pass and aim one meter wider of the keeper.",
  "nextScenarioConfig": {
    "difficulty": "same",
    "passType": "straight",
    "passDirection": "center",
    "ballSpeed": 1.0,
    "defenderPressure": 0.35,
    "receiveWindow": 0.4,
    "shotWindow": 1.5,
    "successCondition": "clean first touch and shot on target"
  }
}
```

响应字段：

| 字段 | 必需 | 限制 / 当前行为 |
|---|---|---|
| `summary` | 推荐 | 空值会 fallback；显示时建议一句话 |
| `mainProblem` | 推荐 | 空值会 fallback；建议指出一个主要问题 |
| `advice` | 推荐 | 空值会 fallback；建议给一个可执行动作 |
| `nextDrillSuggestion` | 推荐 | 空值会 fallback；建议描述下一轮训练 |
| `nextScenarioConfig` | 可选 | 当前只会校验和保存到 `LastValidatedNextScenarioConfig`，还未驱动真实场景生成 |

Unity 侧会执行校验：

- 文本为空会替换为默认 fallback。
- `summary` / `mainProblem` / `advice` / `nextDrillSuggestion` 超过 220 字符会截断。
- `difficulty` 只接受 `easier` / `same` / `harder`。
- `passType` 只接受 `straight` / `diagonal` / `lofted` / `ground`。
- `passDirection` 只接受 `left` / `center` / `right`。
- `ballSpeed` 限制在 `0.2-2.5`。
- `defenderPressure` 限制在 `0-1`。
- `receiveWindow` 限制在 `0.15-1.2`。
- `shotWindow` 限制在 `0.5-5`。
- `successCondition` 为空会 fallback，超过 160 字符会截断。

## 本地服务最小示例

用于先联通 Unity，不依赖真实 LLM。

```python
from fastapi import FastAPI
from pydantic import BaseModel
import uvicorn

app = FastAPI()

class TrainingSummary(BaseModel):
    shotResult: str | None = None
    receiveQuality: float = 0
    receiveTimingError: float = 1
    footVelocityAtTouch: float = 0
    footContactZone: str | None = None
    recoveryTriggered: bool = False

@app.post("/analyze")
def analyze(summary: TrainingSummary):
    quality = summary.receiveQuality
    zone = summary.footContactZone or "unknown contact"

    if summary.recoveryTriggered or quality < 0.4:
        problem = "The first touch created pressure before the shot."
        advice = "Meet the ball earlier, keep the foot quieter, and face the incoming pass."
        next_drill = "Repeat a slower straight pass and require a clean first touch before shooting."
    elif summary.footVelocityAtTouch > 2.5:
        problem = "The foot swing was too aggressive for the receive."
        advice = "Use a softer first touch, then strike after the ball settles."
        next_drill = "Run the same pass with a lower swing speed target."
    else:
        problem = "The main action was stable; improve shot placement next."
        advice = f"Keep using the {zone}, then aim slightly wider of the goalkeeper."
        next_drill = "Repeat the same drill and aim for a cleaner instep shot."

    return {
        "summary": f"{summary.shotResult or 'Round'} recorded. First touch quality {quality:.2f}.",
        "mainProblem": problem,
        "advice": advice,
        "nextDrillSuggestion": next_drill,
        "nextScenarioConfig": {
            "difficulty": "same",
            "passType": "straight",
            "passDirection": "center",
            "ballSpeed": 1.0,
            "defenderPressure": 0.35,
            "receiveWindow": 0.4,
            "shotWindow": 1.5,
            "successCondition": "clean first touch and shot on target"
        }
    }

if __name__ == "__main__":
    uvicorn.run(app, host="127.0.0.1", port=8000)
```

运行：

```powershell
pip install fastapi uvicorn pydantic
python coach_server.py
```

手动测试：

```powershell
curl.exe -X POST http://127.0.0.1:8000/analyze `
  -H "Content-Type: application/json" `
  -d "{\"shotResult\":\"SAVED\",\"receiveQuality\":0.62,\"footVelocityAtTouch\":1.8,\"footContactZone\":\"InstepZone\"}"
```

注意：

- Unity 默认端点是 `http://localhost:8000/analyze`。
- 如果 Windows / Unity 对 `localhost` 解析异常，可以在 Inspector 把 `AICoachClient._analyzeEndpoint` 改成 `http://127.0.0.1:8000/analyze`。
- Quest APK 真机不能访问 PC 的 `localhost`。真机联调时需要改成 PC 局域网 IP，例如 `http://192.168.x.x:8000/analyze`，并确认防火墙放行。

## LLM 服务提示词建议

系统提示词建议：

```text
You are SoccerBot's concise VR football training coach.
You receive one JSON summary of a single training round.
Return only valid JSON matching the required response schema.
Do not include markdown.
Do not invent data that is not present.
Keep each feedback field short, specific, and actionable.
Focus on first touch quality, timing, foot contact zone, pressure/recovery, shot direction, and next drill.
```

用户消息建议：

```text
Analyze this SoccerBot training round.
Return JSON with:
- summary
- mainProblem
- advice
- nextDrillSuggestion
- nextScenarioConfig

Training summary:
{TrainingSummaryJson}
```

输出约束：

```json
{
  "summary": "one short sentence",
  "mainProblem": "one specific problem",
  "advice": "one actionable coaching cue",
  "nextDrillSuggestion": "one next repetition recommendation",
  "nextScenarioConfig": {
    "difficulty": "easier|same|harder",
    "passType": "straight|diagonal|lofted|ground",
    "passDirection": "left|center|right",
    "ballSpeed": 1.0,
    "defenderPressure": 0.35,
    "receiveWindow": 0.4,
    "shotWindow": 1.5,
    "successCondition": "short success condition"
  }
}
```

## 反馈策略

LLM 不应该评价所有字段。每轮只选一个主问题。

推荐优先级：

1. `recoveryTriggered == true`：优先反馈 first touch 造成压力。
2. `receiveQuality < 0.4`：优先反馈接球时机/方向。
3. `footVelocityAtTouch` 过高：反馈触球太猛。
4. `footContactZone == "SoleZone"` 且射门失败：反馈用鞋底停球后再射。
5. `footContactZone == "ToeZone"` 且 `footContactAccuracy` 低：反馈脚尖捅球方向不稳定。
6. `shotResult == "SAVED"`：反馈射门方向被门将读到。
7. `shotResult == "GOAL"` 或高分：强化一个做对的动作，再给下一轮更高要求。

## 验收清单

Editor / PC：

- 勾选 `GameEventRecorder._logSummaryJson` 后，回合结束 Console 能看到完整 `TrainingSummaryJson`。
- 本地服务在线时，Console 无 `[AICoach] Request failed`。
- ScorePanel 先显示 `Analyzing this round...`，随后显示服务返回反馈。
- 服务关闭时，ScorePanel 显示 `Offline coach...` fallback，不影响下一回合。
- 返回空 JSON、非法 JSON、字段为空时都有 fallback，不抛异常。

Quest / APK：

- 不使用 `localhost` 指向头显自身；改成 PC 局域网 IP 或部署到可访问服务器。
- 断网/服务离线时 UI 仍能结算。
- AI 文案不遮挡结果面板主要评分。
- 超时保持 3 秒左右，不拖慢训练节奏。

## 当前限制

- `nextScenarioConfig` 目前只被 `AICoachClient.LastValidatedNextScenarioConfig` 保存，尚未驱动下一轮真实场景参数。
- 当前使用 Unity `JsonUtility`，不适合字典、动态字段或复杂嵌套结构。
- 当前是单轮反馈，不保留跨轮长期记忆。
- 当前没有做认证、签名、速率限制或隐私过滤；只建议在本地/演示网络使用。
- 当前 ScorePanel 显示区域较短，反馈必须短句化。

## 后续开发建议

优先级从高到低：

1. 增加一个本地 mock server 脚本到 `tools/` 或 `docs/examples/`，让联调一键启动。
2. 把 `nextScenarioConfig` 接回 `MatchFlowController` 的下一轮参数，例如传球速度、接球窗口、对手压力。
3. 增加“连续 3 轮摘要”的短期记忆，让教练能指出趋势。
4. 增加响应版本号和 `schemaVersion` 对齐检查。
5. 增加 Unity PlayMode / EditMode 测试，覆盖 fallback、非法 JSON 和字段裁剪。
6. Quest 真机联调时，提供端点配置 UI 或 ScriptableObject 配置资产。
