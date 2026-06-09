# SB006 Semantic Invariants

## Status
Completed.

## Invariant SB006_INV_001
- Invariant ID: `SB006_INV_001`
- Source raw note: "Review real code, not only bundle report" and "Determine real test outcome."
- Expected behavior: Starting a process run persists a `ProcessRun`, materialized `ProcessStepRun` rows, project-structure runtime context, journal/work brief records, and a durable process dispatch outbox record, while invalid, not-ready, and duplicate start attempts fail with typed validation errors.
- Disallowed shallow implementation: Returning a run ID from `StartRunAsync` or updating bundle status without proving durable records and guard failures through integration tests.
- Failing-first test: Duplicate launch and invalid/not-ready contexts are rejected by `StartRunAsync_SB018_INV_002_rejects_invalid_not_ready_and_duplicate_start_attempts`.
- Passing test: `StartRunAsync_SB018_INV_001_persists_project_context_runtime_rows_and_dispatch_outbox` and `StartRunAsync_SB018_INV_002_rejects_invalid_not_ready_and_duplicate_start_attempts` passed in `bundle://proof/SB006/transcripts/gate-b-run-lifecycle-integration-tests.txt`.
- Changed source files: No production source changed in SB006. Current source hashes are captured in `bundle://proof/SB006/manifest.md`.
- Production assertions: `bundle://proof/SB006/transcripts/gate-b-source-assertions.txt` cites `StartRunAsync`, launch-plan guards, runtime row creation, and outbox record creation.
- Red-team negative case: `bundle://proof/SB006/red-team/duplicate-and-invalid-start-rejection.txt` rejects status-only proof and duplicate run creation.
- Downstream dependency check: SB007 may start only because SB006 proves an actual pending dispatch outbox record exists after persisted run creation.

## Shallow-Pass Trap
A fake Gate B closure could stop at an API response, an in-memory object, or a report row. SB006 rejects that by requiring integration assertions for persisted run/step/project context/outbox rows and explicit negative checks for invalid, not-ready, and already-executed launch attempts.

## Semantic Positive Proof
- `bundle://proof/SB006/transcripts/gate-b-run-lifecycle-integration-tests.txt`
- `bundle://proof/SB006/transcripts/gate-b-source-assertions.txt`

## Adversarial Negative Proof
- `bundle://proof/SB006/red-team/duplicate-and-invalid-start-rejection.txt`

## Anti-Stub Audit
- `bundle://proof/SB006/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- Matches are documentation and negative test assertions, not an execution-capable process-driver runtime host, process-driver registry, selector, or production `NotImplemented` path.

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| `ProcessRun` | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs` | Runtime state, readback APIs, UI, and outbox dispatch | Created once per executable launch context with active status and typed metadata | Duplicate launch attempt is rejected before a second run is created |
| `ProcessStepRun` | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs` | Step orchestration, automation dispatch, artifact projection | Created for published definition steps with explicit initial status | Invalid or not-ready launch context prevents step graph creation |
| `ProcessOutboxRecord` | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs` | Outbox claim/drain worker and automation dispatch | Created pending during run start; SB007-SB009 own drain behavior | SB006 does not claim dispatch completion from enqueue-only proof |
| Project-structure runtime context | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs` | Project navigation and generated output proof | Bound to the created run for later readback | Guard failures prevent orphan project context records |
