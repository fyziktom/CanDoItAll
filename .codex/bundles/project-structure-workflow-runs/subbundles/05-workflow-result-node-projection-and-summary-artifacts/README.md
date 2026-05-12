# Workflow Result Node Projection And Summary Artifacts

## Status

- `Completed`

## Objective

- Ensure workflow outputs, project-structure executor-created nodes, assets, and file operations are projected under the workflow node with a visible execution summary.

## Success Criteria

- Workflow-created project nodes default to the workflow node as parent.
- Execution summary is visible in project structure.
- Summary lists created nodes/assets and file paths, including paths not represented as asset nodes.

## Covered Inputs

- `N016`, `N017`, `N018`, `N019`
- `R009`, `R010`

## Prerequisites

- Subbundle 03 closure gate has passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Tools\MafAgentRuntime.ProjectStructureTools.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowExecutorContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowExecutorModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureAgentService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureAssemblyService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workspace\Files\WorkspaceFileContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Persistence\PersistentWorkflowStores.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessWorkflowExecutorIntegrationTests.cs`

## Deliverables

- Result parent context propagated into project-structure executor or projection bridge.
- Execution summary storage/projection.
- File path collection for workflow file operations.
- Tests for result node parentage and summary content.

## Dependency Impact

- Real-world scenarios depend on visible summaries and correct child node placement. If this phase is weak, final proof cannot show users where workflow results went.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Trace existing project-structure workflow executor behavior.
2. Add workflow-node parent context to result node creation.
3. Add summary projection model and persistence strategy.
4. Capture file paths from file-writing workflow artifacts/executors.
5. Add tests for child node parentage and summary path content.
6. Update execution report gate row.

## Scope Exceptions

- This subbundle does not create the full 20 scenario catalog, but it must include enough tests to support those scenarios later.

## Do Not Do

- Do not create result nodes under the original parent by default.
- Do not store only opaque JSON if the UI/API cannot display the summary.
- Do not drop file paths because an asset node was not created.

## Acceptance Checklist

- New project-structure nodes created during workflow execution have workflow node as parent.
- Summary includes run id, state, steps, created node ids, created asset ids, and file paths.
- Summary is accessible from project-structure read/API surface.

## Proof Required

- Focused unit/integration tests for result projection.
- `dotnet test CanDoItAll.slnx --filter "FullyQualifiedName~WorkflowExecutor|FullyQualifiedName~ProjectStructure"`

## Browser Validation Logging

- N/A for this backend/projection phase. Browser proof occurs in subbundle 07 after real runs.

## Progression Gate

- `Passed`: tests prove correct parentage, summary node/asset ids, and summary file path content.

## Implementation Notes

- Project-structure-started workflow input now includes `runContext` with workflow node id/title, requested-by value, and the active agent identity.
- The project-structure workflow executor resolves omitted project id from `$.project.id`, defaults created asset parent to `$.runContext.workflowNodeId`, and uses the inherited agent context so nested canvas mutations share the current lease owner.
- Workflow-node status summaries now persist and expose created node ids, created asset ids, and file paths.
- The in-process backend records configured file artifacts for completed storage-file and spreadsheet write operations.
- The selection floating window renders created node ids, asset ids, and file paths from the persisted summary.

## Closure Evidence

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "MafBackendRecordsConfiguredFileArtifactsForCompletedFileWrites|MafCompilerInvokesExecutorNodeThroughInvoker" /p:BuildInParallel=false` passed: 2 tests.
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "ProjectStructureAgentApi_projects_workflow_created_assets_under_workflow_node|ProjectStructureAgentApi_starts_workflow_node_and_updates_summary" /p:BuildInParallel=false` passed: 2 tests.
- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "ProjectStructureActionCatalogAdapterTests|ProjectStructurePageTests" /p:BuildInParallel=false` passed: 53 tests.
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "ProjectStructureAgentApi_creates_workflow_node_with_typed_metadata|ProjectStructureAgentApi_builds_workflow_input_preview_from_project_parent_and_sources|ProjectStructureAgentApi_starts_workflow_node_and_updates_summary|ProjectStructureAgentApi_projects_workflow_created_assets_under_workflow_node|ProjectStructureAgentApi_marks_workflow_node_waiting_cancelled_and_failed_states|ProjectStructureAgentApi_rejects_workflow_start_from_non_workflow_node|Api_openapi_exposes_focused_control_plane_routes" /p:BuildInParallel=false` passed: 7 tests.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
