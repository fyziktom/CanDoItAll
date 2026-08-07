# SB04: Populate complete workspace execution identity and make root semantics cross-platform

## Metadata

- Phase: C — Scope and lifetime correctness
- Depends on: `SB03`
- Checkpoint: No
- Optional: No
- Baseline: `fyziktom/CanDoItAll` `maf-refactor` @ `9e47a332fa9d329422ff616a0e0b6a97a22933c9`
- Primary executor: Claude Code / Claude Fable 5
- Reasoning profile: deepest available (`xHigh` intent)
- Compatible executor: Codex or another high-capability coding model using the included prompt
- Review findings addressed: `FR-010`

## Goal

Make every workspace bundle identifiable by profile, generation, authority, run, root, and logical scope, with correct Windows/Linux path equality.

## Required context

1. Read `../../00-READ-ME-FIRST.md`, `../../01-REVIEW-VERDICT.md`, `../../02-FINDINGS-REGISTER.md`, and `../../03-EXECUTION-ORDER.md`.
2. Read the architecture and plan documents relevant to this subbundle.
3. Read `../../sharedinfo/required-skills.md` and use installed skills.
4. Confirm every dependency is explicitly unlocked.
5. Confirm current branch/HEAD and refresh the evidence map if it differs from the baseline.
6. Create `proof/proof-manifest.json` and `proof/SESSION-HANDOFF.md` before risky edits.

## Detailed tasks

1. Create WorkspaceExecutionScope from the admitted execution run and governance snapshot rather than from root + WorkspaceScopeDescriptor only.
2. Populate DatabaseProfileId, DatabaseProfileGeneration, AuthorityId, AuthorityFingerprint, and ExecutionRunId on every run-owned bundle.
3. Define which fields participate in identity, reuse, and diagnostic labels; project GUID alone must not identify a scope across profiles.
4. Canonicalize roots with Path.GetFullPath and use an OS-aware comparer: case-insensitive on Windows, case-sensitive on Linux unless the filesystem is explicitly known otherwise.
5. Reject mismatched profile/generation/authority/run bundles before capability composition.
6. Add deterministic tests for Windows-style and Linux-style roots without requiring the tests to run on both operating systems.

## C# architecture impact

This is architecture-sensitive work. Map current owner, target owner, contracts, implementations, composition root, dependency direction, test seam, and old behavior to delete before editing. New interfaces without production consumption do not count as completion.

## Testability contract

- full identity equality
- profile collision
- generation mismatch
- authority mismatch
- OS-aware path comparer

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

- Do not embed secrets or raw user data into identity.
- Do not use machine-specific paths as durable cross-host IDs.
- Do not break existing managed root layout.

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

SB05 and SB06.

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
