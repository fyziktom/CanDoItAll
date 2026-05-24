# SB02 — Canonical runtime vs pending activation contract

## Status

Prepared.

## Objective

Make the running canonical profile and pending-next-start activation explicit everywhere.

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

- Introduce a model that exposes both runtime canonical profile and persisted pending activation profile.
- Update `DatabaseProfileWorkspaceService`, Data Sources UI, MainLayout DB flyout, dev endpoints, and API DTOs.
- Ensure UI labels use `Running now` and `Pending restart` rather than ambiguous `Active` when they differ.
- Update `DatabaseSwitchResult` or companion DTO so activation result can be displayed after reload.
- Add tests for activate profile -> running profile remains old -> pending profile displayed -> after simulated restart canonical changes.

## Dependency Impact

This subbundle affects downstream canonicality and throughput proof. If it fails, later subbundles must not claim merge readiness.

## Validation Depth

Critical semantic validation is required. Do not rely only on build success.

## Implementation Steps

1. Introduce a model that exposes both runtime canonical profile and persisted pending activation profile.
2. Update `DatabaseProfileWorkspaceService`, Data Sources UI, MainLayout DB flyout, dev endpoints, and API DTOs.
3. Ensure UI labels use `Running now` and `Pending restart` rather than ambiguous `Active` when they differ.
4. Update `DatabaseSwitchResult` or companion DTO so activation result can be displayed after reload.
5. Add tests for activate profile -> running profile remains old -> pending profile displayed -> after simulated restart canonical changes.

## Scope Exceptions

Do not modify `CanDoItAll.IPFS`. Do not reintroduce SQLite support.

## Do Not Do

- Do not hide validation failures.
- Do not weaken canonical runtime DB semantics.
- Do not replace durable DB ownership with only in-memory locks.
- Do not add comments in non-English inside source code.

## Acceptance Checklist

- [ ] The UI never labels pending activation as current runtime.
- [ ] API DTOs clearly distinguish runtime and pending restart state.
- [ ] Existing profile activation remains restart-first.
- [ ] Browser proof covers activation, reload, and second-tab behavior.

## Proof Required

- `proof/SB02/manifest.md`
- `proof/SB02/semantic-invariants.md`
- component tests
- Playwright screenshot and assertions

## Browser Validation Logging

Required for Data Sources and restart/pending activation UI proof.

## Progression Gate

All acceptance checklist items must pass, and `proof/SB02/manifest.md` must contain changed-file hashes, command transcripts, source assertions, and residual risk notes.

## Suggested Agent Prompt

Execute SB02 only. Read this README, implement the scoped changes, run the required validation, write the proof manifest, and stop before downstream work unless the progression gate passes.
