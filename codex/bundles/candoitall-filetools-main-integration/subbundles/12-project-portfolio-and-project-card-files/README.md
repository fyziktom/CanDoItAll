# SB12 Project Portfolio And Project Card Files

## Status

- `Completed — Pass (2026-07-13)`

## Objective

- Extend the accepted pilot to filtered project/subproject portfolio browsing and a focused project-card Files dialog while maintaining one filter/source truth.

## Covered Inputs

- N010-N014; R017-R018, R024-R030.

## Prerequisites

- SB11 unqualified Pass; no reopened foundation.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.Projects/Pages/ProjectsPage.razor`
- `repo://src/Modules/CanDoItAll.Modules.Projects/Pages/Components/ProjectsBoard.razor`
- `repo://src/Modules/CanDoItAll.Modules.Projects/Pages/Components/ProjectModalHost.razor`
- `repo://src/Modules/CanDoItAll.Modules.Projects/ProjectModels.cs`
- `repo://tests/Components/CanDoItAll.Tests.Components/ProjectsPageTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/ProjectsServiceIntegrationTests.cs`

## Deliverables

- Pure `ProjectFileFilterProjection`/hierarchy closure shared by cards and Files pane; directly tested.
- Files tab/pane uses deterministic ordered project IDs, hierarchy/include-subprojects, binding/source revisions, and catalog revision fingerprint.
- Card Files action and focused `ProjectFilesDialog`; do not enlarge `ProjectModalHost`.
- Source-set update invalidates/replaces stale location/session state correctly.
- Shared component wrappers and desktop browser proof for filters -> files -> open flow.

## Dependency Impact

- SB13 and Resources project aggregates reuse the accepted project-scope semantics.

## Validation Depth

- Proof tier: `Behavioral`.
- User-visible project aggregate story.

## Implementation Steps

1. Characterize existing filters/hierarchy/card callbacks.
2. Extract pure shared projection and fingerprint with positive/negative/cycle-safe hierarchy tests.
3. Add focused Files pane/dialog/action using SB10 seam.
4. Prove filter/source changes and stale-session disposal.
5. Use managed watch/Playwright desktop proof and run architecture gate.

## C# Architecture Impact

- Responsibility extraction from broad Projects owners into focused top-level types/components.

## Boundary Ownership

- Projects owns projection/UI contract consumption; Workbench implementation is injected through neutral contract; no reverse edge.

## Dependency Direction

- Projects must not reference Workbench/Resources; refresh graph.

## Pattern Decision

- PSR-05 focused projection/pane/dialog; no broad workspace facade.

## Testability Contract

- Projection/fingerprint/scope/source update directly tested without page; component/browser prove wiring.

## Partial Class Policy

- No partial; no nested policy in page/component.

## Architecture Proof Required

- Before/after page/board responsibilities, project refs/cycles, direct tests, no duplicate filters/source truth, Components choices, C# gate.

## Scope Exceptions

- No canvas/process/resource/edit story.

## Do Not Do

- Do not copy filters into Files pane, wrap the entire board in tabs while hiding filter controls, retain invalid location, or add Files behavior to `ProjectModalHost`.

## Acceptance Checklist

- [x] Cards/Files use identical directly tested projection.
- [x] Include-subprojects/fingerprint/source replacement works.
- [x] Card dialog browses/searches/opens authorized file.
- [x] Empty/error/stale/unauthorized cases pass.
- [x] Desktop UI/console/network and C# gate pass.

## Proof Required

- Behavioral service/component/host/Playwright evidence, meaningful hierarchy/source-stale negative, DOM/screenshots/review, dependency/source assertions.

## Browser Validation Logging

- Route `/projects`; viewports `1900x1200`, `1440x900`.
- Exercise shared filters, Files tab, include subprojects, source change, card Files dialog, known-file open, no results/error, open overlay/menu, close/reopen.
- Assert filter parity, source fingerprint effect, one scroll owner, no clipping/lateral overflow, zero unexpected console/page/network errors.

## Progression Gate

- SB13 enters after the story's C# gate and downstream project-scope reuse smoke pass.

Progression decision: `Pass`. Behavioral proof is `bundle://proof/SB12/behavioral-proof.md`; final focused architecture snapshot is `snap-20260713091027-759c0917`. SB13 is unlocked.

## Reopen Triggers

- Filter divergence, wrong project source set, stale location, reverse reference, dialog lifecycle/visual defect reopens SB12 and dependent project aggregates.

## Suggested Agent Prompt

```text
Extend only the accepted pilot to the Projects Files tab and project-card Files dialog. Extract one shared filter/hierarchy projection and deterministic source fingerprint, keep existing large owners thin, and prove desktop behavior and meaningful stale/unauthorized negatives.
```
