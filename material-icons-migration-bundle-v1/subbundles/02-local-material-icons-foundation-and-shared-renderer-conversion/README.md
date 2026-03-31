# Local Material Icons Foundation And Shared Renderer Conversion

## Status

- `Ready`

## Objective

- Vendor the Material Icons assets locally, remove the remote runtime dependency, and replace the shared icon renderer foundation so downstream migrations target one local Material icon system.

## Covered Inputs

- `N002` Switch icons in CanDoItAll to Google Material Icons.
- `N003` Keep the icon system as part of the solution, not an external resource.
- `N006` Put the foundation in the shared component layer, preferably BaseLib.

## Prerequisites

- `subbundles/01-icon-census-tracker-workbook-and-migration-map` completed and trusted.

## Exact Source References

- `C:/repositories/CanDoItAll/src/CanDoItAll.Web/Components/App.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components.BaseLib/Components/Identity/Icon.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components.BaseLib/Components/Identity/FontAwesomeIconCatalog.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components/Components/Icon.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components/Components/FontAwesomeIconCatalog.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components.BaseLib/CanDoItAll.Components.BaseLib.csproj`

## Deliverables

- Local Material Icons asset files checked into the solution and wired through static web assets.
- `App.razor` updated so runtime icon delivery is local.
- Shared `Icon` infrastructure updated to render Material Icons markup and token compatibility through a Material-oriented alias path.

## Dependency Impact

- Every later subbundle depends on this runtime foundation being correct.
- If this phase is weak, downstream CSS updates, token mapping, and route proof become unreliable.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Add the local Material Icons stylesheet and font files under solution-owned static assets.
2. Replace the remote Font Awesome link in `App.razor` with the local asset reference.
3. Convert the shared `Icon` implementations and supporting token catalog logic from Font Awesome output to Material Icons output.
4. Verify the new class contract and runtime asset path before downstream component work begins.

## Scope Exceptions

- None are acceptable here; if the runtime still depends on a remote icon asset, this phase remains open.

## Do Not Do

- Do not keep a CDN fallback in place.
- Do not leave old Font Awesome-specific output classes as the primary render path.
- Do not change page-level call sites here unless required to keep the shared foundation compiling.

## Acceptance Checklist

- No runtime icon stylesheet is loaded from outside the solution.
- Shared `Icon` renderers no longer emit Font Awesome markup.
- The solution still compiles after the foundation swap.
- The workbook rows for external asset delivery and shared renderers are updated.

## Proof Required

- `dotnet build C:/repositories/CanDoItAll/CanDoItAll.slnx`
- Browser proof on `/` and `/groups/foundations`
- Desktop and narrower-width screenshots showing the local Material icon path rendering correctly
- Evidence that `App.razor` no longer points at the remote Font Awesome stylesheet

## Browser Validation Logging

- Route: `/`, `/groups/foundations`
- Viewports: `1600x900` first pass, then `768x1024`
- Actions: load the routes, inspect representative icons, and capture screenshots after the local asset path resolves
- Screenshots: record the actual file paths in `reviews/01-execution-report.md`
- Review questions: confirm no missing glyphs, no fallback text, and correct icon alignment in shared primitives

## Progression Gate

- Do not start subbundle `03` until the remote stylesheet is gone, the shared icon renderer outputs the new Material contract, and the foundation screenshots look correct.

## Suggested Agent Prompt

```text
Implement only subbundle 02. Vendor local Material Icons assets, remove the remote stylesheet, convert the shared Icon foundation, and prove the change on the shared foundation routes before moving on.
```
