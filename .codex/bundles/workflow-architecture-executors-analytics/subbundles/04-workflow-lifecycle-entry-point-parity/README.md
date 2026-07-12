# Workflow Lifecycle Entry-Point Parity

## Status

- `Completed`

## Objective

- Establish one launch policy and authoritative incremental lifecycle for API/UI, scheduler, project structure, generic agent tools, and process workflow execution.

## Success Criteria

- A typed launch intent/origin contract replaces caller-specific nullable source fields and duplicated validation.
- Production starts consistently require an active definition and an explicitly supported backend; no silent InProcess fallback.
- Accepted/Running and started progress are persisted before backend completion; terminal/cancel/failure transitions are explicit.
- Generic agent start/status/cancel/external-response tools use the same application service.
- Process Workflow executor resolves a typed workflow/version policy, waits/resumes exactly once, and persists lineage.

## Covered Inputs

- WF-LIFE-01, WF-LIFE-02, and all launch-path notes.
- Completion-first persistence, unused process bridge, missing generic tool, and silent backend fallback findings.

## Prerequisites

- SB01 active contracts and composition gate passes.
- Current API/scheduler/project/process behavior has characterization tests.

## Exact Source References

- `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Runtime/WorkflowRuntimeManager.cs`
- `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Core/WorkflowLaunchService.cs`
- `repo://src/App/CanDoItAll.Web/Api/WorkflowsApi.cs`
- `repo://src/Modules/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerService.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureWorkflowNodeService.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessLaunchExecutorResolver.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessLaunchContracts.cs`

## Deliverables

- Typed launch intent/origin and `IWorkflowLaunchService` application contract/implementation.
- Migrated API, scheduler, project-node, and preview callers.
- Generic governed workflow agent tools for start/status/cancel/external response.
- Real Process Workflow driver with selection, lineage, waiting/resume, failure/cancel, and idempotency.
- Incremental run/event persistence and in-process active cancellation registry with honest capability reporting.
- Lifecycle/source lineage persistence and migrations where required.

## Dependency Impact

- SB05 needs stable run/node/origin correlation and lifecycle timestamps.
- SB07 requires each entry point to use the same policy and lifecycle producer.
- Process analytics must avoid double-counting child workflow usage.

## Validation Depth

- `Process-critical closure` with controllable-backend concurrency tests and entry-point integration matrix.

## C# Architecture Impact

- Adds a real application boundary above runtime and replaces a nominal unused bridge with adapters that have consumers.

## Boundary Ownership

- Launch service owns definition/version/status/backend/input policy. Caller adapters own origin construction and authorization. Runtime owns state transitions/backend coordination.

## Dependency Direction

- API/modules/process adapters depend on Workflows.Abstractions/Core. Workflows.Core must not depend on process/scheduler/workbench modules.

## Pattern Decision

- Use PSR-03 Application Launch Service and PSR-06 Lifecycle State Machine. Avoid nullable-source accumulation and backend fallback.

## Testability Contract

- Fake catalog/runtime/backends and `TimeProvider` prove policy and transition order.
- A controllable backend must block while tests query Running/progress, then complete/fail/cancel deterministically.

## Partial Class Policy

- New launch/lifecycle behavior lives in cohesive services, not page/process partial files. Existing UI partials only orchestrate adapters.

## Architecture Proof Required

- Producer/consumer matrix for every origin, transition-order tests, real process driver consumption, and no direct runtime starts left in scoped callers except test infrastructure.

## Implementation Steps

1. Add failing launch-policy/lifecycle/process-resolution tests.
2. Introduce typed intent/origin and common launch service; migrate API/scheduler/project callers.
3. Persist Accepted/Running/start event before backend invocation and terminal transition after it.
4. Add explicit in-process cancellation tracking and backend capability errors.
5. Add generic agent tools over the launch/query boundary.
6. Implement process workflow selection/driver/wait-resume/lineage and idempotency.
7. Run entry-point integration matrix and persistence migration tests.

## Scope Exceptions

- True durable resume requires a durable backend and is not claimed for InProcess. External response must report unsupported resume rather than fake completion if backend cannot resume.

## Do Not Do

- Do not silently switch requested backends.
- Do not mark external response as completed without backend continuation.
- Do not duplicate launch validation in each adapter.

## Acceptance Checklist

- Running state visible before completion.
- Crash/failure preserves accepted run and prior progress.
- All five origins use launch service with lineage.
- Process assignment waits/resumes once and handles failure/cancel.
- Focused concurrency/integration/build tests pass.

## Proof Required

- Failing-first completion-first/process-rejection transcripts.
- Passing controllable-backend and entry-path matrix transcripts.
- Negative backend-fallback, ambiguous workflow, double-resume, and unsupported-resume proof.
- `bundle://proof/SB04/manifest.md` and `bundle://proof/SB04/semantic-invariants.md` during execution.

## Browser Validation Logging

- `N/A: process/project UI behavior is integration-tested here; workflow large-screen UI proof occurs in SB06/SB07.`

## Progression Gate

- Passed. Lifecycle transition order, all launch origins, process lineage/idempotency, cancellation, failure, persistence, and build proof are recorded in `bundle://proof/SB04/manifest.md`.

## Suggested Agent Prompt

```text
Implement SB04 only. Create one typed launch policy, persist lifecycle before/during/after execution, and make agent/process entry paths real consumers. Prove transition order and reject silent backend or resume fallbacks.
```
