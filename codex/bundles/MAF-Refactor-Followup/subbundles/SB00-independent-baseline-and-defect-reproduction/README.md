# SB00: Independent baseline, current-head validation, and failing-first defect reproduction

## Metadata

- Phase: A — Evidence and characterization
- Depends on: None
- Checkpoint: No
- Optional: No
- Baseline: `fyziktom/CanDoItAll` `maf-refactor` @ `9e47a332fa9d329422ff616a0e0b6a97a22933c9`
- Primary executor: Claude Code / Claude Fable 5
- Reasoning profile: deepest available (`xHigh` intent)
- Compatible executor: Codex or another high-capability coding model using the included prompt
- Review findings addressed: `FR-014`

## Goal

Independently establish the maf-refactor branch state and reproduce every merge-blocking review finding before production fixes begin.

## Required context

1. Read `../../00-READ-ME-FIRST.md`, `../../01-REVIEW-VERDICT.md`, `../../02-FINDINGS-REGISTER.md`, and `../../03-EXECUTION-ORDER.md`.
2. Read the architecture and plan documents relevant to this subbundle.
3. Read `../../sharedinfo/required-skills.md` and use installed skills.
4. Confirm every dependency is explicitly unlocked.
5. Confirm current branch/HEAD and refresh the evidence map if it differs from the baseline.
6. Create `proof/proof-manifest.json` and `proof/SESSION-HANDOFF.md` before risky edits.

## Detailed tasks

1. Verify the exact branch head and compare it with development; stop if HEAD differs from the bundle baseline until the evidence map is refreshed.
2. Build CanDoItAll.slnx in Release and run Unit, Components, and Integration projects without expanding any accepted-failure list.
3. Create a focused CodeAnalytics snapshot covering AgentFramework Core, Runtime.Abstractions, MAF, LLM, Workflows, Modules.AgentFramework, Modules.Processes, Workbench, Security, and tests.
4. Add failing characterization tests for FR-001 through FR-006: authority permissions not consumed, unknown-source scope grant, project-turn recovery using base scope, script inspection using base scope, envelope-wrapped conversationId, and fingerprint-policy mismatch.
5. Add lifetime probes that prove how many LocalWorkspaceProcessHost and WorkspaceRuntimeServices instances are created/disposed per profile workspace.
6. Reproduce the explicit project-lease test conflict and record which production purpose each test is actually modeling.
7. Produce a baseline test/failure inventory with each failure categorized as pre-existing, refactor regression, environment-only, or unresolved.

## C# architecture impact

This is architecture-sensitive work. Map current owner, target owner, contracts, implementations, composition root, dependency direction, test seam, and old behavior to delete before editing. New interfaces without production consumption do not count as completion.

## Testability contract

- Release solution build
- Unit suite
- Components suite
- Integration suite
- new failing-first authority/scope/state/lifetime tests
- architecture guard scripts

Every behavior change requires at least one negative test. Extracted behavior must be directly testable without constructing the original broad runtime/workspace graph unless the test is explicitly an integration smoke.

## Constraints

- Keep source-code comments and identifiers in English.
- Do not add partial-class architecture, nested architecture owners, broad Helpers/Managers, or a Common dumping ground.
- Do not let UI observation, route, prompt text, payload JSON, or current navigation grant authority.
- Do not recapture current UI context or authority during approval continuation.
- Do not duplicate provider, tool, process, or persistence side effects for comparison.
- Do not restore product/process semantics or product module references to MAF.
- Do not make lightweight LLM calls use the full agent runtime.
- Do not add new accepted test failures or exclusions.
- Do not commit, push, or open a PR unless explicitly requested.

## High-risk points

- Do not modify production behavior while producing the baseline.
- Do not accept closure-audit claims without rerunning the current HEAD.
- Do not convert new failures into exclusions.

## Required architecture proof

- before/after responsibility and dependency map
- CodeAnalytics snapshot/dependency/cycle evidence when available
- changed files/projects
- failing-first and negative tests
- focused build/test transcript
- source assertions proving the old production path no longer grants/owns the responsibility
- rollback and retained compatibility readers/selectors
- completed proof manifest and durable session handoff

## Acceptance criteria

- The goal is satisfied in the production call chain, not only in new contracts/tests.
- Authority, scope, state, approval, and process invariants remain fail-closed.
- No new reverse references, hidden service location, partial-class growth, or duplicate side-effecting paths exist.
- Focused tests, architecture guards, and all checkpoint-required validation pass.

## Stop and repair conditions

Mark this subbundle Blocked if the design requires UI-derived authority, current-context recapture, mixed workspace bundles, silent state replay, process logic in MAF, a project-reference cycle, or a test exclusion. Continue other safe in-scope work and record exact evidence; do not weaken the invariant.

## Downstream unlock

SB01, SB04, and SB08 may start only after the failing tests and baseline evidence are committed or durably recorded.

## Required closure output

- Status: Completed | Blocked | Completed with bounded non-blocking follow-up
- Start/result commit and changed files/projects
- Implemented boundary changes
- Build/test/guard commands and results
- Failing-first and negative proof
- Architecture/dependency/source-of-truth proof
- Bugs found and owner-boundary fixes
- Compatibility/rollback state
- Downstream unlock decision
- Path to updated `proof/SESSION-HANDOFF.md`
