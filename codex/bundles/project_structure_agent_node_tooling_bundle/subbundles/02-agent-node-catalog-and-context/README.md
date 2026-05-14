# Agent Node Catalog And Context

## Status

- `Completed`

## Objective

- Give agents a default project-structure node catalog and selected-node context so they can create correct typed nodes and understand "selected nodes" prompts.

## Covered Inputs

- N002 work task node failure.
- N003 default tool catalog analysis.
- N005 selected-node context prerequisite.
- N007 dependency guidance for task creation.
- R002, R003, R004.

## Prerequisites

- Subbundle 01 is independent and may run before or after this subbundle.
- Prepared bundle validation has passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\ContextualAgentWorkspaceWindows.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\ContextualAgentWorkspaceModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureAgentContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureAgentService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Tools\MafAgentRuntime.ProjectStructureTools.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureCanvasCatalog.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureCanvasCatalog.RichDefinitions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectNodes\ProjectNodeKindRegistry.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\MafAgentRuntimeTests.cs`

## Deliverables

- Public node catalog response records.
- Agent service method and MAF tool for project-structure node catalog.
- MAF node-create/update/dependency descriptions explicitly mention `WorkItem/task` and dependency semantics.
- Contextual project-structure chat prompt and metadata include selected node IDs.

## Dependency Impact

- Critical foundation for subbundle 03 because selected-node transfer depends on selected IDs reaching the agent.
- Critical foundation for correct task node creation and later workbook recommendations.

## Validation Depth

- Critical foundation; service/integration and component-level proof.

## Implementation Steps

1. Expose node catalog DTOs and service response built from available catalog/type data.
2. Add `project_structure_node_catalog` internal MAF tool.
3. Update descriptions for task nodes and dependencies.
4. Pass selected node IDs from `ProjectStructurePage` to contextual agent windows.
5. Include selected IDs in prompt context and invocation metadata.
6. Add targeted tests.

## Scope Exceptions

- Do not implement every high-level scenario in the catalog phase.

## Do Not Do

- Do not manually invent project object types that are not in `ProjectObjectType`.
- Do not reintroduce the old ProjectStructure MCP.
- Do not hide selected-node context only in UI state.

## Acceptance Checklist

- Catalog includes `WorkItem` subtype `task`.
- Catalog includes typed project block, file, participant, runtime, infrastructure, and assurance categories.
- MAF tool list includes `project_structure_node_catalog`.
- Contextual prompt includes selected node IDs when present.

## Proof Required

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter MafAgentRuntimeTests`
- Targeted service/component tests for catalog and selected-node prompt.

## Proof Captured

- Unit catalog test passed and proves `WorkItem:task` catalog coverage.
- Component prompt test passed and proves selected-node IDs are present in project-structure prompt context.
- MAF integration test passed and proves default tool list includes catalog, dependency link/unlink, and selected-node subproject tools.

## Browser Validation Logging

- Route: `/projects/{projectId}/structure` if rendered proof is captured.
- Viewport: component-level proof acceptable unless browser app is already running.
- Assertions: selected node IDs are present in generated prompt/metadata.

## Progression Gate

- Subbundle 03 may start only after selected-node context and catalog tool tests pass or an explicit blocker is recorded.

## Suggested Agent Prompt

```text
Implement the node catalog and selected-node context foundation only. Prove WorkItem/task guidance and selected node IDs are available to agents before moving to selected-node mutation tooling.
```
