# App and module migration from duplicated utilities and custom CSS

## Status

- `Blocked`

## Objective

- Migrate non-canvas app and module surfaces from repeated raw utility strings and safe custom CSS to the shared Tailwind classes and BaseLib primitives, while recording measurable duplication reduction and preserving behavior.

## Covered Inputs

- `REQ-01`, `REQ-03`, `REQ-04`, `REQ-12`, `REQ-13`, `REQ-16`, `REQ-17`, `REQ-18`
- Raw prompt steps `5` and `6`

## Prerequisites

- Subbundles `01`, `02`, and `03` completed with passing gates.
- Shared Tailwind families and BaseLib primitives are stable enough for page migration.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\ProjectsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Resources\Pages\ResourcesPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Prompts\Pages\PromptGalleryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\Components\ProjectStructureAgentSettingsPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components\Components\AppShell.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayout.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\ReconnectModal.razor.css`

## Deliverables

- Repeated raw markup families replaced by shared Tailwind classes or BaseLib primitives on the targeted routes.
- Safe custom CSS reduced, removed, or converted to Tailwind-backed shared classes.
- Measured progress showing occurrences replaced, near-duplicates unified, and code or selector reduction achieved.

## Dependency Impact

- The final closure audit depends on this phase to produce the actual route-level migration evidence.
- Weak proof here would make the final answers to the step `0` questions dishonest.

## Validation Depth

- `UI, build, and browser-proof`

## Implementation Steps

1. Migrate the highest-churn route files first so the duplication reduction is meaningful.
2. Replace safe repeated raw utility families with BaseLib primitives or shared semantic classes.
3. Review custom CSS hotspot files and remove or convert only the rules that are safe to absorb into the shared system.
4. Record progress metrics after the main migration wave.
5. Browser-validate the migrated route matrix and repair regressions immediately.

## Scope Exceptions

- Any custom CSS or markup family that is proven unsafe to migrate in this wave must be documented explicitly in the execution report instead of being silently skipped.

## Do Not Do

- Do not edit CanvasLib or canvas-host surfaces.
- Do not keep duplicated utility strings when a proven shared class or BaseLib primitive now exists.
- Do not delete behavior-specific CSS without browser proof.

## Acceptance Checklist

- Targeted routes consume shared classes and BaseLib primitives for the repeated families identified in the census.
- Safe custom CSS hotspots are materially reduced.
- Metrics for replaced occurrences, unified families, and file or selector reduction are recorded.
- Browser validation shows readable text, correct wrapping, and no overlapping or clipping on the migrated routes.

## Proof Required

- `npm run build` from `C:\repositories\CanDoItAll\Tailwind`
- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx`
- Playwright screenshots for `/projects`, `/resources`, `/prompts`, `/validation`, `/activity`, and `/settings`
- Updated metrics in the execution report and workbook refresh if the census materially changes

## Browser Validation Logging

- Target routes: `/projects`, `/resources`, `/prompts`, `/validation`, `/activity`, `/settings`
- Required viewports: `1600x960`, `1280x900`, `1024x768`
- Required Playwright actions: navigate, interact with migrated controls where needed, evaluate text wrapping or overflow when risk exists, and capture screenshots
- Required screenshot findings: no overlapping content, no clipped controls, no broken wrapping, coherent spacing, and readable text without zooming

## Progression Gate

- All targeted migrated routes have populated browser-analytics rows with screenshots and explicit results.
- Progress metrics are updated with facts.
- Any intentionally deferred unsafe migration is documented explicitly.

## Suggested Agent Prompt

```text
Migrate the highest-value non-canvas routes and safe custom CSS hotspots onto the shared Tailwind and BaseLib style system, record the real reduction metrics, and browser-validate the route matrix before closing the migration phase.
```
