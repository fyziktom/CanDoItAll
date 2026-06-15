# SB12 Runtime History Inventory

Observed at: 2026-06-15

## Legacy Runtime Entities

The archived v1 runtime model contains these legacy runtime history entities:

- `ProcessRun`
- `ProcessStepRun`
- `ProcessRunAssignment`
- `ProcessWorkBrief`
- `ProcessDecisionRecord`
- `ProcessArtifactRecord`
- `ProcessJournalEntry`
- `ProcessConformanceObservation`
- `ProcessImprovementCandidate`
- `ProcessLaunchPlan`
- `ProcessLaunchPlanRole`
- `ProcessLaunchCandidate`
- `ProcessWorkflowRunLink`
- `ProcessLaunchApprovalRecord`
- `ProcessLaunchProvisioningRequest`

Active PostgreSQL migration snapshots still contain historical schema references to these entities. That is schema history, not active runtime service behavior.

## Implemented Compatibility Path

`LegacyProcessHistoryProjectionAdapter` inventories legacy records and creates read-only run projections. Runtime actions against legacy history return `LegacyProcessHistoryActionDenial` with reason `ReadOnlyLegacyHistory`.

## Decision

Legacy history is read-only unless a full explicit migration is selected and validated. Old runtime services are not kept alive for history display.

## Evidence

- Old-symbol scan: `old-symbol-scan-active-process-code.txt`
- Focused tests: `test-unit-sb12.txt`
- Process slice: `test-unit-sb12-process-slice.txt`
