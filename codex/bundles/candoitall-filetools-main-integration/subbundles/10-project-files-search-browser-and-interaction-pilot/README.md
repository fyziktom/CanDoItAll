# SB10 Project Files Search Browser And Interaction Pilot

## Status

- `Ready`

## Objective

- Prove exactly one real end-to-end case: browse/search one project's authorized files and open one known Markdown/text file read-only in FileInteraction on large desktop.

## Covered Inputs

- N007, N009, N012-N017; R015-R016, R022, R024-R040.

## Prerequisites

- SB09 unqualified Pass; Components MCP recommendations/examples and managed watch/Playwright available.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.Projects/Pages/ProjectsPage.razor`
- `repo://src/Modules/CanDoItAll.Modules.Projects/Pages/Components/ProjectsBoard.razor`
- `repo://src/Modules/CanDoItAll.Modules.Projects/ProjectModels.cs`
- `bundle://subbundles/06-filetools-package-adoption-and-integration-boundaries/README.md`
- `bundle://subbundles/07-authorized-handles-content-save-and-endpoint-hardening/README.md`
- `C:\repositories\CanDoItAll.FileTools\src\CanDoItAll.FileTools.FileBrowser.Components\Components\FileBrowser.razor.cs`
- `C:\repositories\CanDoItAll.FileTools\src\CanDoItAll.FileTools.FileInteraction.Components\Components\FileInteraction.razor.cs`
- `C:\repositories\CanDoItAll.FileTools\docs\host-integration-security.md`
- `repo://tests/Components/CanDoItAll.Tests.Components/ProjectsPageTests.cs`
- `repo://tests/Playwright/CanDoItAll.Tests.Playwright`
- `bundle://architecture/10-performance-and-scale.md`

## Deliverables

- Focused project file-scope provider/coordinator/session owner and pilot Razor pane/dialog using shared component wrappers selected by Components MCP.
- Single project source only; host/session cache Disabled unless SB08 explicitly proves an aggregate policy.
- Search, folder navigation, paging/refresh, loading/empty/error/retry/result, keyboard activation.
- Activation re-resolves/authorizes and opens known Markdown/text in read-only FileInteraction using independent content source.
- Large-source proof using instrumented production adapters/fakes: bounded first page/search/rendered state, latest-request cancellation, inspected/returned/retained counts, and no browser dependency after known-file activation.
- Clean disposal/replacement and visible error handling; no editing/portfolio/canvas/process/resource scope.

## Dependency Impact

- SB11 decides whether the seam is trustworthy; all broader UI waits.

## Validation Depth

- Proof tier: `Behavioral`.
- Critical user-visible pilot.

## Implementation Steps

1. Pull Components MCP library/recommend/component/usage/example data and record chosen wrappers.
2. Add directly testable project scope/coordinator without growing page-local policy.
3. Add smallest pilot entry surface and explicit lifetimes.
4. Use managed watch loop one edit at a time and one persistent Playwright page.
5. Prove positive project search/browse/open, large-source structural bounds, direct interaction handoff, and meaningful unauthorized/stale/replaced/no-result/error negatives.
6. Capture/inspect desktop screenshots, DOM, scroll/overlay geometry, console/network.
7. Run component/service/build tests and C# gate.

## C# Architecture Impact

- Focused service/component extraction; no broad Projects refactor yet.

## Boundary Ownership

- Project semantic scope in correct module/contract implementation; UI only renders/orchestrates; outer integration effects authorize.

## Dependency Direction

- Projects references only allowed Integration/FileTools packages; no Workbench/Resources edge.

## Pattern Decision

- PSR-05 focused coordinator/pane; existing FileInteraction builder.

## Testability Contract

- Scope/search/session/activation tested with fakes without `ProjectsPage`; component tests own callbacks/state; Playwright owns real flow.

## Partial Class Policy

- No partial/nested service; no browser logic added to existing page code block as final owner.

## Architecture Proof Required

- New owner responsibility/test seam, references/cycles, no service locator, Components choices, parent growth/source assertions.

## Scope Exceptions

- Exactly one project, one known read-only file type. No broader user story or editing.

## Do Not Do

- Do not use FileTools sandbox/fake provider as product proof, duplicate project filters, trust browser item, add raw layout wrappers/CSS before Components discovery, or test small/medium/mobile.

## Acceptance Checklist

- [ ] Real authorized project files search/browse works.
- [ ] Known Markdown/text opens read-only through handle/content source.
- [ ] Browsing/search stays within declared work/state/render limits and cancels superseded requests.
- [ ] After activation, the known file loads independently; closing/disposal of FileBrowser does not affect FileInteraction.
- [ ] Unauthorized/stale/replaced/no-result/error cases behave explicitly.
- [ ] Lifetimes/disposal and source replacement are correct.
- [ ] Desktop DOM/visual/scroll/overlay/console/network proof passes.

## Proof Required

- Behavioral semantic record, exact unit/component/host/Playwright commands, DOM assertions, screenshot paths/review answers, source/dependency/anti-stub assertions.
- Shallow-pass traps: a bounded-looking UI backed by full-source scanning, and a FileInteraction dialog that still depends on the live browser session. Instrumented large-source negatives and browser-disposal tests must fail those implementations; the realistic positive searches/browses a real authorized project source, closes/replaces browsing, and keeps the activated known file usable.

## Browser Validation Logging

- Route: selected pilot Projects route/surface.
- Viewports: `1900x1200`, `1440x900` only.
- Actions: open pilot, search exact and missing term, navigate folder, activate known file by pointer and keyboard, close/reopen, provoke unauthorized/stale/error/retry, open any menu/dialog state.
- Assertions: result identity/count semantics, active source/location, interaction content/state, one scroll owner, overlay geometry, no clipping/lateral overflow, zero unexpected console/page/network errors.
- Screenshots: `proof/SB10/browser/project-files-pilot-1900x1200.png`, `project-files-pilot-interaction-1900x1200.png`, `project-files-pilot-1440x900.png`, error/open-overlay as needed; inspect each.

## Progression Gate

- SB11 enters only with complete positive/negative/desktop proof through real production adapters and no unresolved generic gap.

## Reopen Triggers

- Stale/unauthorized content, fake-only path, scope/filter leak, unbounded work/state/rendering, browser-dependent interaction, lifecycle defect, shared component mismatch, clipping/scroll/overlay/console defect reopens SB10 or owning foundation; broader UI remains blocked.

## Suggested Agent Prompt

```text
Implement the single project-files pilot only: real authorized search/browse and read-only Markdown/text FileInteraction. Use Components MCP and the managed watch/one-page Playwright loop. Prove meaningful negatives and desktop layout, then stop before broader stories.
```
