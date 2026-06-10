# SB033 Semantic Invariants

## SB033_INV_001 Typed Scheduler/Workflow Read-Only Verification Job
- Source raw note: P11 requires scheduler/workflow verification readiness without approving execution-capable process drivers.
- Expected behavior: scheduler and workflow readiness is modeled by `ProcessReadOnlyVerificationJob` with a typed source kind, exact `ProcessDriverVerificationGatewayLane`, typed `ProcessReadOnlyVerificationBatchPayload`, typed manager projection mode, requester identity, request timestamp, and bounded audit readback limit.
- Disallowed shallow implementation: string-only job metadata, dynamic payload dispatch, generic object routing, or a job shape that can mutate process, transition, or finalizer state.
- Positive proof: `Process_readonly_verification_job_SB031_INV_001_models_scheduler_and_workflow_jobs_as_manager_readback_requests_without_mutation` in `bundle://proof/SB031/transcripts/read-only-verification-job-focused-tests.txt`.
- Source proof: `bundle://proof/SB031/transcripts/read-only-verification-job-source-assertions.txt`.
- Changed source: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationJobModel.cs`.
- Red-team negative case: `bundle://proof/SB033/transcripts/red-team-scheduler-workflow-readiness-shallow-proof-rejection.txt`.
- Downstream dependency check: P12 runtime regression matrix can treat scheduler/workflow readiness as manager readback preparation only, not execution driver authorization.

## SB033_INV_002 Scheduler/Workflow Modules Do Not Bypass The Manager Boundary
- Source raw note: SB032 requires proof that scheduler/workflow do not call process drivers directly.
- Expected behavior: `src/CanDoItAll.Modules.SchedulerPlanner` and `src/CanDoItAll.Modules.AgentFramework` contain no direct references to process driver namespaces, verification gateway types, runtime host types, orchestrator shortcuts, or payload builder shortcuts.
- Disallowed shallow implementation: a report row claiming no direct calls without source scanning, a scan that only checks one module, or an allowlist that permits direct gateway/orchestrator references.
- Positive proof: `Scheduler_workflow_verification_readiness_SB032_INV_001_does_not_call_process_drivers_directly` in `bundle://proof/SB032/transcripts/scheduler-workflow-readiness-focused-tests.txt`.
- Source proof: `bundle://proof/SB032/transcripts/scheduler-workflow-no-direct-driver-source-scan.txt`.
- Red-team negative case: `bundle://proof/SB033/transcripts/red-team-scheduler-workflow-readiness-shallow-proof-rejection.txt`.
- Downstream dependency check: future scheduler/workflow integration must consume the manager/facade path or another explicitly approved boundary; it cannot call process drivers directly.

## SB033_INV_003 Manager Readback Boundary Remains Mutation-Free
- Source raw note: P11 must preserve the no-mutation and Core genericity guarantees established by P05-P10.
- Expected behavior: `ProcessReadOnlyVerificationJob.ToManagerReadbackRequest` returns `ProcessManagerReadOnlyVerificationReadbackRequest`, preserving the manager facade/readback boundary and the existing durable audit query path.
- Disallowed shallow implementation: calling `IProcessVerificationRuntimeHost` directly from scheduler/workflow, constructing driver gateway payloads in scheduler/workflow modules, or adding execution permissions to the job model.
- Positive proof: the Gate K focused suite passes 31 tests covering host, selector, durable audit, facade, diagnostics readback, and scheduler/workflow readiness.
- Anti-stub audit: `bundle://proof/SB033/transcripts/gate-k-source-diff-and-anti-stub-audit.txt`.
- Red-team negative case: `bundle://proof/SB033/transcripts/red-team-scheduler-workflow-readiness-shallow-proof-rejection.txt`.
- Downstream dependency check: SB034-SB036 runtime regression matrix must pass before any downstream runtime claims rely on Gate K.

## Production Behavior Artifact Matrix
| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `ProcessReadOnlyVerificationJob` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationJobModel.cs` | `ToManagerReadbackRequest` returns a typed manager readback request | Focused suite passes 31 tests | Gate K red-team rejects execution-capable or string-only jobs |
| Scheduler/workflow no-direct-driver boundary | `bundle://proof/SB032/transcripts/scheduler-workflow-no-direct-driver-source-scan.txt` | Source scan covers SchedulerPlanner and AgentFramework modules | Focused test enforces the same scan in CI | Red-team rejects report-only no-direct-call proof |
| Manager readback request conversion | `ProcessReadOnlyVerificationJob.ToManagerReadbackRequest` | Manager facade readback tests from P10 remain passing | Gate K focused suite includes readback and audit tests | Anti-stub audit rejects placeholders and bundle-path coupling |

## Gate Result
Gate K is semantically adequate for P11. Scheduler/workflow readiness is modeled as typed, read-only manager readback preparation, while SchedulerPlanner and AgentFramework remain free of direct process driver and verification host calls.
