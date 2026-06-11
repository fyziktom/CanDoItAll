# SB05 Proof Manifest

- Subbundle: `SB05`
- Status: `Completed`
- Owned requirement: `REQ-006`
- Raw notes: prove scheduler/workflow-origin process starts and read-only verification jobs through process-owned lifecycle, not driver hooks.
- Semantic invariant contract: `bundle://proof/SB05/semantic-invariants.md`
- Bundle start SHA: `430496c5e7217a847e9172dcc0c2fba57f75f75c`

## Changed File Hashes

| Path | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs` | `24197ff50702d4f172066cd48b27f0dc0e6afa3b55d281d88cfcb320cbb84c4c` | `78c5f534c3a51369ad8ac5eddaf0903954e715a635d97ca0e9b7cb9d6970b7dc` |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs` | `400b11e0df4f96ac4b969cdd021f9d859e66b2e059ce97342498eda5de341900` | `5412aa9fece197b982b0f16571bf478a4382af5d0d7b5b0935eca53e5a9d7fb2` |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs` | `59fb3c1bfc2860ee6079f070dcadcc0ea4dbca92dba74e3fcdb76f7a096a0bbf` | `959291046299f5728ba2146f74d3b37a7d46d2b370aa77823c28e44630ec25c3` |

## Command Transcripts

- Failing-first transcript: `bundle://proof/SB05/transcripts/failing-first-source-assertion.txt`
- Passing focused integration transcript: `bundle://proof/SB05/transcripts/focused-integration.txt`
- Source assertion transcript: `bundle://proof/SB05/transcripts/source-assertions.txt`
- Boundary scan transcript: `bundle://proof/SB05/transcripts/boundary-scan.txt`
- Anti-stub audit transcript: `bundle://proof/SB05/transcripts/anti-stub-audit.txt`

## Semantic Adequacy

- Test name: `StartRunFromTriggerAsync_SB05_INV_001_starts_scheduler_and_workflow_origin_runs_through_process_owned_path_without_driver_hooks`
- Test name: `Process_readonly_verification_job_SB05_INV_002_models_scheduler_and_workflow_jobs_as_manager_readback_requests_without_mutation`
- Test name: `Process_readonly_verification_job_runner_SB05_INV_003_executes_scheduler_and_workflow_lifecycle_status_provenance_readback_without_mutation`
- Guard name: `Process_runtime_host_codefirst_SB05_INV_004_scheduler_workflow_lifecycle_proof_uses_process_owned_readonly_paths`
- Invariant ID: `SB05_INV_001`
- Invariant ID: `SB05_INV_002`
- Invariant ID: `SB05_INV_003`
- Invariant ID: `SB05_INV_004`
- Shallow-pass trap: a scheduler/workflow proof can start a run while bypassing process-owned trigger metadata, losing outbox provenance, calling workflow/runtime driver hooks directly, or using read-only verification jobs that allow mutation.
- Adversarial negative proof: `bundle://proof/SB05/transcripts/failing-first-source-assertion.txt` records that baseline source lacked the SB05 trigger, verification job, runner, guard, and workflow readback hardening markers.
- Semantic positive proof: `bundle://proof/SB05/transcripts/focused-integration.txt` exits 0 with scheduler/workflow trigger, verification job model, verification job runner, and code-first boundary guard tests passing.
- Source assertion proof: `bundle://proof/SB05/transcripts/source-assertions.txt` verifies trigger metadata/outbox assertions, scheduler/workflow read-only job assertions, the code-first guard, and production launcher/runner markers.
- Boundary proof: `bundle://proof/SB05/transcripts/boundary-scan.txt` verifies the scheduler process launcher uses `ProcessesService.StartRunFromTriggerAsync`, not workflow runtime or execution-capable driver hooks, and verifies the verification job runner delegates to `IProcessManagerReadOnlyVerificationFacade` with all mutation flags denied.
- Anti-stub audit: `bundle://proof/SB05/transcripts/anti-stub-audit.txt` reports no TODO, HACK, NotImplemented, stub, or fake-pass markers in SB05 added lines.

## Production Behavior Artifact Matrix

| Signal or record | Producer | Consumer | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Scheduler-origin process run | `SchedulerTargetLauncher.LaunchProcessAsync` through `ProcessesService.StartRunFromTriggerAsync` | Scheduler planner and process runtime readbacks | Boundary scan proves production launcher uses process-owned trigger service; focused test proves persisted trigger reason includes source kind, id, name, requester, start-run outbox, and automation dispatch outbox. | Guard and boundary scan reject direct workflow runtime or execution-capable driver hooks in the process launcher. |
| Workflow-origin process run | `ProcessesService.StartRunFromTriggerAsync` with `ProcessRunTriggerSourceKind.WorkflowRun` | Process runtime readbacks | Focused test proves workflow-origin starts persist trigger source metadata, create process-owned steps, emit outbox records, and leave workflow/execution link rows empty. | Failing-first transcript shows SB05 workflow-origin process proof markers were absent from baseline. |
| Read-only verification job model | `ProcessReadOnlyVerificationJob` | Scheduler/workflow verification orchestration | Focused test proves scheduler and workflow jobs convert to manager readback requests, retain source reference/correlation/requester, and deny process/transition/finalizer mutation. | Unsupported source kind throws `ArgumentOutOfRangeException`; source assertions fail if workflow request conversion markers are removed. |
| Verification job lifecycle result | `ProcessReadOnlyVerificationJobRunner` | Integration tests and release proof | Focused test proves scheduler/workflow lifecycle status, timestamps, source provenance, audit id/count, audit hash, manager readback, read-only contract, and no-mutation flags. | Boundary scan rejects mutating process operations or execution-capable driver markers in the runner. |
| Code-first boundary guard | `ProcessRuntimeHostCodeFirstGuardTests` | Release closure | Guard test proves SB05 proof methods and production launcher/runner markers remain aligned. | Baseline failing-first transcript lacks the SB05 guard marker. |

## Closure Decision

- Entry gate: Passed because SB04 runtime-host readback remained process-owned and read-only.
- Closure gate: Passed after focused integration proof, source assertions, boundary scan, anti-stub audit, and failing-first source proof.
- Progression decision: SB06 may proceed; scheduler/workflow process starts and read-only verification jobs have process-owned lifecycle proof.
