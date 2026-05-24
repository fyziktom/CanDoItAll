# SB04 — Maintenance profile context factory boundaries

## Status

Prepared.

## Objective

Separate canonical runtime context creation from profile-specific maintenance context creation.

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

- Split or rename `ISwitchableAppDbContextFactory` to make responsibilities explicit.
- Preferred: `IDbContextFactory<AppDbContext>` for canonical runtime and `IProfileAppDbContextFactory` for explicit profile-specific maintenance operations.
- Audit runtime modules to ensure they inject only canonical `IDbContextFactory<AppDbContext>`.
- Allow profile-specific contexts only in schema health, create/bootstrap, transfer, and migration proof paths.
- Add an analyzer-style test or source audit that fails if process/automation/cognitive-memory runtime injects profile-specific factory.

## Dependency Impact

This subbundle affects downstream canonicality and throughput proof. If it fails, later subbundles must not claim merge readiness.

## Validation Depth

Critical semantic validation is required. Do not rely only on build success.

## Implementation Steps

1. Split or rename `ISwitchableAppDbContextFactory` to make responsibilities explicit.
2. Preferred: `IDbContextFactory<AppDbContext>` for canonical runtime and `IProfileAppDbContextFactory` for explicit profile-specific maintenance operations.
3. Audit runtime modules to ensure they inject only canonical `IDbContextFactory<AppDbContext>`.
4. Allow profile-specific contexts only in schema health, create/bootstrap, transfer, and migration proof paths.
5. Add an analyzer-style test or source audit that fails if process/automation/cognitive-memory runtime injects profile-specific factory.

## Scope Exceptions

Do not modify `CanDoItAll.IPFS`. Do not reintroduce SQLite support.

## Do Not Do

- Do not hide validation failures.
- Do not weaken canonical runtime DB semantics.
- Do not replace durable DB ownership with only in-memory locks.
- Do not add comments in non-English inside source code.

## Acceptance Checklist

- [ ] Runtime modules cannot accidentally use profile-specific DB contexts.
- [ ] Maintenance paths remain functional.
- [ ] Source audit lists all allowed profile-specific context call sites.

## Proof Required

- `proof/SB04/manifest.md`
- `proof/SB04/source-audit.txt`
- targeted tests for schema health, create empty, transfer

## Browser Validation Logging

N/A unless UI-visible behavior is changed.

## Progression Gate

All acceptance checklist items must pass, and `proof/SB04/manifest.md` must contain changed-file hashes, command transcripts, source assertions, and residual risk notes.

## Suggested Agent Prompt

Execute SB04 only. Read this README, implement the scoped changes, run the required validation, write the proof manifest, and stop before downstream work unless the progression gate passes.
