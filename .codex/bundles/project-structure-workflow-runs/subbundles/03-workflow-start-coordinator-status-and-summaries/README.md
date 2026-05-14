# Workflow Start Coordinator Status And Summaries

## Status

- `Completed`

## Objective

- Implement the backend coordinator that starts a workflow from a project-structure workflow node, persists the run linkage, updates progress/status/markers, and builds the execution-summary model.

## Success Criteria

- Start request composes input from the stored workflow-node settings.
- Node progress changes to started/running and then completed/failed/cancelled/waiting as the run changes.
- Step index/count can be derived and returned for selection status.
- Execution summary model is available for projection in subbundle 05.

## Covered Inputs

- `N009`, `N010`, `N011`, `N012`, `N013`, `N015`, `N020`
- `R001`, `R006`, `R007`, `R008`, `R010`, `R011`

## Prerequisites

- Subbundle 01 and 02 closure gates have passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowRuntimeManager.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\WorkflowsApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Persistence\PersistentWorkflowStores.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureProcessRunSyncBridge.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureProcessNodeService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureAgentService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\ProjectStructureAgentApi.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs`

## Deliverables

- Project-structure workflow start service/API.
- Run linkage from workflow node to workflow run id.
- Status/progress/marker mapping.
- Step progress derivation from graph/events.
- Execution summary DTO/model ready for project-structure projection.

## Dependency Impact

- UI selection status and final scenario proof depend on this backend state. Weak proof here invalidates browser screenshots because UI would only be displaying wrong or transient state.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Add workflow start input/result contracts for project-structure workflow nodes.
2. Compose input JSON from the workflow-node input settings and current project/parent node state.
3. Call `IWorkflowRuntimeManager.StartAsync` with explicit backend behavior.
4. Persist run linkage and update workflow node status/progress/markers.
5. Add summary/status read model for selection panel.
6. Add tests for running, completed, failed, cancelled, and waiting states.
7. Update execution report gate row.

## Scope Exceptions

- This subbundle creates backend summary/status data but does not render UI and does not create final summary child nodes.

## Do Not Do

- Do not add process staffing or HR matching.
- Do not silently retry with a different backend/provider.
- Do not put status derivation in Razor components.

## Acceptance Checklist

- `[x]` Start from invalid node returns explicit error.
- `[x]` Start from valid workflow node returns run id.
- `[x]` Running state sets started/progress.
- `[x]` Completed state sets 100 percent.
- `[x]` Failed/cancelled/waiting states set appropriate markers/status.
- `[x]` Step count is nonzero for a nontrivial workflow graph.

## Proof Required

- Focused backend tests for project-structure workflow start/status.
- `dotnet test CanDoItAll.slnx --filter "FullyQualifiedName~Workflow|FullyQualifiedName~ProjectStructure"`

## Browser Validation Logging

- N/A. Browser proof is required after UI wiring in subbundle 04.

## Progression Gate

- `Passed`
- Backend tests prove start/status/step/summary behavior without UI.
- Focused integration proof covered completed run summary, file artifact paths, waiting state, cancellation refresh, failed unavailable-backend start, invalid non-workflow start, and OpenAPI route exposure.
- Solution-level `Workflow|ProjectStructure` filter reached the relevant unit/integration/component suites successfully, but the full command returned failure because an existing Playwright process audit test timed out waiting for `processes-launch-name-input`; this is outside this backend slice and remains a UI/browser residual for later validation.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
