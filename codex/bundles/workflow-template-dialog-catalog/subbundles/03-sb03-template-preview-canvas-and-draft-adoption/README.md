# SB03 Template Preview Canvas And Draft Adoption

## Status

- `Completed`

## Objective

- Add a separate preview dialog that shows the selected template as a workflow canvas and lets users add the template to drafts with deterministic conflict-prefixed names.

## Success Criteria

- Preview from the catalogue opens a separate dialog.
- The dialog shows a canvas visualization of the workflow and selected template metadata.
- "Add to my drafts" saves a draft workflow.
- Name collisions resolve to `01 <base>`, `02 <base>`, etc.

## Covered Inputs

- `N005`, `N006`, `N007`

## Prerequisites

- SB02 closure gate passed.
- Catalogue dialog can load and select templates.
- Proposal artifact `bundle://evidence/design/template-preview-dialog-proposal.png` exists.

## Exact Source References

- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor.cs`
- `repo://src/CanDoItAll.AgentFramework.Workflows.Templates/WorkflowTemplatePack.cs`
- `repo://tests/CanDoItAll.Tests.Components/WorkflowsPageTests.cs`

## Deliverables

- Preview dialog layout modeled on the generated preview proposal.
- Template-to-transient-definition preview materialization.
- Add-to-drafts action that persists a draft definition and required component.
- Deterministic draft naming helper and tests.

## Dependency Impact

- SB04 final browser proof depends on this dialog’s open state and adoption behavior.
- Weak naming proof risks user-visible duplicate workflow definitions.

## Validation Depth

- `Critical UI and persistence behavior foundation`

## Implementation Steps

1. Add selected-template preview state.
2. Materialize a preview `WorkflowDefinition` and component for the canvas.
3. Render preview dialog with canvas-dominant layout.
4. Add read-only guardrails so preview does not persist edits until explicit adoption.
5. Implement draft adoption and name conflict resolution.
6. Add tests for preview, draft save, and `01`/`02` naming.
7. Capture proof and update execution report.

## Scope Exceptions

- Template text debranding remains SB04.
- Full workflow execution from the preview dialog is out of scope.

## Do Not Do

- Do not save on preview open.
- Do not mark created workflows active.
- Do not add a second canvas renderer unless `WorkflowCanvasEditor` cannot be safely reused.

## Acceptance Checklist

- [x] Preview dialog opens from each catalogue item.
- [x] Canvas is visible and read-only.
- [x] Add-to-drafts creates a draft workflow.
- [x] Collision naming covers base, `01`, and `02`.
- [x] Component tests pass.
- [ ] Large-screen screenshot captured in final SB04 pass.

## Proof Required

- Failing-first transcript for duplicate naming/adoption behavior.
- Passing component tests for preview and adoption.
- Source assertions for draft status, name algorithm, and no save-on-preview.
- Anti-stub audit transcript.
- Browser screenshot for preview dialog.
- `proof/SB03/manifest.md` and `proof/SB03/semantic-invariants.md`.

## Browser Validation Logging

- Route: `/agents/workflows`
- Viewport: `>=1600x900`; no small/medium pass.
- Actions: open catalogue, click Preview, inspect preview dialog, verify canvas, click Add to my drafts in a controlled test profile if safe.
- Screenshot path: `bundle://proof/SB03/browser/workflow-template-preview-dialog-large.png`
- Review against `bundle://evidence/design/template-preview-dialog-proposal.png`.

## Progression Gate

- SB04 may start only after preview and add-to-drafts behavior are proven and the draft naming invariant is recorded.

## Suggested Agent Prompt

```text
Implement SB03 only. Add the preview canvas dialog and add-to-drafts behavior, keep preview read-only, prove deterministic naming collisions, capture large-screen browser proof, and update the SB03 proof manifest and execution report.
```
