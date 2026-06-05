# Assumptions And Risks

## Assumptions

- Codex has the latest `maf-processes-refactor` branch locally.
- Existing focused tests around dispatch, finalizer, artifacts, recovery, and provider/tool boundaries are available.
- `ProcessRunAutomationDispatchService.Dispatch.cs` remains the authoritative dispatch orchestrator.
- Helper extraction should preserve wrapper entry points to avoid broad test rewrites.

## Critical Path Risks

1. Moving too much from `Dispatch.cs` can silently alter lifecycle behavior.
2. Claim/heartbeat code is safety-critical; a shallow extraction can create duplicate or lost transitions.
3. Concurrency selection rules can introduce races if stale/recoverable/competing execution semantics drift.
4. Route planning must not become a fake state machine that bypasses existing finalizer and workflow behavior.
5. Driver-readiness work must stay documentation-only.

## Validation Risks

- Tests that only build are insufficient.
- Source scans must prove no Process Core, no driver API, no MAF product dependency, and no viewport-proof drift.
- Focused integration slices must cover concurrent/stale/recovery dispatch paths, not only happy-path process execution.

## Reopen Triggers

Reopen earlier subbundles if:

- Any helper adds DB, storage, MAF, Workbench, or UI dependencies unexpectedly.
- Any route helper mutates state instead of returning decisions/snapshots.
- Any claim/heartbeat helper starts swallowing claim-lost exceptions.
- Required existing tests are weakened, skipped, quarantined, or renamed without replacement.
