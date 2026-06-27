---
name: ponytail-audit
description: >
  Whole-repo audit for over-engineering. Like ponytail-review, but scans the
  entire codebase instead of a diff: a ranked list of what to delete, simplify,
  or replace with stdlib/native equivalents. Use when the user says "audit this
  codebase", "audit for over-engineering", "what can I delete from this repo",
  "find bloat", "ponytail-audit", or "/ponytail-audit". One-shot report, does
  not apply fixes.
---

ponytail-review, repo-wide. Scan the whole tree instead of a diff. Rank
findings biggest cut first.

## Tags

Same as ponytail-review:

- `delete:` dead code, unused flexibility, speculative feature. Replacement: nothing.
- `stdlib:` hand-rolled thing the standard library ships. Name the function.
- `native:` dependency or code doing what the platform already does. Name the feature.
- `yagni:` abstraction with one implementation, config nobody sets, layer with one caller.
- `shrink:` same logic, fewer lines. Show the shorter form.

## Hunt

Deps the stdlib or platform already ships, single-implementation interfaces,
factories with one product, wrappers that only delegate, files exporting one
thing, dead flags and config, hand-rolled stdlib.

## SoccerBot Hunt

Prioritize the active Unity prototype surface:

- `unity/Assets/Scripts/**`: duplicated flow/controllers/presenters/builders,
  single-use wrappers, and test-mode code still wired into production paths.
- `docs/**` and root planning files: duplicated status lists that should live
  only in `docs/PROJECT_STATUS.md` plus the short `PLAN.md` entry point.
- `skills/**`: overlapping project skills where one command can delegate to
  another instead of restating the same process.
- `robot/**`: abstractions around real robot integration that are not exercised
  while the current demo path uses fake/local data.

Protect these from deletion-only audits unless evidence shows they are dead:

- Quest tuning/calibration knobs, collision parameters, and physical touch
  test paths.
- Diagnostic logs consumed by `vr-diagnose` and `quest-build`.
- Fallbacks that let PC Editor, Quest Link, and APK each run with missing
  hardware or offline local services.

## Output

One line per finding, ranked: `<tag> <what to cut>. <replacement>. [path]`.
End with `net: -<N> lines, -<M> deps possible.` Nothing to cut: `Lean already. Ship.`

## Boundaries

Scope: over-engineering and complexity only. Correctness bugs, security holes,
and performance are explicitly out of scope. Route them to a normal review
pass. Lists findings, applies nothing. One-shot.
"stop ponytail-audit" or "normal mode" to revert.
