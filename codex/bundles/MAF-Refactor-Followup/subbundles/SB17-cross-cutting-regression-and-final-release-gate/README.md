# SB17: Cross-cutting fault injection, regression repair, and final merge gate

## Metadata

- Phase: H — Release validation
- Depends on: `SB10`, `SB13`, `SB14`, `SB16`
- Checkpoint: Yes
- Optional: No
- Baseline: `fyziktom/CanDoItAll` `maf-refactor` @ `9e47a332fa9d329422ff616a0e0b6a97a22933c9`
- Primary executor: Claude Code / Claude Fable 5
- Reasoning profile: deepest available (`xHigh` intent)
- Compatible executor: Codex or another high-capability coding model using the included prompt
- Review findings addressed: `FR-014`

## Goal

Prove the corrected architecture through independent builds, tests, fault injection, live scenarios, and a strict no-known-regression merge decision.

## Required context

1. Read `../../00-READ-ME-FIRST.md`, `../../01-REVIEW-VERDICT.md`, `../../02-FINDINGS-REGISTER.md`, and `../../03-EXECUTION-ORDER.md`.
2. Read the architecture and plan documents relevant to this subbundle.
3. Read `../../sharedinfo/required-skills.md` and use installed skills.
4. Confirm every dependency is explicitly unlocked.
5. Confirm current branch/HEAD and refresh the evidence map if it differs from the baseline.
6. Create `proof/proof-manifest.json` and `proof/SESSION-HANDOFF.md` before risky edits.

## Detailed tasks

1. Rebase or merge the latest development into maf-refactor only through an explicit operator decision, then refresh the CodeAnalytics snapshot and review the new diff.
2. Run Release build and all Unit, Components, and Integration tests with zero newly accepted failures. Resolve the explicit lease-token conflict rather than allow-listing it.
3. Exercise floating chat Canvas -> Gantt, Project X -> Y, detached/follow mode, multiple chats, rapid navigation, profile switch, send during loading, and approval while viewing a different project.
4. Exercise provider/model/history/tool/policy state migrations, restart/resume, stale/tampered authority, and abandoned waiting-run reconciliation.
5. Exercise process recovery with exact run scope and every ordinary completion gate exactly once. Do not perform uncontrolled production-like external mutations.
6. Exercise lightweight workflow LLM across fake OpenAI/Azure/Ollama drivers, empty response retry, JSON schema failure, timeout, and sanitized error.
7. Run architecture guards, dependency/cycle checks, sensitive public projection review, and changed-file ownership audit.
8. For each defect found, add a failing regression test before the smallest owner-boundary fix; update the bugfix register and durable session handoff.
9. Produce final status: Ready to merge, Blocked, or Ready with only explicitly named non-merge-blocking follow-up. A known authority/scope/state/approval failure always blocks.

## C# architecture impact

This is architecture-sensitive work. Map current owner, target owner, contracts, implementations, composition root, dependency direction, test seam, and old behavior to delete before editing. New interfaces without production consumption do not count as completion.

## Testability contract

- full build and all test projects
- fault-injection matrix
- live floating chat
- workflow LLM matrix
- process recovery
- architecture/dependency guards

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

- Do not expand failure allow-lists.
- Do not make tests green by widening authority.
- Do not run dual side-effecting paths for comparison.
- Do not merge without CI or equivalent durable transcript.

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

Final merge decision only.

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
