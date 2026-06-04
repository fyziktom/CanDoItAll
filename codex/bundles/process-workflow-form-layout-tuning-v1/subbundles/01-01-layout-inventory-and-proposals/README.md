# 01-layout-inventory-and-proposals

## Status

- `Completed`

## Objective

- Ground the layout request in the real repo and create separate imagegen proposal artifacts for the affected process and workflow forms.

## Success Criteria

- Current-state analysis names the exact process and workflow components.
- Generated proposal images are copied into the bundle.
- Requirements and traceability map each raw note to downstream work.

## Covered Inputs

- `N001`
- `N002`
- `N003`
- `N004`
- `N005`

## Prerequisites

- none

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceStepsTab.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessStepEditorForm.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessStepBranchOutcomeEditor.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessStepRoleAssignmentEditor.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessArtifactExpectationEditor.razor`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor`
- `repo://src/CanDoItAll.AppComponents/Components/Tabs.razor`

## Deliverables

- `bundle://analysis/01-current-state.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://architecture/01-target-solution.md`
- `bundle://traceability/01-requirement-traceability.md`
- `bundle://evidence/imagegen-proposals/README.md`

## Dependency Impact

- Downstream UI implementation depends on this phase for the tab grouping and source inventory.
- If this inventory is wrong, the implementation could tune the wrong form surface or leave a requested form untouched.

## Validation Depth

- Planning and source-inventory validation.

## Implementation Steps

1. Inspect process and workflow source files.
2. Query the components MCP; if unavailable, ground component usage from local repo files.
3. Generate separate imagegen proposals for the main affected form types.
4. Copy selected proposal images into `bundle://evidence/imagegen-proposals/`.
5. Map raw notes to requirements and owning subbundles.

## Scope Exceptions

- Generated proposal images are not expected to be exact screenshots of the shipped app.

## Do Not Do

- Do not treat imagegen output as final UI proof.
- Do not edit product code in this phase.

## Acceptance Checklist

- [x] Raw request preserved.
- [x] Source inventory recorded.
- [x] Imagegen proposals copied into bundle evidence.
- [x] Requirements and traceability mapped.

## Proof Required

- Bundle file presence and prepared-stage validator.

## Browser Validation Logging

- N/A. This planning phase produces proposal artifacts, not rendered product UI.

## Progression Gate

- Product implementation may start after the prepared-stage bundle validator passes.

## Suggested Agent Prompt

```text
Validate the inventory and proposal mapping. Do not edit product code from this subbundle. If the source references or raw-note coverage are wrong, repair the bundle before implementation starts.
```
