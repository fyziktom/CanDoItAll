# manager control plane and HR override

## Status

- `Completed`

## Objective

- Add default manager reporting, per-process manager override selection, HR matching integration, and explicit manager instructions.

## Covered Inputs

- Default AI managers must exist for running processes.
- Process definitions can override the manager agent.
- HR matching must automatically use the override during a process run.
- Users need manager-style reports and instructions for unblocking.

## Prerequisites

- `subbundles/01-architecture-source-of-truth-and-schema`
- `subbundles/02-runtime-subprocess-orchestration`
- Revalidation gate A passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Services\ProcessesService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Services\ProcessesService.Reads.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Launch\ProcessesService.Launch.Staffing.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessOperatorControlPlane.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessRuntimeViewModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsOperatorConsoleSection.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\Seeds\SandboxWorkspaceSeedBuilder.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs`

## Deliverables

- Manager override is respected during launch staffing.
- Run snapshots include manager agent id/name.
- Manager report projection summarizes run tree state, blockers, failures, stale work, and next actions.
- Manager instructions are persisted through the existing control plane/journal path.
- UI surfaces show manager report and instruction affordance.

## Dependency Impact

- Real-world validation depends on manager reporting to understand process trees.

## Validation Depth

- `Process-critical closure`

## Implementation Steps

1. Trace existing HR matching and process role assignment paths.
2. Apply manager override during manager candidate selection.
3. Snapshot selected manager on run start.
4. Implement deterministic manager report projection over root/child runs.
5. Persist manager instructions through existing runtime control-plane records.
6. Add targeted tests.

## Scope Exceptions

- Do not build a full autonomous manager agent workflow unless existing AgentFramework contracts make it minimal.
- Do not add a new chat product surface.

## Do Not Do

- Do not match manager overrides by display name only.
- Do not hide failed manager instruction persistence.
- Do not leak sensitive prompt or credential data into reports.

## Acceptance Checklist

- Manager override wins over default manager candidate where configured.
- Report includes active child runs and blockers.
- Instruction is persisted and visible in runtime history/control plane.
- Missing override agent fails or degrades explicitly according to existing staffing rules.

## Proof Required

- Targeted staffing/report tests.
- Integration test or scenario note proving manager report over parent-plus-child run.
- Execution report update.

## Browser Validation Logging

- Target route or window: process run operator console.
- Required viewport passes: desktop.
- Required actions/assertions: show manager report and submit an instruction.
- Screenshot evidence: `process-manager-report-desktop.png`.
- Review questions: Are blockers visible without reading every child step? Is the selected manager clear?

## Progression Gate

- Continue only when manager override is strongly typed and reports derive from runtime hierarchy.

## Suggested Agent Prompt

```text
Implement manager override and reporting only. Use agent ids plus snapshots, honor override in HR matching, and persist manager instructions explicitly.
```
