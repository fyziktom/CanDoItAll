# Sandbox layout example page and registry updates

## Status

- `Completed`

## Objective

- Create a dedicated CanDoItAll components sandbox page that preserves the four layout-comparison hero variants as a layout-composition reference and register it in the sandbox catalog.

## Covered Inputs

- User request to move the comparison examples onto their own sandbox page.
- User requirement to keep the examples available for experimentation and future layout debugging.
- User preference for shared layout components over page-local structural CSS.

## Prerequisites

- Subbundle `01-zyphonote-cleanup-and-responsive-progress-preservation` may proceed in parallel, but this page must become the new home for the removed examples before final closure.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\SandboxCatalogRegistry.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\Pages\Layout.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\Pages`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\CatalogPageBase.cs`

## Deliverables

- New sandbox page dedicated to the layout-composition comparison examples.
- Catalog entry or example route that makes the page discoverable through the Layout group.
- Shared-component-based comparison sections that demonstrate Stack, Grid, Grid+Row+Column, and responsive Row/Column composition.

## Dependency Impact

- The components MCP should point to this sandbox page as the curated layout-composition reference.
- Weak sandbox coverage here would leave the MCP guidance without a canonical proof page.

## Validation Depth

- `UI, component-test, and browser-proof`

## Implementation Steps

1. Create a dedicated sandbox page under the Layout group for layout-composition comparison.
2. Register the page in `SandboxCatalogRegistry` with a distinct example id and route.
3. Keep the page focused on composition proof rather than product branding.
4. Validate the new route at desktop and narrow widths.

## Scope Exceptions

- Do not redesign the main `Layout.razor` group page beyond adding discoverability if needed.
- Do not turn the comparison page into a generic documentation essay.

## Do Not Do

- Do not leave the comparison examples stranded only in Zyphonote.
- Do not introduce a one-off sandbox structure that bypasses the catalog conventions.
- Do not depend on custom CSS for grid structure when the shared components can express it.

## Acceptance Checklist

- The sandbox has a dedicated route for the layout-composition comparison page.
- The route is discoverable through the sandbox catalog metadata.
- The page includes all four comparison variants and labels the responsive Row/Column version as the preferred composition.
- The page renders correctly in desktop and narrow viewports.

## Proof Required

- `dotnet build C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\CanDoItAll.Components.Sandbox.csproj -v:minimal`
- Browser screenshot for the new sandbox route at desktop width.
- Narrow-width browser pass for the same route.
- Registry or MCP-visible evidence that the new example route is discoverable.

## Browser Validation Logging

- Target route: sandbox layout comparison route created in this subbundle
- Required viewports: maximized desktop and `390px`
- Required actions: open the route, review all comparison sections, confirm the responsive example stacks correctly
- Required evidence paths: screenshot artifacts captured during validation and execution report entries
- Required review questions:
- Do the four versions remain visually comparable on one page?
- Is the responsive Row/Column version clearly the recommended pattern?

## Progression Gate

- The sandbox comparison route must exist, render, and be registered in the catalog before the MCP guidance subbundle can close.

## Suggested Agent Prompt

```text
Implement this subbundle only. Create a dedicated sandbox layout-composition page that hosts the four comparison hero variants, register it in the Layout catalog group, and prove the route at desktop and mobile widths.
```
