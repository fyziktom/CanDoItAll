# SB07: Scheduler/workflow launch + read-only verification jobs

## Status
Prepared.

## Objective
Prove scheduler/workflow-origin process launch and read-only verification job lifecycle without direct driver hooks.

## Exact Source References
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.TriggerStart.cs
- repo://src/CanDoItAll.Modules.SchedulerPlanner
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationJobModel.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationJobRunner.cs
- repo://tests/CanDoItAll.Tests.Integration

## Deliverables
- Add/strengthen scheduler-origin template launch test for a representative process.
- Add/strengthen workflow-origin launch test or workflow-origin trigger proof.
- Add read-only verification job lifecycle result with source kind/reference/correlation id/start/end/readback status.
- Verify scheduler/workflow paths use process service/facade, not driver execution hooks.

## Do Not Do
- Do not add scheduler/workflow driver runtime hooks.
- Do not call domain drivers directly from scheduler/workflow.
- Do not add hosted execution-capable driver worker.

## Acceptance Checklist
- Scheduler-origin run starts through `StartRunFromTriggerAsync` or equivalent process-owned path.
- Workflow-origin run starts through process-owned path.
- Read-only verification job runner records lifecycle and readback.
- No driver mutation hooks are introduced.

## Proof Required
- Focused integration transcript.
- Source scan for forbidden scheduler/workflow driver hook patterns.

## Browser Validation Logging
N/A.

## Progression Gate
SB08 may proceed after scheduler/workflow process paths are proven without driver side effects.
