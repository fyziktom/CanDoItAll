# 02-process-step-form-tabs

## Status

- `Completed`

## Objective

- Reorganize Processes Steps setup forms so the long process step editor is split into intent-level tabs and child setup editors remain compact inside those tabs.

## Success Criteria

- `ProcessStepEditorForm.razor` no longer renders identity, execution, contracts, routing, roles, and artifacts in one long uninterrupted stack.
- Existing add/remove callbacks for branch outcomes, role assignments, and artifact expectations still flow through the parent component.
- `ProcessStepRoleAssignmentEditor.razor` and `ProcessArtifactExpectationEditor.razor` remain compact repeated-card forms.
- No service, persistence, runtime, or editor model changes are introduced.

## Covered Inputs

- `N001`
- `N003`
- `N005`

## Prerequisites

- `subbundles/01-01-layout-inventory-and-proposals` completed.
- Prepared-stage bundle validator passed.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceStepsTab.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessStepEditorForm.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessStepBranchOutcomeEditor.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessStepRoleAssignmentEditor.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessArtifactExpectationEditor.razor`

## Deliverables

- Tabbed process step form sections for Basic info, Execution, Contracts, Routing, Roles, and Artifacts or equivalent names.
- Compact role and artifact child editors if needed for scanability.
- Source assertions and browser proof recorded in `reviews/01-execution-report.md`.

## Dependency Impact

- Final browser proof and raw-note closure depend on this phase.
- Weak proof here would invalidate claims that the Processes Steps setup form was fixed.

## Validation Depth

- UI layout, module build, and browser-proof validation.

## Implementation Steps

1. Add component-local selected-tab state to `ProcessStepEditorForm.razor`.
2. Wrap the form body after the optional header in shared `Tabs`.
3. Move existing fields into intent-level tab panels without changing bindings or event handlers.
4. Keep child editor components responsible for branch outcomes, role assignments, and artifacts.
5. Compact child editor grids only where the proposal and real markup show avoidable vertical waste.
6. Preserve existing test IDs.

## Scope Exceptions

- The visual result does not need to match imagegen pixels exactly.

## Do Not Do

- Do not add custom styling beyond minimal layout mechanics.
- Do not change process editor models, persistence entities, or runtime services.
- Do not remove existing fields or callbacks.

## Acceptance Checklist

- [x] Step form uses shared tabs.
- [x] Basic, execution, contract, routing, role, and artifact controls remain reachable.
- [x] Child editors remain separate components.
- [x] `CanDoItAll.Modules.Processes` builds.
- [x] Browser proof captures the Steps form at desktop and narrow widths.

## Proof Required

- Build transcript for `src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj`.
- Source assertion transcript proving `ProcessStepEditorForm.razor` uses shared tabs and retains child editors.
- Browser screenshots for `/processes` Steps tab at desktop and narrow widths.

## Browser Validation Logging

- Route: `/processes`.
- Viewports: `1600x900` and `390x844`.
- Actions: open page, select or confirm the Steps tab, inspect one step editor, switch each step-form tab.
- Assertions: tab labels visible, representative fields visible in each tab, action buttons reachable, no incoherent overlap or avoidable lateral overflow.
- Screenshots: `bundle://proof/SB04/browser/processes-steps-desktop-basic.png`, `bundle://proof/SB04/browser/processes-steps-desktop-roles.png`, `bundle://proof/SB04/browser/processes-steps-desktop-artifacts.png`, `bundle://proof/SB04/browser/processes-steps-narrow-basic.png`.

## Progression Gate

- `SB03` may proceed independently after prepared validation, but final closure cannot proceed until this subbundle builds and its browser proof is recorded.

## Suggested Agent Prompt

```text
Implement this subbundle only. Use existing shared components, keep behavior unchanged, preserve all process step fields and callbacks, and update the execution report with source and browser proof.
```
