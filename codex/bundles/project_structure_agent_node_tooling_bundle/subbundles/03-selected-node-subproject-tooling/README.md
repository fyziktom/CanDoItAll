# Selected Node Subproject Tooling

## Status

- `Completed`

## Objective

- Add a one-call workflow that creates a named subproject under the current project and moves selected nodes, with descendants, into the new subproject while preserving valid parentage and internal dependencies.

## Covered Inputs

- N004 complex node task support.
- N005 selected nodes to own new subproject.
- N006 parent connection assurance.
- N007 dependency preservation.
- R005, R006, R007.

## Prerequisites

- `02-agent-node-catalog-and-context` closure gate passed.
- Selected-node IDs are available in contextual prompts.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureAgentContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureAgentService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Workbench\ProjectWorkbenchModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Workbench\ProjectWorkbenchCrossModuleMutationService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Workbench\ProjectWorkbenchCrossModuleMutations.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CrossModule\ProjectCrossModuleMutationProcessor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\ProjectStructureAgentApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Tools\MafAgentRuntime.ProjectStructureTools.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectStructureAgentApiIntegrationTests.cs`

## Deliverables

- Service method for moving selected nodes to a new subproject.
- Workbench selected-node move support that includes descendants by default.
- HTTP API endpoint and MAF internal tool for the workflow.
- Tests proving parentage and dependency preservation.

## Dependency Impact

- Critical foundation for the user's complex agent workflow.
- Workbook scenario rankings should reference this as a shipped one-call tool.

## Validation Depth

- Critical service/API/integration proof.

## Implementation Steps

1. Add request/result records for selected-node subproject transfer.
2. Add workbench method to move arbitrary selected editable nodes to a target project.
3. Add agent service method that creates and links the subproject, then moves selected nodes.
4. Add HTTP endpoint and MAF tool.
5. Add integration tests for selected parent/child combinations and internal dependency preservation.
6. Query target dependencies after transfer in proof.

## Scope Exceptions

- Exact canvas placement optimization for moved nodes is not required in this subbundle.

## Do Not Do

- Do not move system-managed projection nodes.
- Do not leave moved root nodes parented to a source-project node.
- Do not preserve cross-project links that the current graph model cannot validate.

## Acceptance Checklist

- New subproject is created and linked under the source project.
- Selected nodes and descendants move to the target project.
- Moved roots use `project:{targetProjectId}` as parent.
- Moved children retain moved parents.
- Internal `DependsOn` links remain in the target project.
- Source project no longer contains moved nodes.

## Proof Required

- Targeted integration/API tests for selected-node subproject transfer.
- Dependency readback from target project.

## Proof Captured

- `ProjectStructureAgentIntegrationTests.AgentService_MoveNodesToNewSubprojectAsync_creates_subproject_and_preserves_dependency_links` passed.
- `ProjectStructureAgentIntegrationTests.AgentService_MoveNodesToNewSubprojectAsync_without_descendants_reparents_left_behind_children` passed.
- The tests verify new child project linkage, moved root parentage, moved descendant parentage, source removal, target `DependsOn` preservation, and non-orphaning behavior when descendants are left behind.

## Browser Validation Logging

- N/A for direct UI rendering; contextual selected IDs were covered in subbundle 02.

## Progression Gate

- Do not start workbook closure until selected-node transfer tests prove parentage and dependency behavior.

## Suggested Agent Prompt

```text
Implement selected-node-to-new-subproject tooling only. Use existing lease and mutation patterns, include descendants by default, preserve internal dependencies, remove invalid cross-project links, and prove target parentage by readback tests.
```
