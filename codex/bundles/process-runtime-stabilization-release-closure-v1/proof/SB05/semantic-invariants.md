# SB05 Semantic Invariants

## Scheduler And Workflow Origin Starts Stay Process-Owned

- Invariant ID: `SB05_INV_001`
- Source raw note: determine whether scheduler/workflow process starts still work like before without further extraction.
- Expected behavior: scheduler and workflow origin process runs start through `ProcessesService.StartRunFromTriggerAsync`, persist source kind/id/name/requester in process trigger metadata, create process-owned steps, emit start-run and automation-dispatch outbox records, and do not create workflow or execution driver hook rows for those trigger starts.
- Disallowed shallow implementation: direct scheduler-to-driver calls, direct workflow runtime calls for process-origin starts, missing trigger metadata, missing process outbox records, dead-lettered outbox records, or manual dispatch suppression.
- Failing-first proof: `bundle://proof/SB05/transcripts/failing-first-source-assertion.txt` shows baseline source lacked the SB05 trigger proof name and code-first guard.
- Passing test: `bundle://proof/SB05/transcripts/focused-integration.txt` proves `StartRunFromTriggerAsync_SB05_INV_001_starts_scheduler_and_workflow_origin_runs_through_process_owned_path_without_driver_hooks` passes.
- Source assertions: `bundle://proof/SB05/transcripts/source-assertions.txt` verifies scheduler/workflow source kinds, empty workflow/execution rows, empty workflow links, non-dead-lettered outbox assertion, and production launcher markers.
- Boundary proof: `bundle://proof/SB05/transcripts/boundary-scan.txt` proves `LaunchProcessAsync` uses `processesService.StartRunFromTriggerAsync` and not workflow runtime or execution-capable driver hooks.

## Verification Jobs Are Manager Readback And Read-Only

- Invariant ID: `SB05_INV_002`
- Source raw note: read-only verification jobs must remain process-owned and non-mutating.
- Expected behavior: scheduler and workflow verification jobs model manager readback requests, preserve source/correlation/requester metadata, enforce supported source kinds, and expose no mutation permissions.
- Disallowed shallow implementation: a scheduler-only model check, workflow jobs that skip request conversion, or readback jobs that allow process, transition, or finalizer mutation.
- Passing test: `bundle://proof/SB05/transcripts/focused-integration.txt` proves `Process_readonly_verification_job_SB05_INV_002_models_scheduler_and_workflow_jobs_as_manager_readback_requests_without_mutation` passes.
- Source assertions: `bundle://proof/SB05/transcripts/source-assertions.txt` verifies `workflowReadbackRequest` and read-only model markers.
- Anti-stub audit: `bundle://proof/SB05/transcripts/anti-stub-audit.txt` reports no stub or fake-pass markers.

## Verification Job Runner Emits Lifecycle Provenance

- Invariant ID: `SB05_INV_003`
- Source raw note: scheduler/workflow verification lifecycle needs status, timestamps, provenance, audit/readback, and no-mutation proof.
- Expected behavior: the job runner returns scheduler and workflow lifecycle records with completed status, started/completed timestamps, source provenance, correlation id, process run id, step run id, audit id/count, audit hash, manager readback surface, scheduler/workflow read-only contract, request identity, and denied mutation flags.
- Disallowed shallow implementation: returning only a DTO without lifecycle state, timestamps, audit count, source provenance, or read-only contract validation.
- Passing test: `bundle://proof/SB05/transcripts/focused-integration.txt` proves `Process_readonly_verification_job_runner_SB05_INV_003_executes_scheduler_and_workflow_lifecycle_status_provenance_readback_without_mutation` passes.
- Boundary proof: `bundle://proof/SB05/transcripts/boundary-scan.txt` verifies the runner delegates to `IProcessManagerReadOnlyVerificationFacade`, emits `SchedulerWorkflowReadOnlyJob`, and keeps mutation flags denied.

## Code-First Guard Keeps The Proof Aligned

- Invariant ID: `SB05_INV_004`
- Source raw note: release closure must not count proof that bypasses process-owned paths.
- Expected behavior: the guard checks the SB05 trigger proof, verification job proof, production scheduler process launcher, and read-only job runner source markers together.
- Disallowed shallow implementation: renaming proof tests without checking production launcher/runner boundaries.
- Passing guard: `bundle://proof/SB05/transcripts/focused-integration.txt` includes `Process_runtime_host_codefirst_SB05_INV_004_scheduler_workflow_lifecycle_proof_uses_process_owned_readonly_paths`.
- Failing-first proof: `bundle://proof/SB05/transcripts/failing-first-source-assertion.txt` shows the SB05 guard marker was absent from baseline source.

## Production Behavior Artifact Matrix

| Signal or record | Producer | Consumer | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Scheduler process trigger metadata | Scheduler process launcher and `ProcessesService.StartRunFromTriggerAsync` | Process run persistence and readback tests | Focused test asserts source kind, id, name, requester, run name, operating mode, and trigger reason. | Boundary scan rejects workflow runtime and execution-capable driver hooks in `LaunchProcessAsync`. |
| Workflow process trigger metadata | `ProcessesService.StartRunFromTriggerAsync` | Process run persistence and readback tests | Focused test asserts workflow source kind/id/name/requester, process-owned steps, and no workflow/execution link rows. | Failing-first transcript lacks SB05 workflow trigger proof markers. |
| Process outbox records | Process runtime start path | Scheduler/workflow trigger proof | Focused test asserts start-run and automation-dispatch command records, owned process run id, payload, and non-dead-lettered status. | Test fails if records are missing or dead-lettered. |
| Verification job manager readback request | `ProcessReadOnlyVerificationJob` | Scheduler/workflow verification job runner | Focused model test proves scheduler and workflow request conversion and read-only mutation flags. | Unsupported source kind throws and workflow request conversion source assertion fails if removed. |
| Verification job lifecycle/audit contract | `ProcessReadOnlyVerificationJobRunner` | Integration proof and release manifest | Focused runner test proves lifecycle status, timestamps, provenance, audit id/count/hash, manager readback, request identity, read-only contract, and no-mutation flags. | Boundary scan rejects mutating operations and execution-capable driver markers in the runner. |
