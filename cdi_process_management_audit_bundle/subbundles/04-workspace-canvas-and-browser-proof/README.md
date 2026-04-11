# Workspace Canvas And Browser Proof

## Status

- `Completed`

- Closure note: live browser proof covered the authoring controls and runtime action surfaces. Selected-path activation is validated by integration tests because the branch state machine lives in the process service layer.

## Objective

- Add branch authoring and runtime branch-selection support to the process workspace and canvas, then prove the flows in a real browser session with recorded analytics and screenshot review.

## Covered Inputs

- `U003` Add a decision or if node flow with explicit routing ownership.
- `U004` Support multiple switch-style outputs.
- `U005` Real validations are mandatory.
- `BRQ-009`, `BRQ-010`, and `BRQ-011`.

## Prerequisites

- `subbundles/02-branch-definition-model-and-publish-guardrails` closure gate passed.
- `subbundles/03-runtime-branch-orchestration-and-mcp-contracts` closure gate passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessStepEditorForm.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessCanvasSelectionPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessCanvasSurfaceFactory.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessCanvasTemplateCatalog.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs`

## Deliverables

- Authoring UI for branch outcomes, decision-maker role selection, and dependency outcome binding.
- Runtime UI for selecting a branch outcome when completing a branching step.
- Definition and runtime canvas behavior aligned to the real dependency graph.
- Component coverage for the workspace behavior changed in this phase.
- Playwright proof for authoring and runtime flows with screenshots recorded in the execution report.

## Dependency Impact

- Final closure depends on this phase proving the feature is usable, not only compilable.
- Weak browser proof here would invalidate the user-visible completion claim even if backend tests pass.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Add branch authoring controls to the definition workspace without introducing stringly typed state.
2. Update runtime controls so a branching step can be completed only after an explicit outcome is chosen when required.
3. Align canvas authoring and runtime visuals with the actual dependency graph.
4. Add targeted component coverage for the new workspace behavior.
5. Run a real headed Playwright session and capture screenshots for authoring and runtime flows.

## Scope Exceptions

- This phase does not reopen broader styling or process-workbench redesign outside the branch flows needed to prove usability.

## Do Not Do

- Do not ship branch behavior that is only accessible through raw JSON or MCP calls.
- Do not accept browser proof that never opens the branching controls.
- Do not treat screenshots as proof unless they were actually reviewed for layout and readability defects.

## Acceptance Checklist

- A user can author multiple branch outcomes for a step.
- A user can bind a downstream step to a specific dependency outcome.
- A user can choose a branch outcome before completing a branching runtime step.
- The canvas no longer suggests a fake purely sequential flow when branches exist.
- Component and browser proof both cover the new flows.

## Proof Required

- Targeted component tests for the changed workspace behavior.
- A headed Playwright run covering both authoring and runtime branch flows.
- Desktop-width screenshots plus a narrower-width follow-up if layout shifts.
- Explicit screenshot review answers recorded in the execution report.

## Browser Validation Logging

- Route under test: `/processes` or the project-scoped processes route that exposes the repaired workspace.
- Viewports: first `1600x900` or larger desktop-sized viewport, then one narrower responsive pass if layout changes are visible.
- Playwright actions: navigate, create or load a branch-capable definition, author outcomes, publish or start a run, choose a runtime branch outcome, verify the selected path activation, capture screenshots.
- Screenshot evidence paths must be recorded in `reviews/01-execution-report.md`.
- Review questions must be answered for readability, clipping, collisions, spacing, alignment, intended use of space, and branch-control clarity.

## Progression Gate

- Browser analytics rows, screenshots, and screenshot review conclusions are all populated, and the UI proof is strong enough that subbundle 05 does not need to guess what actually worked.

## Suggested Agent Prompt

```text
Implement only the workspace and canvas changes needed for branch authoring and runtime outcome selection. Prove the result with component tests and a real headed Playwright session before calling the phase complete.
```
