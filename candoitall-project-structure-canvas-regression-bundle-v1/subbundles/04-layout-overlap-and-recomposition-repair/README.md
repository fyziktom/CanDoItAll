# 04 - Layout Overlap And Recomposition Repair

## Status

- `Completed`

## Objective

- Repair the overlapping and exploded saved layout on the B13 structure canvas, prove that recomposition produces a logical mindmap, and persist that readable state across reloads.

## Covered Inputs

- `RQ-07`
- `RQ-08`

## Prerequisites

- `01-mcp-canvas-harness-and-core-node-coverage` must be completed or honestly blocked before this phase starts.
- `02-context-menu-links-and-dependencies` must be completed or honestly blocked before this phase starts.
- `03-conditional-repairs-and-closure` must be completed or honestly blocked before this phase starts.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureSubtreeRecompositionEngine.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectWorkbenchSubtreeRecompositionIntegrationTests.cs`
- `C:\repositories\CanDoItAll\artifacts\project-structure-crm-testing\control-plane\database-profiles\managed-sqlite\9cef275207d34fc1b92c043e05ed2a2c\db\candoitall.db`

## Deliverables

- a minimal recomposition-engine repair for single-child deep branches
- regression coverage for the repaired branch mode
- persisted layout repair for the B13 project in the managed SQLite profile
- screenshot-backed before / after / after-reload proof

## Dependency Impact

- no downstream subbundles remain

## Validation Depth

- code regression plus live browser persistence proof

## Implementation Steps

1. Reproduce the unreadable saved layout in the live B13 project and record screenshots.
2. Inspect the persisted node coordinates and identify whether recomposition or viewport logic is causing the unreadable state.
3. Implement the smallest correct repair in the recomposition engine and add regression coverage.
4. Rebuild, reopen the B13 project on the correct managed SQLite profile, rerun recomposition, and store a readable persisted layout.
5. Review the resulting mindmap as a real execution plan and record whether the flow makes sense.

## Do Not Do

- Do not leave the layout fixed only in memory.
- Do not accept a collision-free output if the resulting flow is still illogical to read.
- Do not switch to the wrong control-plane profile and pretend the route was validated.

## Acceptance Checklist

- recomposition no longer explodes single-child deep branches into extreme coordinates
- the B13 route opens on the correct managed SQLite profile
- the repaired layout is readable at a normal zoom level after fit
- a clean reload preserves the repaired positions and viewport

## Proof Required

- code diff for the recomposition repair
- targeted integration-test pass
- Playwright MCP screenshots before repair, after repair, and after reload

## Browser Validation Logging

- target route: `http://127.0.0.1:5046/projects/8d55cc21-1c49-4654-8e13-07f39891e883/structure`
- required viewport passes: `1600x1000` repaired route plus persisted reload
- required Playwright MCP evidence: root selection, recomposition, fit-to-view, reload review
- expected screenshot location: `C:\repositories\CanDoItAll\output\playwright\canvas-regression-v1\b13-layout-repair\`

## Progression Gate

- the bundle may close only after the route is readable on reload and the completed validator passes.

## Suggested Agent Prompt

```text
Implement only this reopened layout-repair subbundle. Reproduce the broken B13 saved layout, repair the single-child recomposition behavior, validate with live Playwright MCP proof on the managed SQLite profile, and leave the route readable after reload.
```
