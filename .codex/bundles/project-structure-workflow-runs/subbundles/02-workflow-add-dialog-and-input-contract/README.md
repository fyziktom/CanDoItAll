# Workflow Add Dialog And Input Contract

## Status

- `Completed`

## Objective

- Build the typed input composition contract and add-dialog state model so the UI can show exactly what will be fed into a workflow before the workflow node is created.

## Success Criteria

- Input preview always includes project details and parent-node details.
- Optional selected input sources are typed and visible.
- Workflow selection validates against active workflow definitions.

## Covered Inputs

- `N005`, `N006`, `N007`, `N008`, `N020`
- `R003`, `R004`, `R005`, `R011`

## Prerequisites

- Subbundle 01 closure gate has passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.OverlayStates.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.Processes.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureCreateRequestComposer.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureAgentContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureAgentService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Catalog\WorkflowExampleCatalogSeedService.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageSimpleMutationTests.cs`

## Deliverables

- Workflow input settings and preview builder.
- Add workflow dialog state/options.
- Backend preview endpoint or service method usable by UI and tests.
- Tests for project/parent inclusion, optional files/folders, subtree summary, and manual JSON validation.

## Dependency Impact

- Start, UI, and scenario validation depend on the same input preview/composition logic. If this phase is wrong, workflows may run against missing or misleading context.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Add typed input settings for project, parent node, subtree, files/folders, and manual JSON.
2. Add preview composition that returns a readable summary and raw JSON.
3. Add workflow list/selection options from the workflow catalog.
4. Add validation for invalid manual JSON and unavailable workflow definitions.
5. Add unit/component tests for every required input shape.
6. Update execution report gate row.

## Scope Exceptions

- This subbundle does not render the final dialog; it builds the contract and state needed by UI.

## Do Not Do

- Do not start workflows.
- Do not create result nodes.
- Do not weaken the project/parent input invariant.

## Acceptance Checklist

- [x] Project id/title/status appears in every preview.
- [x] Parent node id/title/type/subtype/status/notes/metadata appears in every preview.
- [x] Folder path input can be represented in preview for SEAMARK.
- [x] Invalid manual JSON fails before node creation.

## Proof Required

- Focused tests for input preview/composition.
- `dotnet test CanDoItAll.slnx --filter "FullyQualifiedName~ProjectStructure"`

## Proof Captured

- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "ProjectStructureWorkflowNodeKeysTests|ProjectNodeKindRegistryTests|ProjectStructureNodeCatalogTests"` passed with 8 tests.
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "ProjectStructureAgentApi_creates_workflow_node_with_typed_metadata|ProjectStructureAgentApi_builds_workflow_input_preview_from_project_parent_and_sources|Api_openapi_exposes_focused_control_plane_routes"` passed with 3 tests.

## Implementation Notes

- Added `workflow-add-options` API contract for project-structure nodes.
- Added active workflow definition options. Draft/inactive workflows are visible but not selectable.
- Added typed preview sections and raw JSON input payload with project, parent node, selected nodes, optional parent subtree, additional sources, and manual JSON.
- Added shared input settings normalizer used by preview and node creation.

## Browser Validation Logging

- N/A for this contract phase. Browser proof is required in subbundle 04.

## Progression Gate

- Tests prove input preview/composition includes required project and parent details and supports folder/file/manual input modes.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
