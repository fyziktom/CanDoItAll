# Codex task — PRM-F24

Implement **Live process execution canvas overlays and baton visibility** inside the uploaded CanDoItAll solution.

## Constraints

- Treat overlay data as derived from canonical definition, runtime, and journal state.
- Do not make CanvasLib the owner of process runtime state.
- Keep all code comments in English.
- Preserve buildability for the current solution layout.

## Required outputs

- Code changes for this feature
- Matching tests
- Migration updates if persistence changes
- A short implementation note describing what changed and how it was verified

## Done definition

This task is done when:

- A live run can be viewed on the authored process canvas with active, waiting, blocked, and completed step overlays.
- Canvas overlays show current assignee or executor, wait reason, approval state, and last baton movement where relevant.
- Timeline and canvas views link to the same underlying run and journal without duplicate state mutation paths.
- Runtime overlay projection is explicitly separated from canonical definition data and mutable runtime state.

## Recommended first files to touch

- `src/CanDoItAll.Modules.Processes/CanvasAdapters/ProcessCanvasGraphAdapter.cs`
- `src/CanDoItAll.Modules.Processes/ProcessRuntimeOverlayService.cs (new)`
- `src/CanDoItAll.Modules.Processes/Pages/ProcessDesignerPage.razor`
- `src/CanDoItAll.Modules.Processes/Pages/ProcessRunPage.razor (new)`
- `src/CanDoItAll.Components.CanvasLib/Canvas/Workbench/*`
- `tests/CanDoItAll.Tests.Component/ProcessRuntimeOverlayTests.cs (new)`
- `tests/CanDoItAll.Tests.Playwright/ProcessRunCanvasOverlayFlowTests.cs (new)`
