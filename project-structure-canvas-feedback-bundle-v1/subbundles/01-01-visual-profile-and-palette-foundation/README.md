# 01-01-visual-profile-and-palette-foundation

## Status

- `Completed`

## Objective

- Establish one maintainable node-preset pipeline for the project-structure canvas so category colors, palette semantics, and Tailwind-backed effects come from node properties instead of split logic.

## Covered Inputs

- `N001`
- `RQ-01`
- Foundation for `RQ-06`, `RQ-07`, and `RQ-09`

## Prerequisites

- Prepared-stage bundle validator passes.
- Existing project-structure smoke test route remains healthy enough to open `/projects/{projectId}/structure`.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.SharedKernel\ProjectObjectContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureGraphAdapter.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureCanvasCatalog.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructureGraphAdapterTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs

## Deliverables

- A single typed preset contract that carries the node visual data needed by the canvas adapter, including semantic palette identity and Tailwind-driven styling metadata.
- Unified preset resolution so PDF, Excel, deployment, and related categories render through the same source of truth.
- Removal or consolidation of split palette logic that currently lives partly in the graph adapter.
- Automated coverage proving both preset mapping and rendered browser distinction for representative node categories.

## Dependency Impact

- `02-02-catalog-expansion-and-type-mutation-flows` depends on this phase so new common blocks inherit the same preset behavior as existing nodes.
- `03-03-inline-note-multiline-and-note-conversion` depends on this phase because note-to-block conversion must land on the same preset contract as ordinary block mutation.
- `04-04-node-id-copy-and-subtree-clipboard-workflows` depends on this phase indirectly because subtree duplication must recreate nodes whose visuals remain correct after paste.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Inspect the current visual profile and palette split between workbench models and the graph adapter.
2. Extend or replace the visual preset contract so the node carries the canvas-facing preset data in one place.
3. Refactor preset resolution into a single strongly typed path and update adapter consumption accordingly.
4. Align the required semantic colors called out in the request, including deployment-oriented blue presets.
5. Add or update focused component tests and Playwright coverage.

## Do Not Do

- Do not introduce a second styling registry outside the existing workbench and shared-kernel boundaries.
- Do not hardcode ad hoc CSS classes directly in multiple Razor components just to satisfy individual node types.
- Do not defer browser-visible color proof to later subbundles.

## Acceptance Checklist

- Nodes of different key categories resolve distinct presets through one source of truth.
- PDF, Excel, and deployment-related nodes match the requested semantic color intent.
- The adapter no longer owns a competing palette-mapping decision tree for the same node kinds.
- Existing and new preset consumers can retrieve the same semantic result without stringly typed branching.

## Proof Required

- Run focused component coverage for graph adapter or preset-resolution behavior.
- Run a Playwright pass that creates or inspects representative PDF, Excel, and deployment-related nodes on `/projects/{projectId}/structure`.
- Capture screenshots showing the distinct rendered colors at `1600x1000` and `1280x800`.
- Record screenshot review findings in `reviews/01-execution-report.md`.

## Browser Validation Logging

- Route under test: `/projects/{projectId}/structure`
- Required viewports: `1600x1000` large-screen proof and `1280x800` follow-up
- Required Playwright evidence: open the structure route, surface representative node types, assert distinct computed styles or stable selectors, and confirm the selection panel still works
- Required screenshots: `01-visual-presets-large.png`, `01-visual-presets-narrow.png`
- Screenshot review questions: are category colors visually distinct, semantically appropriate, and readable against the current canvas chrome

## Progression Gate

- Downstream subbundles may continue only after preset logic is unified, focused tests pass, and browser screenshots confirm the rendered distinctions for representative categories.

## Suggested Agent Prompt

```text
Implement subbundle 01-01-visual-profile-and-palette-foundation only. Unify the node visual preset path, eliminate split palette logic, align semantic colors for key categories, and add the focused tests plus Playwright proof required by the subbundle.
```
