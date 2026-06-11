# SB06: Scheduler/workflow launch and read-only verification lifecycle

## Objective
Prove scheduler/workflow origins can start representative process runs and execute read-only verification jobs through process-owned paths.

## Exact source references
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.TriggerStart.cs
- repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerService.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationJobRunner.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs

## Implementation steps
1. Start a representative template run from `SchedulerPlan`.
2. Start a representative template run from `WorkflowRun`.
3. Verify trigger metadata persists.
4. Verify `start-run` and `dispatch-run-automation` outbox records.
5. Run read-only verification job tied to the run/step.
6. Verify lifecycle status, provenance, audit id/hash, no-mutation flags.
7. Scan for direct driver hooks.

## Acceptance checklist
- Process-owned path only.
- No scheduler/workflow direct driver execution.
- No mutation through verification job.
