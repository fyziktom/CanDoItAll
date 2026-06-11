# SB07 Semantic Invariants

## Invariant SB07_INV_001
- Invariant ID: `SB07_INV_001`
- Source raw note: Continue toward generic runtime host without unsafe side effects through scheduler/workflow launch paths.
- Expected behavior: Scheduler-origin and workflow-origin process starts must use process-owned trigger-start service paths and leave driver hook rows empty.
- Disallowed shallow implementation: Do not add scheduler/workflow driver runtime hooks, direct domain-driver calls, or hosted execution-capable driver workers.
- Failing-first test: `bundle://proof/SB07/transcripts/failing-first-source-assertion.txt`
- Passing test: `bundle://proof/SB07/transcripts/focused-integration.txt`
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`; `repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs`; `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.TriggerStart.cs`; `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerService.cs`; `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationJobRunner.cs`.
- Production assertions: `StartRunFromTriggerAsync_SB07_INV_001` starts representative process definitions from both `SchedulerPlan` and `WorkflowRun` metadata, persists trigger source fields, emits process-owned outbox records, reads back process-owned steps, and leaves workflow/execution driver hook rows empty.
- Red-team negative case: `bundle://proof/SB07/transcripts/forbidden-hook-scan.txt` reports no direct process-driver, execution-capable driver, command execution, workspace/storage/network/Office/CRM mutation hook, or automation-dispatch service tokens in scoped scheduler/workflow verification paths.
- Downstream dependency check: SB08 can classify release readiness from process-owned scheduler/workflow launch proof without approving execution-capable drivers.

## Invariant SB07_INV_002
- Invariant ID: `SB07_INV_002`
- Source raw note: Read-only verification jobs must record lifecycle/readback provenance without mutation.
- Expected behavior: Scheduler and workflow verification jobs must execute through the read-only job runner and manager facade while preserving contract version, source kind/reference, correlation id, timestamps, audit count, readback status, and no-mutation safety.
- Disallowed shallow implementation: Do not satisfy verification job proof with a model-only test that never invokes the job runner or facade.
- Failing-first test: `bundle://proof/SB07/transcripts/failing-first-source-assertion.txt`
- Passing test: `bundle://proof/SB07/transcripts/focused-integration.txt`
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs`; `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationJobRunner.cs`; `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationJobModel.cs`.
- Production assertions: `Process_readonly_verification_job_runner_SB07_INV_001` asserts lifecycle status, started/completed timestamps, process run id, step run id, audit id/count, readback status, `SchedulerWorkflowReadOnlyJob` contract, request identity, and no read-only safety violations.
- Red-team negative case: The forbidden-hook scan and integration assertions prove verification job readbacks remain manager-facade readbacks and do not bypass `IProcessManagerReadOnlyVerificationFacade.VerifyForReadbackAsync`.
- Downstream dependency check: SB08 release scans can treat scheduler/workflow verification as read-only evidence rather than process-driver execution approval.
