# SB03 — Remove dead hot-switching and drain state

## Status

Completed.

## Objective

Remove obsolete context lease/drain mechanics from `DatabaseRuntimeSwitching.cs` and configuration.

## Covered Inputs

- User requested review of latest `db-remove-sqlite` branch.
- User requested another DB bottleneck review after SQLite removal.
- User requested canonicality protection while unlocking PostgreSQL performance.

## Prerequisites

See dependency map in `plan/01-phase-plan.md`.

## Exact Source References

- `repo://src/CanDoItAll.Infrastructure/ControlPlane/CanonicalRuntimeDatabase.cs`
- `repo://src/CanDoItAll.Infrastructure/Persistence/DatabaseRuntimeSwitching.cs`
- `repo://src/CanDoItAll.Infrastructure/Persistence/SwitchableAppDbContextFactory.cs`
- `repo://src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- `repo://src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs`
- `repo://src/CanDoItAll.Modules.Automation/Services/AutomationMessagingServices.cs`
- `repo://src/CanDoItAll.Modules.Workspace/Connectors/ConnectorOutboxService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`

## Deliverables

- Audit all references to `AcquireContextLeaseAsync`, `BeginSwitchAsync`, `DatabaseContextLease`, `DatabaseSwitchSession`, `WaitForDrainAsync`, and `EnableMaintenanceHotSwitch`.
- Remove dead APIs or replace them with a small `RuntimeDatabaseStatus` service that only stores canonical profile metadata and generation.
- Remove `EnableMaintenanceHotSwitch` unless implementing a real explicit maintenance-only feature is deliberately approved.
- Ensure normal `DbContext` creation cannot be blocked by runtime switching state.
- Update tests and docs to refer to restart-first activation.

## Dependency Impact

This subbundle affects downstream canonicality and throughput proof. If it fails, later subbundles must not claim merge readiness.

## Validation Depth

Critical semantic validation is required. Do not rely only on build success.

## Implementation Steps

1. Audit all references to `AcquireContextLeaseAsync`, `BeginSwitchAsync`, `DatabaseContextLease`, `DatabaseSwitchSession`, `WaitForDrainAsync`, and `EnableMaintenanceHotSwitch`.
2. Remove dead APIs or replace them with a small `RuntimeDatabaseStatus` service that only stores canonical profile metadata and generation.
3. Remove `EnableMaintenanceHotSwitch` unless implementing a real explicit maintenance-only feature is deliberately approved.
4. Ensure normal `DbContext` creation cannot be blocked by runtime switching state.
5. Update tests and docs to refer to restart-first activation.

## Scope Exceptions

Do not modify `CanDoItAll.IPFS`. Do not reintroduce SQLite support.

## Do Not Do

- Do not hide validation failures.
- Do not weaken canonical runtime DB semantics.
- Do not replace durable DB ownership with only in-memory locks.
- Do not add comments in non-English inside source code.

## Acceptance Checklist

- [ ] No normal runtime path references context leases or drain signals.
- [ ] `EnableMaintenanceHotSwitch` is removed or explicitly implemented with proof.
- [ ] `DatabaseSwitchCoordinator` does not pretend to hot-switch.
- [ ] Source assertion audit passes.

## Proof Required

- `proof/SB03/manifest.md`
- `proof/SB03/source-assertions.txt`
- `proof/SB03/unit-tests.txt`

## Browser Validation Logging

N/A unless UI-visible behavior is changed.

## Progression Gate

All acceptance checklist items must pass, and `proof/SB03/manifest.md` must contain changed-file hashes, command transcripts, source assertions, and residual risk notes.

## Suggested Agent Prompt

Execute SB03 only. Read this README, implement the scoped changes, run the required validation, write the proof manifest, and stop before downstream work unless the progression gate passes.
