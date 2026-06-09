# SB039 Proof Manifest

Status: Passed.

## Scope

Gate M covers `P13: Scheduler and workflow launch readiness`.

The source change is bounded to process-start readiness:

- existing scheduler/planner/workflow trigger surfaces were inventoried from source;
- `ProcessesService.StartRunFromTriggerAsync` was added as a typed manual/test trigger path that validates trigger source identity and requester audit text before delegating to `StartRunAsync`;
- scheduler process targets now use the typed trigger path;
- focused tests prove scheduled and workflow-origin starts create normal process runtime rows without workflow-run links or driver-runtime hooks.

No generic runtime driver host, driver registry, runtime selector, driver DI registration, manager command, workflow executor hook into process creation, shell execution, Office/Graph call, workspace/storage write, transition mutation shortcut, finalizer mutation shortcut, claim mutation, UI change, or mobile/small-screen proof was introduced.

## Command Transcripts

- `bundle://proof/SB037/transcripts/scheduler-workflow-process-trigger-inventory.txt`
- `bundle://proof/SB038/transcripts/safe-trigger-path-source-assertions.txt`
- `bundle://proof/SB039/transcripts/focused-scheduler-workflow-launch-tests.txt`
- `bundle://proof/SB039/transcripts/anti-stub-scheduler-workflow-trigger-negative-proof.txt`
- `bundle://proof/SB039/transcripts/forbidden-drift-scan.txt`
- `bundle://proof/SB039/transcripts/prepared-validator-after-sb039.txt`
- `bundle://proof/SB039/transcripts/changed-file-hashes.txt`

## Source Assertions

- SB037 inventory proves the existing scheduler process target path is `ISchedulerTargetLauncher` -> process service start, while workflow assignment coordination remains inside process dispatch and does not start new process runs.
- SB038 source assertions prove `ProcessRunTriggerSourceKind`, `ProcessRunTriggerStartRequest`, and `StartRunFromTriggerAsync` are strongly typed and validation-backed.
- Scheduler process launch now passes `ProcessRunTriggerSourceKind.SchedulerPlan`, source id/name, and `scheduler-planner` requester through the process service wrapper.

## Test Proof

`dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~Scheduler_and_workflow_trigger_start_paths_use_process_service_without_driver_runtime_hooks"` passed with 1 test.

`dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~StartRunFromTriggerAsync_SB038|FullyQualifiedName~Target_launcher_starts_real_process_run"` passed with 3 tests:

- `StartRunFromTriggerAsync_SB038_INV_001_starts_workflow_origin_process_without_runtime_driver_hook`
- `StartRunFromTriggerAsync_SB038_INV_002_rejects_workflow_trigger_without_source_identity`
- `Target_launcher_starts_real_process_run`

## Anti-Stub And Adversarial Proof

- The negative proof reruns the source-identity rejection test and the architecture guard.
- The workflow-origin positive test asserts persisted process rows, step rows, start/dispatch outbox records, no workflow links, no execution runs, and trigger audit text containing source kind, source id, and requester.
- The architecture guard rejects driver host/registry/selector and manager-command tokens in the trigger path.

## Forbidden Drift

`bundle://proof/SB039/transcripts/forbidden-drift-scan.txt` confirms:

- no transient bundle-path dependency in scoped P13 source/tests;
- no runtime-host, driver registry, selector, driver DI, or manager-command drift in P13 trigger-production files;
- the process trigger wrapper does not depend on workflow runtime managers, Quartz, hosted services, or process workflow coordinator;
- no UI/media files were touched for P13.

## Changed-File Hashes

See `bundle://proof/SB039/transcripts/changed-file-hashes.txt`.

## Production Behavior Artifact Matrix

| Signal | Producer | Consumer | Behavior |
| --- | --- | --- | --- |
| `ProcessRunTriggerSourceKind.SchedulerPlan` | Scheduler process target launch | `ProcessesService.StartRunFromTriggerAsync` | Starts a normal process run through `StartRunAsync` with scheduler source audit text. |
| `ProcessRunTriggerSourceKind.WorkflowRun` | Manual/test workflow-origin request | `ProcessesService.StartRunFromTriggerAsync` | Starts a normal process run through `StartRunAsync` only when source id and requester are supplied. |
| Trigger wrapper validation errors | `ProcessesService.StartRunFromTriggerAsync` | Caller/test | Rejects missing source id/requester before runtime rows are created. |

## Downstream Dependency Check

SB040-SB042 can evaluate the runtime-host roadmap with scheduler/workflow process launch readiness proven and still blocked from generic driver runtime hooks.
