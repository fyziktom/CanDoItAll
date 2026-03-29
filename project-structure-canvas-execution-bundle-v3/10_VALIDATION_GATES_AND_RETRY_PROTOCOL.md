# Validation gates and retry protocol

## Core rule

Every task must follow this loop:

1. implement,
2. run targeted validation,
3. inspect failures,
4. fix,
5. rerun,
6. repeat until green.

Do **not** move to the next task with known failures.

## Required validation layers

### A) Component/unit level
Run targeted component tests for:
- `ProjectStructurePageTests`
- `ProjectStructurePageMoveTests`
- `ProjectStructurePageSimpleMutationTests`
- `CanvasWorkbenchTests`
- `CanvasFloatingWindowTests`
- `PromptFactoryPageTests`
- any preview-boundary tests affected by relocation

### B) Browser level
Run targeted Playwright flows for:
- ProjectStructure toolbox,
- ProjectStructure selection/context flows,
- export image flow,
- PromptFactory shared-canvas smoke,
- CanvasBenchmark smoke.

### C) Screenshot level
Required screenshot coverage:
- default ProjectStructure canvas state,
- toolbox collapsed/default,
- toolbox expanded group,
- toolbox search result,
- selection window single-node,
- selection window multi-node,
- health window,
- context menu,
- quick action dialog,
- transcript/provider dialog,
- summary modal,
- attachment preview,
- mermaid modal,
- PromptFactory canvas default,
- CanvasBenchmark results,
- large-graph diagnostics state.

### D) Performance level
Required evidence after renderer tasks:
- stage DOM count before/after,
- renderer kind output,
- persistence commit count before/after,
- benchmark evidence from CanvasBenchmark,
- no unintended zoom events during toolbox wheel usage.

## Toolbox-specific gates

The toolbox task is not done until browser tests prove:
- collapsed group expands,
- expanded group collapses,
- `aria-expanded` changes correctly,
- tooltip/title exists for item description,
- rows remain single-line,
- wheel scroll inside toolbox does not change canvas zoom.

## Renderer-specific gates

The real-canvas migration is not done until browser proof shows:
- actual `<canvas>` layers in the runtime stage,
- runtime nodes/links/frames/minimap are not primarily DOM/SVG in the main scene anymore,
- node behaviors still work through hit zones or overlay escape hatches,
- export still succeeds.

## Shared-consumer gates

Any change in shared canvas code must also validate:
- PromptFactory browser smoke,
- PromptFactory component tests,
- Sandbox canvas page smoke,
- preview-boundary component tests if moved.

## Suggested command groups

Because exact local commands can vary, use project-scoped validation such as:
- `tests/CanDoItAll.Tests.Components`
- `tests/CanDoItAll.Tests.Playwright`

Use filters for the classes and scenarios listed above.

## Failure policy

If any gate fails:
- do not suppress the test,
- do not mark the task complete,
- fix the implementation or add missing compatibility,
- rerun until the task is green.
