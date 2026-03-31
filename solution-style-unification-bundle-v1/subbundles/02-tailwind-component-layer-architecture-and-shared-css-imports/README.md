# Tailwind component layer architecture and shared CSS imports

## Status

- `Completed`

## Objective

- Restructure `Tailwind/input.css` into imported responsibility-based files, move shared semantic classes into those files, and prove that the shared styling foundation still renders correctly on representative non-canvas routes.

## Covered Inputs

- `REQ-01`, `REQ-02`, `REQ-03`, `REQ-08`, `REQ-09`, `REQ-17`, `REQ-18`
- Raw prompt steps `2` and `3`

## Prerequisites

- Subbundle `01` completed with workbook, taxonomy, and exclusion list.
- The highest-frequency style families are named and prioritized.

## Exact Source References

- `C:\repositories\CanDoItAll\Tailwind\input.css`
- `C:\repositories\CanDoItAll\Tailwind\package.json`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\wwwroot\css\output.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayout.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components\Components\AppShell.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\ProjectsPage.razor`

## Deliverables

- Imported Tailwind file structure grouped by responsibility.
- Shared semantic classes for the canonical families identified in subbundle `01`.
- Compatibility mappings for existing shared classes that still have consumers.
- Passing Tailwind build output wired into BaseLib static assets.

## Dependency Impact

- Subbundles `03` and `04` both depend on these imports being the stable source of truth.
- If the import architecture or shared classes are wrong, later BaseLib and page migrations will either duplicate styles or regress shell-level layout.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Split `Tailwind/input.css` into imported files grouped by responsibility.
2. Move existing shared semantic classes out of the monolithic block into the new files.
3. Add canonical semantic classes for the highest-frequency repeated families from the census.
4. Rebuild Tailwind output and verify the compiled static asset path stays correct.
5. Browser-validate the shell and one representative content-heavy page before allowing BaseLib changes.

## Scope Exceptions

- BaseLib markup migration is deferred to subbundle `03`.

## Do Not Do

- Do not start wide page/module migration here.
- Do not change CanvasLib or canvas-host surfaces.
- Do not rename existing shared classes purely for prefix cleanliness if that adds churn without reuse value.

## Acceptance Checklist

- `Tailwind/input.css` is import-based and no longer carries the entire shared component layer inline.
- Shared canonical families exist for the highest-frequency repeated patterns.
- Tailwind build succeeds and produces updated `output.css`.
- Representative routes load without immediate shell, spacing, or form regressions.

## Proof Required

- `npm run build` from `C:\repositories\CanDoItAll\Tailwind`
- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx`
- Playwright screenshots for `/` and `/projects`
- Execution-report browser analytics row populated for this subbundle

## Browser Validation Logging

- Target routes: `/`, `/projects`
- Required viewports: `1600x960` and `1280x900`
- Required Playwright actions: navigate, wait for route content, snapshot, and screenshot
- Required screenshot findings: no clipped shell chrome, no broken header/navigation spacing, no field/button regressions on the representative page

## Progression Gate

- Tailwind build and solution build both pass.
- Desktop screenshots for `/` and `/projects` show no shell or shared-control regressions.
- The execution report records the route proof and screenshot paths.

## Suggested Agent Prompt

```text
Restructure the Tailwind component layer into imported files, move and extend the shared semantic classes for the top repeated families, rebuild Tailwind output, and browser-validate the shell and a representative non-canvas page before closing the foundation.
```
