# Runtime Branch Orchestration And MCP Contracts

## Status

- `Completed`

## Objective

- Make runtime flow branch-capable by activating the correct path from a selected outcome, resolving non-selected mutually exclusive branches deterministically, and exposing the needed branch metadata through the runtime and MCP contracts.

## Covered Inputs

- `U003` Decision node support with explicit routing ownership.
- `U004` Multi-outcome switch behavior.
- `A002` Legacy audit finding that runtime progression still uses sequence ordering.
- `U005` Real validation and execution, not paper closure.

## Prerequisites

- `subbundles/02-branch-definition-model-and-publish-guardrails` closure gate passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRuntimeModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Reads.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Processes\ProcessesCoordinator.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Processes\ProcessesTools.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Processes\ProcessToolModels.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests\ProcessesToolsTests.cs`

## Deliverables

- Runtime transition request updated to carry selected branch outcome data when required.
- Root-step activation based on actual graph roots instead of only sequence order.
- Completion logic that activates selected downstream paths and resolves non-selected branch paths deterministically.
- Read model and MCP support for branch-capable step execution.
- Integration and MCP tests for multi-path routing.

## Dependency Impact

- The UI cannot prove branching honestly until runtime exposes branch choices and selected-path behavior correctly.
- Weak proof here would make browser proof theatrical because the UI could select outcomes that runtime ignores.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Extend the runtime request and read models with the branch metadata needed for safe routing.
2. Replace sequence-only activation with dependency-aware activation.
3. Require explicit outcome selection when a step has conditional routes.
4. Resolve non-selected mutually exclusive branches so run completion stays deterministic.
5. Update MCP coordinator and tool tests for the new contract.
6. Add integration coverage for a real multi-outcome path.

## Scope Exceptions

- This phase does not own the workspace form controls or browser-proof workflow.

## Do Not Do

- Do not leave non-selected branch steps pending forever.
- Do not keep runtime graph behavior hidden behind sequence-only reads.
- Do not add UI-only routing behavior that the MCP contract cannot express.

## Acceptance Checklist

- Runtime requires a selected branch outcome when the completed step has conditional routes.
- The selected branch activates the correct downstream step or steps.
- Non-selected mutually exclusive branch steps are resolved deterministically.
- MCP or runtime read data exposes the branch metadata needed by callers.

## Proof Required

- Targeted runtime integration tests for multi-path execution.
- MCP contract tests for the updated step transition surface.
- Build or test confirmation for the affected process and MCP projects.
- One dependent-flow smoke showing downstream trust before UI work starts.

## Browser Validation Logging

- N/A. This phase establishes behavior and contracts. Browser-visible proof is recorded in subbundle 04.

## Progression Gate

- Runtime and MCP validation pass, and at least one dependent-flow smoke confirms downstream work can trust the branch behavior before subbundle 04 starts.

## Suggested Agent Prompt

```text
Implement only the runtime and MCP branch contract. Require explicit branch selection where needed, activate the correct path, resolve non-selected branches deterministically, and prove it with integration and MCP tests before UI work begins.
```
