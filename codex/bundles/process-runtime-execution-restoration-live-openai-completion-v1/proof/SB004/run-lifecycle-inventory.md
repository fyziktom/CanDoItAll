# SB004 Run Lifecycle Inventory

## Gate Decision
- Entry gate: Pass. SB003 critical Gate A completed and allows P02 to start.
- Closure gate: Pass. The service, API, UI, entity, and test surfaces for persisted run lifecycle exist in current source.
- Code changes: None. SB004 is source inventory and proof capture only.

## Source Inventory
- API endpoints: `repo://src/CanDoItAll.Web/Api/ProcessesApi.cs` maps run start, run detail, step runs, launch execution, and run readback routes.
- Runtime creation: `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs` creates `ProcessRun`, `ProcessStepRun`, `ProcessRunAssignment`, `ProcessWorkBrief`, `ProcessJournalEntry`, project-structure sync, and outbox records.
- Launch execution: `repo://src/CanDoItAll.Modules.Processes/Launch/ProcessesService.Launch.cs` delegates `ExecuteLaunchPlanAsync` into `StartRunAsync`.
- Trigger-origin launch: `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.TriggerStart.cs` validates trigger metadata and delegates to `StartRunAsync`.
- Readback: `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs` exposes run list, step list, and run details.
- Existing focused tests: `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs` contains durable run lifecycle, outbox, readback, trigger-origin, and duplicate launch guard coverage.

## Proof Artifacts
- Source/API inventory transcript: `bundle://proof/SB004/transcripts/run-lifecycle-service-api-inventory.txt`
- No transient path scan: `bundle://proof/SB004/transcripts/no-transient-bundle-path-scan.txt`
- Anti-stub/runtime-host scan: `bundle://proof/SB004/transcripts/anti-stub-and-runtime-host-drift-scan.txt`

## Progression
- SB005 may use the existing focused integration tests to prove persisted run/step/project context creation and duplicate guards.
