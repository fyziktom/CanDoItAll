# Non-Canvas App And Module Icon Adoption

## Status

- `Ready`

## Objective

- Carry the Material icon migration through the application and module routes outside the final Workbench closure phase, updating remaining token sources and raw icon surfaces while the workbook stays current.

## Covered Inputs

- `N004` Map all places where `Icon.razor` or pure icons are used and replace them.
- `N005` Use the tracker to show what is done and what still needs change.

## Prerequisites

- `subbundles/01-icon-census-tracker-workbook-and-migration-map` completed and trusted.
- `subbundles/02-local-material-icons-foundation-and-shared-renderer-conversion` completed and trusted.
- `subbundles/03-baselib-and-legacy-shared-component-icon-migration` completed and trusted.

## Exact Source References

- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Activity/Pages/ActivityPage.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Automation/Pages/AutomationPage.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Factory/Pages/Components/PromptFactoryHistoryToolbar.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Projects/Pages/ProjectsPage.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Prompts/Pages/PromptGalleryPage.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.TestLab/Pages/TestLabPage.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Validation/Pages/ValidationCenterPage.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor`

## Deliverables

- Route-level icon adoption across the non-Workbench module surfaces.
- Remaining raw glyph and token-driven icon surfaces in these routes migrated or explicitly mapped to Material tokens.
- Workbook rows for these module surfaces updated with status and mapping notes.

## Dependency Impact

- Later closure depends on these routes being stable so Workbench proof can focus on its own icon-heavy surfaces instead of still carrying shared app regressions.
- If these routes still contain leftover Font Awesome or raw glyph output, the final closure report cannot honestly say the solution-wide migration is complete.

## Validation Depth

- `UI, component-test, and browser-proof`

## Implementation Steps

1. Review the workbook rows assigned to the non-Workbench routes and module files.
2. Replace remaining route-level raw icon spans, toolbar glyphs, and token-driven icon sources with Material icon equivalents.
3. Update any route-specific CSS or markup coupling that still assumes the old icon output.
4. Mark the affected workbook rows with status and mapping notes.

## Scope Exceptions

- Workbench-specific and canvas-heavy route proof is deferred to subbundle `05`, but do not leave obvious shared-route leftovers behind in this phase.

## Do Not Do

- Do not reopen shared component abstractions here unless execution proves subbundle `03` was weak.
- Do not mark workbook rows complete if the route still renders fallback text or mixed icon systems.
- Do not treat Prompt Factory toolbar glyph swaps as optional just because the page is complex.

## Acceptance Checklist

- The targeted module routes render Material icons instead of leftover raw glyph text.
- Workbook statuses for the touched routes and files are updated.
- No touched route reintroduces Font Awesome-specific classes.
- Browser screenshots show stable icon sizing and alignment on desktop and narrow viewports.

## Proof Required

- `dotnet build C:/repositories/CanDoItAll/CanDoItAll.slnx`
- Browser proof on `/activity`, `/automation`, `/prompt-factory`, `/projects`, `/prompt-gallery`, `/resources`, `/test-lab`, `/validation`, and `/settings`
- Desktop and narrower-width screenshots covering the changed route clusters
- Workbook updates showing the completed versus remaining rows for this route family

## Browser Validation Logging

- Route: `/activity`, `/automation`, `/prompt-factory`, `/projects`, `/prompt-gallery`, `/resources`, `/test-lab`, `/validation`, `/settings`
- Viewports: `1600x900` first pass, then `768x1024`
- Actions: navigate each route, open or hover the controls that expose the changed icons, and capture screenshots for representative states
- Screenshots: record the actual file paths in `reviews/01-execution-report.md`
- Review questions: confirm route-level toolbars, cards, and actions show the right Material icons with no clipping or fallback text

## Progression Gate

- Do not start subbundle `05` until the non-Workbench route matrix is stable and the workbook clearly shows which rows are still reserved for Workbench and canvas closure.

## Suggested Agent Prompt

```text
Implement only subbundle 04. Migrate the remaining non-Workbench app and module icon surfaces, keep the workbook current, and prove the route matrix on desktop and narrow viewports before moving on.
```
