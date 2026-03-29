# Task sequence

Execute tasks in this exact order.

| Task ID | Phase | Title | Depends on |
| --- | --- | --- | --- |
| T00 | P0 | Baseline capture, missing test coverage, and feature lock | None |
| T01 | P0 | Overlay isolation and input ownership cleanup | T00 |
| T02 | P0 | Write-behind canvas state persistence and committed-state ownership | T00, T01 |
| T03 | P0 | Delta move flow and removal of full reload-after-move | T00, T02 |
| T04 | P0 | Toolbox functional repair and dedicated component extraction | T00, T01 |
| T05 | P0 | Visual Studio-like toolbox UX and compact layout | T04 |
| T06 | P1 | CanvasLib directory reorganization skeleton | T00 |
| T07 | P1 | Deterministic asset build pipeline and centralized asset includes | T06 |
| T08 | P1 | Split long JS, CSS, Razor, and C# files into maintainable parts | T06, T07 |
| T09 | P1 | Preview-boundary isolation and legacy path quarantine | T06, T07, T08 |
| T10 | P2 | Real canvas stage shell with stable CanvasWorkbench API | T01, T02, T06, T07, T08 |
| T11 | P2 | Canvas links renderer and geometry-based link hit model | T10 |
| T12 | P2 | Canvas minimap, diagnostics, and group-frame renderer | T10, T11 |
| T13 | P2 | Canvas node renderer phase A with hot-zones and HTML overlay escape hatches | T10, T11, T12 |
| T14 | P2 | Canvas export pipeline and accessibility mirror alignment | T10, T11, T12, T13 |
| T15 | P3 | ProjectStructurePage adoption, performance pass, and benchmark evidence | T03, T04, T05, T10, T11, T12, T13, T14 |
| T16 | P3 | PromptFactory compatibility, shared-consumer rollout, and preview lane safety | T02, T06, T07, T08, T10, T11, T12, T13, T14, T15 |
| T17 | P3 | Cleanup, legacy-path reduction, final documentation, and full regression pack | T09, T15, T16 |

## Sequencing rules

- Do not start `T10` before the guard-rail, toolbox, and structure-reorganization tasks are in place.
- Do not start `T13` until links, minimap, diagnostics, and group frames have moved to canvas and the stage shell is stable.
- Do not remove any legacy/fallback path before `T15` and `T16` are green.
