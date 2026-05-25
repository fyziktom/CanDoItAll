# SB02 — Conditional finalization for leased outbox work

## Status

Completed.

## Objective

Prevent stale workers from committing final state after losing a lease.

## Covered Inputs

- User requested review of what Codex fulfilled and skipped.
- User requested removal of DB bottlenecks left from SQLite-era protection.
- User requested preserving canonical database source-of-truth.

## Prerequisites

- Work from branch `db-remove-sqlite`.
- Do not reintroduce SQLite runtime provider, migrations, or UI.
- Keep code comments in English.
- Read `codex/skills/bundles/candoitall-bundle-execution/SKILL.md` before implementation.

## Exact Source References


- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs`
- `repo://src/CanDoItAll.Modules.Workspace/Connectors/ConnectorOutboxService.cs`
- `repo://src/CanDoItAll.Modules.Automation/Services/AutomationMessagingServices.cs`


## Deliverables


1. Add a finalization helper for process outbox that conditionally updates final state only if `Id`, `LeaseToken`, and unexpired lease still match.
2. Add the same pattern to connector command processing.
3. Review automation delivery finalization and either apply the same guard or document why handler execution is short/atomic enough.
4. Ensure attempt/audit rows are idempotent or committed only after guarded finalization succeeds.
5. Add negative tests: worker A claims, worker B steals after expiry, worker A must not mark completed/dead-letter/retry.


## Dependency Impact

This subbundle affects downstream trust in throughput/canonicality proof.

## Validation Depth

Critical where indicated. Use source audit, focused tests, broad validation when possible, and anti-stub checks.

## Implementation Steps


1. Add a finalization helper for process outbox that conditionally updates final state only if `Id`, `LeaseToken`, and unexpired lease still match.
2. Add the same pattern to connector command processing.
3. Review automation delivery finalization and either apply the same guard or document why handler execution is short/atomic enough.
4. Ensure attempt/audit rows are idempotent or committed only after guarded finalization succeeds.
5. Add negative tests: worker A claims, worker B steals after expiry, worker A must not mark completed/dead-letter/retry.


## Scope Exceptions

None unless explicitly documented in proof.

## Do Not Do

- Do not hide failures behind focused tests only.
- Do not claim throughput improvement without either numeric benchmark or clearly stated limitation.
- Do not introduce new non-canonical DB source-of-truth.
- Do not rely on EF tracked entity `SaveChangesAsync` for final leased state without a guarded `WHERE LeaseToken = ...` condition.

## Acceptance Checklist


- [ ] Process outbox stale worker cannot finalize after lease loss.
- [ ] Connector command stale worker cannot finalize after lease loss.
- [ ] Audit rows are not duplicated by stale workers.
- [ ] Tests prove exactly one canonical final state.


## Proof Required


- `proof/SB02/manifest.md`
- changed-file hashes
- integration tests for lease-steal/finalize race
- source assertion showing conditional finalization query


## Browser Validation Logging

N/A unless Data Sources UI or runtime/pending activation display changes.

## Progression Gate

This subbundle is complete only when proof artifacts are written under `proof/` and downstream subbundles can rely on its claims.

## Suggested Agent Prompt

Execute this subbundle exactly. Preserve canonical runtime DB invariants and create artifact-backed proof before moving to the next subbundle.
