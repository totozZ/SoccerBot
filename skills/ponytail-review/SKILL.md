---
name: ponytail-review
description: >
  Code review focused exclusively on over-engineering. Finds what to delete:
  reinvented standard library, unneeded dependencies, speculative abstractions,
  dead flexibility. One line per finding: location, what to cut, what replaces
  it. Use when the user says "review for over-engineering", "what can we
  delete", "is this over-engineered", "simplify review", or invokes
  /ponytail-review. Complements correctness-focused review, this one only
  hunts complexity.
---

Review diffs for unnecessary complexity. One line per finding: location, what
to cut, what replaces it. The diff's best outcome is getting shorter.

## Format

`L<line>: <tag> <what>. <replacement>.`, or `<file>:L<line>: ...` for
multi-file diffs.

Tags:

- `delete:` dead code, unused flexibility, speculative feature. Replacement: nothing.
- `stdlib:` hand-rolled thing the standard library ships. Name the function.
- `native:` dependency or code doing what the platform already does. Name the feature.
- `yagni:` abstraction with one implementation, config nobody sets, layer with one caller.
- `shrink:` same logic, fewer lines. Show the shorter form.

## SoccerBot Review Focus

When reviewing this repo, hunt for project-specific over-engineering:

- New Unity managers that duplicate `GameplayModeBootstrap`,
  `ArenaAttackController`, `MatchFlowController`, or `ScenarioPlayer`.
- New input layers that bypass existing PC/Gamepad/XR control profiles.
- UI frameworks, custom widgets, or new canvases where existing UGUI/TextMesh
  Pro panels cover the need.
- One-off config assets for values already exposed on
  `FootBallTuningController`, `QuestControllerLegRig`, or serialized fields.
- New robot/network abstractions while the active path is still
  `FakeDataGenerator` plus deferred NetworkTables/RoboRIO integration.
- Phase spillover: code for the next design-doc phase added during the current
  phase.

Do not flag these as bloat unless they are actually unused:

- Quest hardware calibration fields and tuning UI.
- `[TrackedLeg]`, `[MatchFlow]`, `[BuildAndroid]` logs used by diagnosis.
- One focused EditMode test for changed gameplay math or state transitions.
- Runtime fallbacks that keep the APK playable when the AI coach service,
  real robot, or Quest-specific feature is offline.

## Examples

❌ "This EmailValidator class might be more complex than necessary, have you
considered whether all these validation rules are needed at this stage?"

✅ `L12-38: stdlib: 27-line validator class. "@" in email, 1 line, real validation is the confirmation mail.`

✅ `L4: native: moment.js imported for one format call. Intl.DateTimeFormat, 0 deps.`

✅ `repo.py:L88: yagni: AbstractRepository with one implementation. Inline it until a second one exists.`

✅ `L52-71: delete: retry wrapper around an idempotent local call. Nothing replaces it.`

✅ `L30-44: shrink: manual loop builds dict. dict(zip(keys, values)), 1 line.`

## Scoring

End with the only metric that matters: `net: -<N> lines possible.`

If there is nothing to cut, say `Lean already. Ship.` and stop.

## Boundaries

Scope: over-engineering and complexity only. Correctness bugs, security holes,
and performance are explicitly out of scope. Route them to a normal review
pass, not this one. A single smoke test or `assert`-based
self-check is the ponytail minimum, not bloat, never flag it for deletion.
Does not apply the fixes, only lists them.
"stop ponytail-review" or "normal mode": revert to verbose review style.
