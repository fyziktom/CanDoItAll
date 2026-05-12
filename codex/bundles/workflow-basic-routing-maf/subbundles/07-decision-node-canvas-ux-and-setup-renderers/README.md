# Decision Node Canvas UX And Setup Renderers

## Status

- `Completed`

## Objective

- Make IF/ELSE, SWITCH/default, and fan-out decisions first-class canvas blocks, not just edge metadata.
- Render decisions as clear diamond nodes with readable branch labels and intuitive split connections.
- Improve first-create setup dialogs with block-specific renderer metadata and useful fields for decisions, LLM calls, executors, human input, artifacts, agents, and subworkflows.

## Covered Inputs

- RQ-022 through RQ-025.
- Attached image target: `C:\Users\lucys\Downloads\Vygenerovaný obrázek 1.png`.

## Prerequisites

- Subbundles 01-05 completed.
- Existing workflow canvas route metadata must continue to save/load and validate.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Workbench\CanvasWorkbenchChrome.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\css\workbench`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\WorkflowsPageTests.cs`

## Deliverables

- Decision quick-create action group with second-layer context menu children and matching toolbox entries.
- Decision node setup requests that create useful default branch edges.
- Renderer-keyed action metadata for block setup dialogs.
- Canvas renderer for diamond decision nodes and colored branch links.
- Component/browser proof that setup dialogs work in maximized canvas.

## Validation Depth

- Critical UI foundation: component tests plus browser screenshots are required.

## Dependency Impact

- The decision action metadata is consumed by the workflow editor, CanvasLib context menu, toolbox, and setup dialog renderer.
- Existing saved workflow route metadata remains compatible because node creation adds typed route edges without changing the persisted route contract.
- Future plugin/executor renderers can key off `setupRendererKey` without replacing the current composer.

## Implementation Steps

1. Add strongly typed decision create-action metadata and parser.
2. Add decision items to toolbox and quick-create nested menu.
3. Consume setup dialog input values when creating decision nodes.
4. Add renderer-key metadata to `CanvasWorkbenchAction` and normalize/render it in JS.
5. Improve composer layout/CSS for maximized canvas and many setup fields.
6. Render workflow decision nodes as diamonds and color branch links by route tone.
7. Add tests and browser proof.

## Do Not Do

- Do not make arbitrary script/C# authoring available from setup dialogs.
- Do not break existing generic canvas consumers.

## Acceptance Checklist

- Right-click menu exposes a decisions submenu.
- Toolbox exposes IF/ELSE, SWITCH/default, and fan-out entries.
- First-create dialog shows meaningful setup fields for decisions and common blocks.
- Decision nodes render as diamonds and branches are visually distinguishable.
- Browser screenshot has no clipped or overlapping setup-dialog content in maximized canvas.

## Proof Required

- Targeted component tests.
- Browser screenshots for decision nodes and setup dialog.

## Closure Proof

- Decision blocks are in the toolbox and nested context menu.
- Decision nodes render as diamonds with side anchors and route labels.
- First-create dialogs use `setupRendererKey` metadata and sectioned fields.
- Executor create dialogs include concrete setup fields; HTTP dialog proof includes method, URL/URL JSON path, headers, body, response limits, and execution policy.
- Browser evidence: `reviews/evidence/subbundle-07/decision-diamond-maximized.png`, `decision-context-submenu.png`, `decision-setup-dialog-maximized.png`, and `http-executor-setup-dialog-maximized.png`.

## Browser Validation Logging

- Route: `/agents/workflows`.
- Evidence files under `reviews/evidence/subbundle-07/`.

## Progression Gate

- Subbundle 09 final proof may proceed only after decision nodes and setup dialogs pass browser review.

## Suggested Agent Prompt

```text
Implement subbundle 07 only: decision canvas blocks, nested menu/toolbox entries, renderer-keyed setup dialogs, and browser proof. Preserve existing routing behavior.
```
