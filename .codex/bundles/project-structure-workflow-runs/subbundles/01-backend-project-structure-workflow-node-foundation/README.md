# Backend Project-Structure Workflow Node Foundation

## Status

- `Completed`

## Objective

- Add the backend foundation for project-structure workflow nodes: typed node identity, workflow definition/run key helpers, metadata/input settings contracts, API/service entry points, and tests proving the app can identify and create workflow-linked nodes without UI involvement.

## Success Criteria

- Workflow node identity is strongly typed.
- Project-structure APIs can create/read workflow node metadata.
- Workflow definition/version references are validated against the workflow catalog.
- No UI code is required to compose or validate workflow node identity.

## Covered Inputs

- `N001`, `N002`, `N004`, `N020`
- `R001`, `R002`, `R011`

## Prerequisites

- Bundle readiness gate has passed.
- Current repo state has been checked for conflicting edits in project-structure or workflow files.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.SharedKernel\Projects\ProjectObjectContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureAgentContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureAgentService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureProcessNodeKeys.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureProcessNodeService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowCatalogContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\ProjectStructureAgentApi.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\ProjectStructureNodeCatalogTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs`

## Deliverables

- Workflow node key helper and typed contracts for workflow node settings/status.
- Project-structure agent/API support for workflow node create/read/start prerequisites.
- Tests proving valid and invalid workflow references are handled explicitly.

## Dependency Impact

- Subbundles 02-07 depend on this identity contract. If workflow id/version/input settings are stored as untyped metadata keys, UI input preview, start, status projection, and scenario proof become unreliable.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Decide whether to add explicit `ProjectObjectType.WorkflowDefinition`/`WorkflowRun` values or a typed workflow subtype under an existing compatible node type; document the decision in the execution report.
2. Add strongly typed workflow node keys and metadata/input settings records.
3. Add project-structure service methods for workflow node creation/reference validation.
4. Add API contract surface if needed for agents/automation to create workflow nodes.
5. Add targeted unit/integration tests for create/read/invalid workflow reference behavior.
6. Update execution report gate row.

## Scope Exceptions

- This subbundle does not start workflows and does not build UI dialogs.

## Do Not Do

- Do not add process staffing/matching logic.
- Do not implement browser UI.
- Do not add untyped JSON dictionary access when a record/enum/id wrapper is reasonable.

## Acceptance Checklist

- [x] Workflow node settings can round-trip through project-structure storage.
- [x] Invalid workflow id/version produces an explicit error.
- [x] Tests prove existing node catalog/process enum coverage remains intact.
- [x] Execution report notes the enum/subtype decision.

## Proof Required

- `dotnet test CanDoItAll.slnx --filter "FullyQualifiedName~ProjectStructure|FullyQualifiedName~Workflow"`
- Specific focused tests added for workflow node contracts.

## Proof Captured

- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "ProjectStructureWorkflowNodeKeysTests|ProjectNodeKindRegistryTests|ProjectStructureNodeCatalogTests"` passed with 8 tests.
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "ProjectStructureAgentApi_creates_workflow_node_with_typed_metadata"` passed with 1 test.
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "Api_openapi_exposes_focused_control_plane_routes|ProjectStructureAgentApi_creates_workflow_node_with_typed_metadata"` passed with 2 tests.

## Implementation Notes

- Decision: added explicit `ProjectObjectType.WorkflowDefinition` and `WorkflowRun` enum values. This matches the process node model and avoids a stringly typed workflow subtype hidden under prompt/process nodes.
- Added typed workflow id/run id node-key helpers.
- Added typed workflow input settings and workflow metadata envelope support.
- Added a project-structure API endpoint for creating a workflow-definition node under an existing parent node. The endpoint validates workflow id/version against the workflow catalog and rejects missing definitions explicitly.
- Added external binding support to project object creation so workflow nodes expose `ArtifactKind=workflow-definition`, `ArtifactId=<workflow id>`, and a route to the workflow workspace.

## Browser Validation Logging

- N/A. Backend-only phase.

## Progression Gate

- Backend tests pass and no downstream phase needs stringly typed workflow node metadata to proceed.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
