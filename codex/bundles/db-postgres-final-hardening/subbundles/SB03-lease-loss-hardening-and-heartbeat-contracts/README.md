# SB03 — Lease-loss hardening and heartbeat contracts

## Status

Completed.

## Objective

Convert lease renewal failure into explicit canonical stop/failure semantics.

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
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `repo://src/CanDoItAll.Modules.Workspace/Connectors/ConnectorOutboxService.cs`


## Deliverables


1. Introduce a clear `LeaseLost` result/exception type for process outbox and connector command processing.
2. Make renewal failures observable to finalization logic.
3. Ensure long-running operations either stop before mutation or can finish externally but cannot write canonical state.
4. Record telemetry/audit for lease lost.
5. Add tests for renewal failure, expired lease, stolen lease, and cancellation.


## Dependency Impact

This subbundle affects downstream trust in throughput/canonicality proof.

## Validation Depth

Critical where indicated. Use source audit, focused tests, broad validation when possible, and anti-stub checks.

## Implementation Steps


1. Introduce a clear `LeaseLost` result/exception type for process outbox and connector command processing.
2. Make renewal failures observable to finalization logic.
3. Ensure long-running operations either stop before mutation or can finish externally but cannot write canonical state.
4. Record telemetry/audit for lease lost.
5. Add tests for renewal failure, expired lease, stolen lease, and cancellation.


## Scope Exceptions

None unless explicitly documented in proof.

## Do Not Do

- Do not hide failures behind focused tests only.
- Do not claim throughput improvement without either numeric benchmark or clearly stated limitation.
- Do not introduce new non-canonical DB source-of-truth.


## Acceptance Checklist


- [ ] Heartbeat failure cannot silently continue into canonical finalization.
- [ ] Lease-lost state is observable in logs/audit.
- [ ] Stolen lease tests pass.


## Proof Required


- `proof/SB03/manifest.md`
- semantic invariant doc for lease lifecycle
- tests proving stale worker stop condition


## Browser Validation Logging

N/A unless Data Sources UI or runtime/pending activation display changes.

## Progression Gate

This subbundle is complete only when proof artifacts are written under `proof/` and downstream subbundles can rely on its claims.

## Suggested Agent Prompt

Execute this subbundle exactly. Preserve canonical runtime DB invariants and create artifact-backed proof before moving to the next subbundle.
