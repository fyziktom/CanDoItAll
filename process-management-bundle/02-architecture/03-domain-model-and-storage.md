# Domain model and storage

## Shared database first

The first release still uses the shared `AppDbContext` with `Processes_*` tables and both SQLite/PostgreSQL migration parity.

This remains the right first move because:

- the current CanDoItAll repo already uses module-assembly EF registration
- direct module integrations are simpler in the shared database
- SQLite must remain viable for smaller/local users
- the hot append-only seams (journal, metrics, overlay cache) can still be extracted later if they become operationally hot

## Core entity groups

### Definition layer
- `ProcessDefinition`
- `ProcessDefinitionVersion`
- `ProcessNode`
- `ProcessTransition`
- `ProcessLayoutState`
- `ProcessContextReference`

### Governance layer
- `ProcessGovernanceProfile`
- `ProcessInterfaceContract`
- `ProcessDecisionRightRule`
- `ProcessChangeRequest`
- `ProcessChangeCommunication`

### Runtime and orchestration layer
- `ProcessRun`
- `ProcessStepRun`
- `ProcessAssignment`
- `ProcessJournalEvent`
- `ProcessWorkBriefTemplate`
- `ProcessWorkBriefSnapshot`
- `ProcessTriageDecisionRecord`
- `ProcessExecutorCorrelation`
- `ProcessExceptionPlaybook`
- `ProcessInputQualityRule`
- `ProcessVariantDefinition`

### Telemetry and conformance
- `ProcessTelemetrySnapshot`
- `ProcessCapacitySignal`
- `ProcessConformanceObservation`
- `ProcessDeviationCluster`
- `ProcessImprovementRequest`
- `ProcessRuntimeOverlayProjection` (query model / optional cache)

## Important storage rules

- Published definition versions are immutable.
- Governance metadata that affects execution is snapshotted into published versions.
- Work brief templates are derived from canonical process semantics, not handwritten ad-hoc runtime packets.
- Runtime journals are append-oriented.
- External executor correlations are stored explicitly rather than inferred later from log text.
- Runtime overlays are projections and may be cached, but they are not canonical state.
- Telemetry can be materialized from journals later, but the semantic shape must exist now.
- Observation records require restricted access and must stay evidence-oriented.

## Storage stance on scale

The bundle still does **not** recommend a separate per-project database as the first implementation.  
That can remain a future scale seam for especially hot append-only data, but it is not the right first move for the current repo and integration patterns.
