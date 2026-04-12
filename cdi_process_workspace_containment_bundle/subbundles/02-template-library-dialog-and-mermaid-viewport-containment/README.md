# Template library dialog and mermaid viewport containment

## Status

- `Completed`

## Objective

- Make the fullscreen templates dialog use internal list/detail scrolling without weak nested body scrolling, and keep Mermaid preview zoom and pan visually clipped to its own preview surface.

## Covered Inputs

- `Same for the modal with Templates. List must be scrollable same as content.`
- `Assure that mermaid graph during zoom will not overflow the component.`

## Prerequisites

- Subbundle 01 closed with trusted page-shell containment proof.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessTemplateLibraryDialog.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessTemplateMermaidPreview.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Modals\Dialog.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.ProcessManagementBundle.cs`

## Deliverables

- Templates modal content uses the dialog body height cleanly.
- Template list and preview panes keep their own scroll regions.
- Mermaid preview host clips transformed content to its viewport during zoom and pan.
- Targeted test coverage proves the modal flow still works after the containment change.

## Dependency Impact

- Subbundle 03 depends on this phase because bundle closure requires live browser proof against the exact screenshot-driven regression surface.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Replace the modal's fixed-height inner wrapper with a true `h-full min-h-0` containment shell.
2. Keep the list and detail panes internally scrollable inside the modal body.
3. Harden the Mermaid preview host so transformed content cannot bleed across adjacent modal content.
4. Extend browser or component assertions where needed to lock in the modal behavior.

## Scope Exceptions

- None planned.

## Do Not Do

- Do not replace the fullscreen dialog component.
- Do not rewrite the Mermaid renderer or add a separate JS viewport system unless the existing host cannot enforce clipping.
- Do not fold unrelated template-library behavior changes into this phase.

## Acceptance Checklist

- The templates modal does not rely on an extra outer body scroll for normal list/detail browsing.
- The template list remains internally scrollable.
- The preview content remains internally scrollable.
- Mermaid zoom and pan stay visually bounded inside the preview card.
- Existing template import actions still work.

## Proof Required

- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessWorkspaceTests" -v:minimal`
- `dotnet test tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj --filter "FullyQualifiedName~Process_management_template_library_flows_are_validated_in_browser" -v:minimal`
- Browser screenshots showing the open templates dialog, list/detail containment, and Mermaid interaction state.

## Browser Validation Logging

- Route: `/processes`
- Viewports: desktop first, then narrower follow-up if the modal wrap changes at smaller widths
- Required actions: open templates dialog, select a process template, switch to diagrams, zoom the Mermaid preview, confirm role or artifact actions still remain reachable
- Expected artifacts:
- `output/playwright/process-workspace-containment/02-template-library-dialog.png`
- `output/playwright/process-workspace-containment/03-template-library-mermaid-contained.png`
- Required review answers: no nested-scroll confusion, no content collision, no visible Mermaid bleed across adjacent columns, buttons remain reachable

## Progression Gate

- Downstream closure may continue only after the open templates dialog shows pane-scoped scrolling and the Mermaid preview remains clipped during zoom in browser proof.

## Suggested Agent Prompt

```text
Implement only the templates dialog and Mermaid containment fix.
Keep the dialog fullscreen workflow and selective-import behavior intact.
Browser proof must include the open dialog state and a Mermaid zoom interaction.
```
