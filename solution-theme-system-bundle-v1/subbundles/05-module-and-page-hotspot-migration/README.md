# module-and-page-hotspot-migration

## Status

- `Ready`

## Objective

- Clean up the highest-value route and module hotspots that still hard-code palette utilities or depend on unstable shared prefixes after the BaseLib contract is ready.

## Covered Inputs

- `N02`, `N05`, `N06`
- `R05`, `R06`, `R07`, `R08`

## Prerequisites

- Subbundles `01` through `04` completed and trusted

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Resources\Pages\ResourcesPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Prompts\Pages\PromptGalleryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\Components\ProjectModalHost.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayout.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectCalendarPage.razor`
- `C:\repositories\CanDoItAll\solution-theme-system-bundle-v1\inventories\03-color-hotspot-summary.md`
- `C:\repositories\CanDoItAll\solution-theme-system-bundle-v1\inventories\04-validation-route-matrix.md`

## Deliverables

- Reduced direct palette utility usage on the chosen route matrix
- Real routes using shared theme-aware primitives or shared semantic selectors
- Updated hotspot notes in the execution report

## Dependency Impact

- Prefix stabilization and final closure depend on real routes using the new contract instead of only demo surfaces.

## Validation Depth

- `UI, build, and browser-proof`

## Implementation Steps

1. Prioritize routes with both high color-hotspot counts and strong BaseLib overlap.
2. Replace route-local palette utilities with shared theme-aware primitives or semantic selectors.
3. Keep the route structure readable and avoid large layout refactors outside the theme/prefix scope.
4. Record which hotspots were intentionally deferred.

## Scope Exceptions

- Canvas-only routes and surfaces remain outside this migration pass.

## Do Not Do

- Do not invent a new page-level theme layer that bypasses BaseLib.
- Do not do broad unrelated layout surgery.

## Acceptance Checklist

- The chosen routes reduce direct palette utility usage.
- Changed routes visibly follow the shared theme contract.
- Any deferred hotspots are named explicitly.

## Proof Required

- Solution build
- Large-screen screenshots for each migrated route
- Narrow-width pass for any route whose layout changes materially

## Browser Validation Logging

- Target routes: `/resources`, `/prompt-gallery`, `/settings`, and one additional route chosen from `/` or `/projects/...`
- Viewports: `1600x1000` and at least one narrow-width pass for layout-sensitive routes
- Required actions: navigate, inspect the migrated shared surfaces, and capture screenshots after the theme contract is active
- Evidence paths: `evidence/theme-resources-desktop.png`, `evidence/theme-prompt-gallery-desktop.png`, `evidence/theme-settings-desktop.png`
- Review questions: Did the route stop encoding its own palette, and does it still feel coherent with the rest of the app after the migration?

## Progression Gate

- The route matrix must show that at least the highest-value hotspots now rely on the shared contract and remain visually stable across the required screenshots.

## Suggested Agent Prompt

```text
Implement this subbundle only. Clean up the highest-value route hotspots without drifting into unrelated layout refactors. Use the shared theme contract that earlier subbundles established.
```
