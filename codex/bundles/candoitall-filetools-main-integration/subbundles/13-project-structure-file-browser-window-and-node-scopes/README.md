# SB13 Project Structure File Browser Window And Node Scopes

## Status

- `Completed`

## Objective

- Preserve direct asset-node double-click dialogs and add authorized project/node collection browsing through a focused compact floating window and action coordinator, without growing the partial-page cluster.

## Covered Inputs

- N010-N017; R019, R024-R040.

## Prerequisites

- SB12 Completed; SB07-SB11 foundations trusted.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectStructureCanvasDialogs.razor`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructureNodeHelpers.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.ToolWindows.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureLocalFileOpener.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureNodeActionCapabilityResolver.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructureActionCatalogAdapter.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureMenuComposition.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProjectStructureLocalFileOpenerManagedFilesTests.cs`
- `repo://tests/Components/CanDoItAll.Tests.Components/ProjectStructureActionCatalogAdapterTests.cs`
- `repo://tests/Playwright/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectStructureQuickActions.cs`
- `bundle://architecture/10-performance-and-scale.md`

## Deliverables

- Top-level project/node file-scope resolver that authorizes supported semantic metadata and rejects arbitrary absolute paths.
- `ProjectStructureFileActionCoordinator` unifying `browse-files` invocation; no duplicated switch arms across page/menu/adapter/quick actions.
- Focused `ProjectStructureFileBrowserWindow` inside `CanvasFloatingWindow`, explicit Compact/Minimal mode, persisted typed constant key, independent session lifetime, host-owned Include subprojects.
- `open-local` remains separate from browse authorization.
- Characterization for existing `HandleNodeOpenedAsync`/`OpenAttachmentPreview` image and PDF behavior. Its dialog receives one authorized known-file interaction and renders FileInteraction directly with zero FileBrowser catalog/session/browse/search/cache calls.
- Typed intent separation: double-click/open-known-asset is not `browse-files`; toolbar/context project/node collection actions are FileBrowser requests.
- Desktop floating-window overlay/scroll/layering and hostile metadata proof.

## Dependency Impact

- SB14/SB16 reuse node/run/interaction patterns; weak scope ownership can expose arbitrary workspace files.

## Validation Depth

- Proof tier: `Behavioral`.
- Security-sensitive UI story relying on governed SB07; story proof itself is Behavioral.

## Implementation Steps

1. Characterize local opener, current node actions, and image/PDF double-click/dialog/close/replacement behavior; add hostile metadata and accidental-browser-initialization failing tests.
2. Implement directly tested semantic scope resolver and coordinator.
3. Add action registrations through typed known-file versus browse-collection seams; do not merge them under a boolean/string mode.
4. Add focused floating-window component/session lifecycle with Components/CanvasLib guidance.
5. Prove project aggregate and supported node sources, unauthorized/absolute/stale cases, plus direct image/PDF FileInteraction with zero browser calls.
6. Run managed desktop browser/visual/console proof and C# gate.

## C# Architecture Impact

- New top-level services/component; strict no-new-partial policy.

## Boundary Ownership

- Workbench owns node meaning/window/dialog; outer integration authorizes/effects; FileInteraction owns one-file rendering; FileBrowser owns collection discovery; Processes policy stays out.

## Dependency Direction

- Workbench consumes neutral integration; no new Projects/Resources reverse edge or process-policy ownership.

## Pattern Decision

- PSR-05 focused coordinator/window; simple handler/catalog registration, not a new command hierarchy.

## Testability Contract

- Resolver/coordinator direct tests without page/canvas host; component/browser prove window integration.

## Partial Class Policy

- No new `ProjectStructurePage.*.cs`. Existing partial may receive only minimal typed callback/state delegation if unavoidable, with line/member delta proof.

## Architecture Proof Required

- No-new-partial/source-switch audit, scope ownership, direct tests, dependency/cycle, page responsibility delta, Components/Canvas usage, C# gate.

## Scope Exceptions

- Only supported semantic node roots; no arbitrary local explorer replacement. A known asset uses direct interaction even if siblings exist.

## Do Not Do

- Do not reuse path existence/local opener as authority, route asset double-click through FileBrowser, add parallel action branches, put session in page lifecycle hooks, or tune small/medium screens.

## Acceptance Checklist

- [x] Supported project/node scopes browse and open through authorized seam.
- [x] Image/PDF double-click still opens the existing dialog lifecycle with direct FileInteraction and zero FileBrowser calls.
- [x] Arbitrary absolute/escaped/stale metadata fails before provider.
- [x] No new partial or duplicated action logic.
- [x] Floating window has one scroll owner and unclipped overlays.
- [x] Desktop browser/console/C# gate pass.

## Proof Required

- Behavioral resolver/component/browser positives, hostile metadata/stale/unsupported negatives, DOM/geometry/screenshots/review, no-new-partial/dependency/source assertions.
- Shallow-pass trap: the dialog hides FileBrowser markup but still constructs a catalog/session or issues a browse/search call. A zero-call spy must fail that path. The realistic positive double-clicks image/PDF nodes into the existing direct interaction dialog and separately opens a multi-file collection action into the browser window.

## Browser Validation Logging

- Route `/projects/{ProjectId}/structure`; viewports `1900x1200`, `1440x900`.
- Double-click image/PDF nodes and verify the direct interaction dialog. Separately open toolbar/node collection browse actions, toggle include subprojects, browse/search/open, minimize/restore/move/resize within supported Canvas contract, open menu/popover, provoke error.
- Assert exactly one results scroll owner, fixed chrome, overlay z-order/clipping, no page lateral overflow, zero unexpected console/page/network errors.

## Progression Gate

- SB14 enters after C# gate and one process-owned root policy consumer smoke proves Workbench does not own process semantics.

## Reopen Triggers

- Arbitrary path access, asset double-click regression, any browser call on direct known-file open, action duplication, new partial, page-local session, overlay/scroll defect, or process-policy leakage reopens SB13 and affected downstream proof.

## Suggested Agent Prompt

```text
Preserve image/PDF node double-click and open its existing dialog with direct authorized FileInteraction and zero FileBrowser calls. Separately implement collection browsing through focused resolver/coordinator/window types. Add no page partial, keep open-local separate, and prove hostile metadata plus desktop behavior.
```
