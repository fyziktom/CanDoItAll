# 01-architecture-map-and-mock-seam

## Status

- `Completed`

## Objective

- Confirm the current process execution core, agent runtime boundary, role projection path, and the smallest safe seam for deterministic process mock agents.

## Covered Inputs

- R8 existing process transitions.
- R9 narrow implementation.
- User request to start from a detailed architecture mapping.

## Prerequisites

- Prepared bundle documents exist.
- Code analytics snapshot `snap-20260425122111-a516befc` is available as supporting evidence.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRunAutomationDispatchService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.RunStart.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.StepTransitions.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRuntimeProgressionPlanner.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Contracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\AgentFrameworkWorkspaceFactory.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\AiTechnicalAgentBridge.cs

## Deliverables

- Architecture notes proving the mock behavior belongs in an `IAgentRuntime` decorator.
- Explicit list of files that later subbundles may edit.
- Decision that `ProcessRunAutomationDispatchService` should remain production-path orchestration, not a mock switch.

## Dependency Impact

- Subbundle 02 depends on this seam to avoid special-casing the process dispatcher.
- Subbundle 03 depends on branch outcome and artifact projection behavior being exercised through existing services.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Review the process run start, step transition, outbox, and progression planner paths.
2. Review the AgentFramework execution service and runtime contract.
3. Review scenario harness wiring and identify reusable patterns.
4. Record the final seam and downstream implementation boundaries.

## Scope Exceptions

- No runtime code is required in this subbundle unless the map reveals a missing compile-time reference needed by later work.

## Do Not Do

- Do not add mock process logic to `ProcessRunAutomationDispatchService`.
- Do not create process definitions or UI affordances in this subbundle.
- Do not call real LLM providers.

## Acceptance Checklist

- The runtime seam is documented.
- Process dispatcher responsibilities are preserved.
- Agent role projection path is documented.
- Later subbundles have clear source ownership.

## Proof Required

- Updated `analysis/01-current-state.md`.
- Updated `architecture/01-target-solution.md`.
- Prepared bundle validation passes.

## Browser Validation Logging

- N/A: backend architecture subbundle with no browser-visible change.

## Progression Gate

- Downstream implementation may continue only after the bundle validator passes for prepared stage and the target seam remains `IAgentRuntime`.

## Suggested Agent Prompt

```text
Implement subbundle 01 only. Confirm the process runtime and agent runtime seam, update bundle architecture notes, and do not change production process dispatch behavior.
```
