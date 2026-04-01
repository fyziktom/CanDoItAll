# 04-04-node-id-copy-and-subtree-clipboard-workflows

## Status

- `Completed`

## Objective

- Add explicit node-id copy actions and subtree-aware clipboard behavior so the project-structure workbench supports copying ids, copying descendant id structure, and cutting and pasting entire selected subtrees with keyboard shortcuts.

## Covered Inputs

- `N003`
- `N004`
- `RQ-03`
- `RQ-04`

## Prerequisites

- `01-01-visual-profile-and-palette-foundation` is completed.
- The project-structure route can already create and refresh representative node selections in Playwright.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Workbench\CanvasWorkbenchChrome.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Workbench\CanvasWorkbenchEvents.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Workbench\CanvasWorkbench.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\02-layout-and-legacy-render.js
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\07-runtime-entry.js
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.Workflows.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructureSelectionPanel.razor
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs

## Deliverables

- A copy action that copies the selected node id.
- A second copy action that copies the descendant id structure in deterministic hierarchy order.
- Recursive clipboard payload support for subtree copy, cut, and paste.
- Project-structure page orchestration that persists subtree duplication, subtree removal on cut, and subtree recreation on paste.

## Dependency Impact

- `05-05-subtree-to-subproject-transfer` depends on this phase because descendant-aware transfer should reuse proven subtree selection and movement semantics.
- `06-06-browser-proof-and-closure` depends on this phase because keyboard and clipboard behavior cannot be inferred from static tests alone.
- Weak proof here would leave downstream hierarchy work unable to distinguish clipboard regressions from transfer regressions.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Extend the CanvasLib clipboard contract to distinguish copy, cut, paste, and subtree-aware payload data.
2. Add selection-surface actions for copy node id and copy subtree id structure.
3. Implement project-structure persistence and refresh flows for subtree cut and paste.
4. Add or update focused tests where feasible for clipboard request handling.
5. Prove the copy and keyboard flows in Playwright with actual subtree selections and screenshots.

## Do Not Do

- Do not treat subtree cut and paste as a shallow selected-node operation.
- Do not copy descendant ids in nondeterministic order.
- Do not leave clipboard behavior as a browser-only visual trick without persisted structure changes.

## Acceptance Checklist

- A selected node exposes one action for copying its own id and a second action for copying the descendant id structure.
- The subtree id structure output is deterministic and reflects hierarchy order.
- `Ctrl+X` cuts the selected node together with descendants.
- `Ctrl+V` pastes a structurally valid copy of the subtree with fresh ids and preserved internal relationships.

## Proof Required

- Run focused automated coverage for any new clipboard request handling or subtree serialization logic.
- Run a Playwright flow that selects a node with descendants, triggers both copy actions, and verifies the UI remains stable.
- Run a Playwright keyboard flow for `Ctrl+X` and `Ctrl+V` on a subtree and assert the descendant structure survives the move.
- Capture screenshots of the selection surface actions and the post-paste subtree state.

## Browser Validation Logging

- Route under test: `/projects/{projectId}/structure`
- Required viewports: `1600x1000` large-screen proof and `1280x800` follow-up
- Required Playwright evidence: select a node with descendants, invoke both copy actions, perform `Ctrl+X` and `Ctrl+V`, and assert the recreated subtree labels or ids in the browser
- Required screenshots: `04-copy-actions.png`, `04-cut-paste-subtree.png`
- Screenshot review questions: are both copy affordances discoverable and does the pasted subtree remain coherent and readable

## Progression Gate

- Subtree-to-subproject transfer may continue only after descendant-aware cut and paste is proven end to end with stable browser behavior and preserved subtree structure.

## Suggested Agent Prompt

```text
Implement subbundle 04-04-node-id-copy-and-subtree-clipboard-workflows only. Add node id copy actions, subtree id export, and descendant-aware cut and paste through the existing CanvasLib and project-structure boundaries, then produce the required proof.
```
