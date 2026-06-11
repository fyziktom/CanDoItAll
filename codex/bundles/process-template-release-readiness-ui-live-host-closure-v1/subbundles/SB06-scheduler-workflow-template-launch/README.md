# SB06: Scheduler/workflow launch and read-only verification lifecycle

## Status
- Status: `Completed`

## Objective
Prove scheduler/workflow origins can start representative process runs and execute read-only verification jobs through process-owned paths.

## Covered Inputs
- REQ-006: Prove scheduler/workflow-origin process launch and read-only verification job lifecycle without driver hooks.

## Prerequisites
- SB05 must be completed with live proof passed or honestly skipped.
- Scheduler/workflow origin paths must use process-owned trigger APIs, not direct driver hooks.

## Exact Source References
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.TriggerStart.cs
- repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerService.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationJobRunner.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs

## Deliverables
- SchedulerPlan-origin representative template run proof.
- WorkflowRun-origin representative template run proof.
- Read-only verification job lifecycle proof tied to run/step provenance.
- Source scan proving no scheduler/workflow direct driver execution.

## Dependency Impact
- SB07 regression matrix relies on scheduler/workflow lifecycle proof.
- SB08 must reject merge readiness if direct driver hooks or mutation paths are present.

## Validation Depth
- Integration tests for trigger metadata, `start-run` and `dispatch-run-automation` outbox records, and verification job lifecycle.
- Negative proof for no mutation through verification job.
- Source scan for direct driver hooks.

## Implementation Steps
1. Start a representative template run from `SchedulerPlan`.
2. Start a representative template run from `WorkflowRun`.
3. Verify trigger metadata persists.
4. Verify `start-run` and `dispatch-run-automation` outbox records.
5. Run read-only verification job tied to the run/step.
6. Verify lifecycle status, provenance, audit id/hash, no-mutation flags.
7. Scan for direct driver hooks.

## Do Not Do
- Do not start providers or drivers directly from scheduler/workflow code.
- Do not mutate process state through verification job paths.

## Acceptance Checklist
- Process-owned path only.
- No scheduler/workflow direct driver execution.
- No mutation through verification job.

## Proof Required
- `bundle://proof/SB06/manifest.md`
- `bundle://proof/SB06/semantic-invariants.md`
- Passing transcript for scheduler/workflow trigger tests.
- Failing-first or adversarial transcript proving direct-driver or mutation behavior is rejected.
- Source scan transcript for driver-hook absence.

## Browser Validation Logging
- No browser proof required for SB06; execution report should record `N/A` outside browser analytics.

## Progression Gate
- SB07 may start only after scheduler/workflow-origin launch and read-only verification lifecycle pass without driver hooks.

## Suggested Agent Prompt
Implement only scheduler/workflow-origin process launch and verification lifecycle proof for SB06, record artifact-backed proof, then run the closure gate before SB07.
