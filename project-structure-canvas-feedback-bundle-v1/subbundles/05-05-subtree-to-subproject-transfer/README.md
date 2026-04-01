# 05-05-subtree-to-subproject-transfer

## Status

- `Completed`

## Objective

- Add a supported workflow that moves all descendants under a selected node into a subproject target while preserving hierarchy, producing a valid subproject relationship, and refreshing both source and destination views coherently.

## Covered Inputs

- `N005`
- `RQ-05`

## Prerequisites

- `02-02-catalog-expansion-and-type-mutation-flows` is completed.
- `04-04-node-id-copy-and-subtree-clipboard-workflows` is completed.
- The subtree recomposition baseline remains green.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureSubtreeRecompositionEngine.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.ProjectHierarchy.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.Workflows.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\ProjectModels.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectWorkbenchSubtreeRecompositionIntegrationTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs

## Deliverables

- An explicit UI flow that moves descendants of the selected node into a subproject target.
- Strongly typed orchestration that preserves parent-child relationships and refreshes the source canvas after transfer.
- Service or page-level logic that creates or reuses the necessary subproject relationship without leaving orphaned descendants.
- Integration and browser proof that the descendant structure survives the transfer.

## Dependency Impact

- `06-06-browser-proof-and-closure` depends on this phase because this is the highest-risk structural mutation in the feedback set.
- Weak proof here invalidates final raw-note closure for descendant movement behavior because subtree transfer is broader than clipboard duplication.

## Validation Depth

- `UI, integration, and browser-proof`

## Implementation Steps

1. Inspect the existing hierarchy dialog and project models to find the strongest supported insertion point for subtree-to-subproject transfer.
2. Implement the descendant transfer flow with explicit preconditions and predictable refresh behavior.
3. Reuse subtree recomposition logic where layout or placement needs to be preserved.
4. Add or update integration coverage for the moved subtree semantics.
5. Prove the end-to-end transfer in Playwright and capture screenshots.

## Do Not Do

- Do not implement this as a silent destructive move without explicit user-driven intent.
- Do not leave descendants behind in the source tree after reporting success.
- Do not bypass existing project hierarchy boundaries with stringly typed project-link manipulation.

## Acceptance Checklist

- The user can invoke a supported flow to move descendants under a node into a subproject.
- The selected anchor node stays in the source project while its descendants move coherently to the target subproject.
- The destination subproject receives the structure without broken parent-child relationships.
- Source and destination views refresh consistently after the move.

## Proof Required

- Run integration coverage for subtree recomposition or transfer semantics as updated by this phase.
- Run a Playwright flow that sets up a node with descendants, invokes the subtree-to-subproject transfer flow, and verifies the source canvas and destination structure afterward.
- Capture screenshots for the transfer invocation state and the post-transfer result.
- Record any required assumptions or UI wording used by the transfer flow in `reviews/01-execution-report.md`.

## Browser Validation Logging

- Route under test: `/projects/{projectId}/structure`
- Required viewports: `1600x1000` large-screen proof and `1280x800` follow-up
- Required Playwright evidence: create or identify a subtree, invoke the move-to-subproject flow, verify the descendants leave the source branch, and confirm they appear in the target subproject context
- Required screenshots: `05-transfer-to-subproject-dialog.png`, `05-transfer-to-subproject-result.png`
- Screenshot review questions: is the transfer intent explicit and does the resulting structure still read as one coherent subtree

## Progression Gate

- Final closure may continue only after descendant transfer is proven through integration coverage and browser evidence showing the subtree moved correctly into the target subproject.

## Suggested Agent Prompt

```text
Implement subbundle 05-05-subtree-to-subproject-transfer only. Add the explicit descendant-to-subproject transfer flow using existing hierarchy boundaries, preserve subtree structure, and produce the required integration and Playwright proof.
```
