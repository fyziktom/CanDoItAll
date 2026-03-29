# P0-04 Batch Node-Move Persistence

## Status
- Lifecycle status: `Ready`

## Objective
- Persist multi-node drag as one mutation flow and one save transaction.

## Covered Inputs
- Audit hotspot about repeated writes during node movement.
- Feature preservation items `F08`, `F29`, and `F30`.

## Prerequisites
- `P0-03` completed with trusted persistence proof.

## Exact Source References
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`

## Deliverables
- Batched move service path or equivalent single-transaction persistence.
- Selection retained after drop.
- Border adoption behavior preserved after batch move commit.

## Dependency Impact
- Downstream simple-mutation and renderer proof is stronger if move commits are already narrowed.
- Border adoption is a known weak-coverage area, so this subbundle can reopen if later drag work reveals defects.

## Validation Depth
- Component or integration-style tests for batched move semantics.
- Browser proof for multi-select drag and selection retention.
- One dependent-flow smoke for border adoption if the code path is touched.

## Implementation Steps
- Inspect current move commit path and write count.
- Collapse repeated move persistence into one coherent mutation path.
- Add missing automated coverage if border adoption remains weak.

## Do Not Do
- Do not widen into non-move property mutation work owned by `P0-05`.
- Do not close the task without addressing border adoption proof.

## Acceptance Checklist
- Multi-node drag produces one service call and one DB save transaction.
- Drag commit keeps selected nodes selected.
- Moved-node border adoption still behaves correctly.

## Proof Required
- Targeted ProjectStructure component tests.
- Browser proof for multi-node drag.
- Persistence evidence showing one save transaction.

## Browser Validation Logging
- Route: ProjectStructure structure route.
- Viewport: large-screen with enough space to observe multi-node drag.
- Record drag path, assertions, screenshot paths, and gate result in `reviews/01-execution-report.md`.

## Progression Gate
- Do not start `P0-05` until batched move behavior is proven and border adoption is trusted again.

## Suggested Agent Prompt
- Validate the current move persistence path, then collapse multi-node drag commit into one coherent save flow while preserving selection retention and border adoption behavior.
