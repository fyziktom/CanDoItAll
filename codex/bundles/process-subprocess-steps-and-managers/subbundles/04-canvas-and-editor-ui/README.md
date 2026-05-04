# canvas and editor UI

## Status

- `Completed`

## Objective

- Make subprocess steps editable and recognizable in the process canvas and step editor.

## Covered Inputs

- Add/change subprocess in process canvas and UI.
- Right-click menu must expose subprocess actions.
- Double-clicking a subprocess opens it in a new browser tab.
- Subprocess canvas nodes need a distinct visual style.

## Prerequisites

- `subbundles/01-architecture-source-of-truth-and-schema`
- `subbundles/02-runtime-subprocess-orchestration`
- Revalidation gate A passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Canvas\ProcessCanvasSurfaceFactory.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Canvas\ProcessCanvasCatalog.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Canvas\ProcessCanvasChromeCatalogService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Canvas\ProcessCanvasTemplateCatalog.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.Actions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.Editor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessStepEditorForm.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessCanvasSelectionPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceStepsTab.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessCanvasSurfaceFactoryTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessStepEditorFormTests.cs`

## Deliverables

- Canvas action ids and context menu entries for add/change subprocess.
- Step editor subprocess selector.
- Distinct subprocess node profile, icon, palette, and runtime chips.
- Double-click opens referenced process definition in a new tab.
- Component tests and browser proof.

## Dependency Impact

- Default templates and real scenario validation rely on users being able to inspect and edit subprocess references.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Add canvas catalog constants and visual profile for subprocess.
2. Add context/chrome actions for subprocess create/change.
3. Add subprocess selector and validation to step editor.
4. Add double-click new-tab behavior.
5. Update selection/runtime panels.
6. Add component tests.
7. Run browser proof with screenshots.

## Scope Exceptions

- Do not redesign the process workspace.
- Do not add Tailwind if the existing process UI is using project components/CSS.

## Do Not Do

- Do not represent subprocess nodes as only generic text labels.
- Do not bypass definition validation from canvas actions.
- Do not open current tab when the requirement is a new tab.

## Acceptance Checklist

- Right-click can add a subprocess step.
- Existing step can be changed to a subprocess with a selected process.
- Subprocess nodes are visually distinct in canvas tests and screenshots.
- Double-click calls browser open with `_blank`.
- Step editor rejects missing subprocess target.

## Proof Required

- Component tests for canvas surface and step editor.
- Browser proof on process canvas at desktop and narrower width.
- Screenshots recorded in execution report.

## Browser Validation Logging

- Target route or window: process workspace definition canvas.
- Required viewport passes: maximized desktop and 900px-wide follow-up.
- Required actions/assertions: open context menu, create/change subprocess step, double-click subprocess, verify new tab call or opened tab.
- Screenshot evidence: `process-subprocess-canvas-desktop.png`, `process-subprocess-canvas-narrow.png`.
- Review questions: Does the subprocess node read as distinct? Is selector text contained? Are menu actions discoverable?

## Progression Gate

- Continue only when UI editing cannot create invalid subprocess references and screenshot review passes.

## Suggested Agent Prompt

```text
Implement only subprocess canvas/editor UI. Keep actions strongly typed, use existing components, and prove right-click, selector, visual style, and double-click new-tab behavior.
```
