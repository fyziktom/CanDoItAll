# SB01: Make canonical execution authority the runtime enforcement input

## Metadata

- Phase: B — Authority integrity
- Depends on: `SB00`
- Checkpoint: No
- Optional: No
- Baseline: `fyziktom/CanDoItAll` `maf-refactor` @ `9e47a332fa9d329422ff616a0e0b6a97a22933c9`
- Primary executor: Claude Code / Claude Fable 5
- Reasoning profile: deepest available (`xHigh` intent)
- Compatible executor: Codex or another high-capability coding model using the included prompt
- Review findings addressed: `FR-001`

## Goal

Turn AgentExecutionAuthorityRecord from metadata/audit evidence into the single immutable permission snapshot used by capability planning and tool invocation.

## Required context

1. Read `../../00-READ-ME-FIRST.md`, `../../01-REVIEW-VERDICT.md`, `../../02-FINDINGS-REGISTER.md`, and `../../03-EXECUTION-ORDER.md`.
2. Read the architecture and plan documents relevant to this subbundle.
3. Read `../../sharedinfo/required-skills.md` and use installed skills.
4. Confirm every dependency is explicitly unlocked.
5. Confirm current branch/HEAD and refresh the evidence map if it differs from the baseline.
6. Create `proof/proof-manifest.json` and `proof/SESSION-HANDOFF.md` before risky edits.

## Detailed tasks

1. Introduce a provider-neutral AgentExecutionGovernanceSnapshot or equivalent immutable execution contract containing authority identity, profile/generation, workspace scope, read/mutation grants, allowed operations, capabilities, aliases, policy version, and fingerprint.
2. Persist only its safe projection, but retain the full trusted snapshot through the in-process execution command and continuation lease.
3. At execution start, validate snapshot agent, profile, generation, scope, authority ID/fingerprint, and transient-context digest before creating the runtime.
4. Populate AgentRuntimeContextIntent and AgentRuntimeToolProviderContext from the governance snapshot, not from UI access entries or default-true behavior.
5. Filter mutation tools when MutationAllowed is false and read tools when ReadAllowed is false; invocation-time policy must independently enforce the same snapshot.
6. Thread allowed operations, capability scopes, external-target aliases, and managed-artifact refs from one snapshot; define monotonic intersection with agent configuration and process restrictions.
7. Add a negative production-path test showing that an agent configured with mutation tools cannot mutate when canonical authority is read-only.

## C# architecture impact

This is architecture-sensitive work. Map current owner, target owner, contracts, implementations, composition root, dependency direction, test seam, and old behavior to delete before editing. New interfaces without production consumption do not count as completion.

## Testability contract

- authority projection round-trip
- capability filtering
- invocation denial
- continuation uses original snapshot
- tampered metadata/digest fails closed

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

- Do not serialize opaque attachment payloads into durable authority.
- Do not let a module widen a previously admitted snapshot during continuation.
- Do not keep product tools as a second grant authority.

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

SB02 after direct tests prove authority controls both composition and invocation.

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
