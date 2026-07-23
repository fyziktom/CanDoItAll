# Architecture Checkpoints

| Checkpoint | Timing | Required evidence | Unlock / invalidation |
| --- | --- | --- | --- |
| `AC00` | Before SB01 | Boundary map, pattern records, no-new-reference plan, CodeAnalytics gap recorded | Unlocks SB01. |
| `AC01` | SB01 closure | Before/after dependency direction, direct query/cache/runner tests, source assertion broad paths are not called, no new partial/reference or service location outside the lifetime adapter, immutable hard-five proof, Home-independent tests | Unlocks SB02; failure invalidates all downstream data proof. |
| `AC02` | SB02 closure | Home dependency/line-count responsibility review, AppComponents ownership, component behavioral tests, no page-local duplicate wrapper | Unlocks SB03 browser closure; failure invalidates UI proof. |
| `AC03` | SB03 closure | CodeAnalytics retry or documented manual graph/cycle evidence, `.csproj` negative diff, solution build, targeted tests, browser proof, architecture review decision | Unlocks final bundle closure only. |

## Review Questions

- Dependency graph: did any contract reference an implementation or any new project edge appear?
- Partial policy: were any new partial/nested dashboard types added?
- Testability: can every cache/query policy be tested without Home/full runtime/service provider, with provider resolution isolated and directly tested only in the lifetime runner?
- Old-class shrink: did Home lose broad workbench/project behavior, and did existing large services avoid gaining dashboard responsibility?
- Downstream decision: is the exact progression gate passed, or must the owning subbundle reopen?

## Current Decisions

- `AC00`: `Approved` during preparation.
- `AC01`: `Approved` on 2026-07-22 after the final targeted foundation gate: Web Release build 0 errors; dashboard query tests 28/28; cache/loader/runner component tests 18/18; branch-local workflow correction followed by AgentFramework Release build 0 errors and focused workflow activity tests 13/13. SB02 is unlocked.
- `AC02`: `Approved` on 2026-07-22 after 7/7 Home/QuickActionCard Behavioral tests and repeated independent review of exact labels, deterministic timer behavior, Home responsibility, and AppComponents ownership.
- `AC03`: `Approved` on 2026-07-22 after the two-pass performance audit, manual no-reference/dependency proof, Release solution build, 53 focused dashboard tests, migration-model check, and inspected Playwright evidence. CodeAnalytics remained unavailable and is recorded as a tooling gap rather than a false clean result.
