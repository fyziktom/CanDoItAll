# 03 Validation And Browser Proof

## Status

- `Completed`

## Objective

- Prove the layout tuning with tests and browser-visible process canvas evidence, then close raw notes.

## Success Criteria

- Targeted component test command passes.
- Build or broader test command confirms touched projects compile.
- Browser proof inspects the actual process canvas route or records an explicit blocker.
- Execution report raw-note closure is no longer pending.

## Covered Inputs

- `N001` through `N007`
- `REQ-006`

## Prerequisites

- `02-definition-recomposition-tuning` completed with tests passing.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.Recomposition.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessCanvasToolbarActions.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessCanvasRecompositionServiceTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureProcesses.cs`

## Deliverables

- Test/build evidence.
- Browser validation analytics row.
- Raw-note closure statuses.
- Final bundle closure validation.

## Dependency Impact

- Final closure depends on this phase. Missing browser proof must be treated as a validation gap, not a solved UI request.

## Validation Depth

- End-to-end regression and closure.

## Implementation Steps

1. Run targeted component tests.
2. Run build or broader test command as needed.
3. Launch the local app if available.
4. Open a large desktop viewport on the process canvas.
5. Trigger or inspect recomposed canvas state and capture screenshot proof.
6. Record browser analytics, raw-note closure, and final validator output.

## Scope Exceptions

- If the local app cannot launch within the turn, record the exact blocker and close browser proof as `Partially solved`.

## Do Not Do

- Do not treat generated images as product proof.
- Do not close a UI note without either browser proof or an explicit validation blocker.

## Acceptance Checklist

- Tests pass.
- Browser row contains route, viewport, actions, screenshots, and result.
- Raw notes have `Solved`, `Partially solved`, or `Not solved`.
- Final bundle validator passes or records a concrete blocker.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter ProcessCanvasRecompositionServiceTests`
- `dotnet build CanDoItAll.slnx` or narrower successful build command covering touched projects.
- Playwright or in-app browser screenshot proof for a process canvas route.

## Browser Validation Logging

- Route: `/processes` or `/projects/{projectId}/processes`.
- Viewport: large desktop first; narrower viewport if canvas chrome changes unexpectedly.
- Required actions: navigate, select or load a process definition, open Steps canvas, trigger or inspect recomposed layout, capture screenshot.
- Review questions: Is the default path readable? Are branch routes separated? Are role nodes near related steps? Are connectors traceable?

## Progression Gate

- Passed. Targeted tests, isolated solution build, and browser analytics support raw-note closure.

## Completion Notes

- Targeted command passed: `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter ProcessCanvasRecompositionServiceTests --logger "console;verbosity=normal" -p:BaseOutputPath=.codex\tmp\layout-test-bin\`.
- Build command passed after stopping the prior proof app that held isolated output DLL locks: `dotnet build CanDoItAll.slnx -p:BaseOutputPath=.codex\tmp\layout-build-bin\`.
- Browser proof used `http://127.0.0.1:5079/processes` at `1600x1000`, opened the 16-step `Multi-team software delivery and release governance` process, opened `Steps`, triggered `Recomposition`, fit the canvas, and captured `C:\repositories\CanDoItAll\process-canvas-layout-browser-proof.png` plus `C:\repositories\CanDoItAll\process-canvas-layout-browser-proof.json`.
- Residual risk: existing CanvasLib edge routing still draws many role/artifact links through dense areas when the whole process is fit into a small viewport. This bundle tuned node positions only; edge bundling/routing remains a separate improvement.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
