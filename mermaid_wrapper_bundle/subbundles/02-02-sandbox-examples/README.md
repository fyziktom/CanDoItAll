# 02-sandbox-examples

## Status

- `Completed`

## Objective

Add a new Mermaid page/group to the component sandbox that demonstrates the wrapper with flowchart, architecture-beta, click events, pan/zoom, and syntax error handling.

## Covered Inputs

- N004, N006, N007, N008
- Requirements R004, R005, R006, R007

## Prerequisites

- Subbundle 01 closure gate passed.
- `CanDoItAll.Components.Mermaid` package builds and can be referenced by the sandbox.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\CanDoItAll.Components.Sandbox.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Program.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\_Imports.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\SandboxCatalogRegistry.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\Layout\MainLayout.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\Pages\Charts.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\Pages\LayoutComposition.razor`
- `C:\repositories\mermaid\docs\syntax\architecture.md`

## Deliverables

- Sandbox project reference/import/service registration for Mermaid package.
- New `SandboxGroupKey.Mermaid` group and examples.
- New `/groups/mermaid` page using existing sandbox and BaseLib patterns.
- Examples for flowchart node click, architecture-beta, pan/zoom controls, and invalid syntax.
- Callback log panel showing clicked node metadata.
- Error panel showing syntax location details.

## Dependency Impact

- Final closure proof depends on this page for real browser validation.
- If this page is visually or interactively weak, the raw user request cannot be closed.

## Validation Depth

- `Critical UI proof`
- Build plus Playwright route proof on desktop and narrow width.

## Implementation Steps

1. Add project reference/import/service registration for `CanDoItAll.Components.Mermaid`.
2. Extend `SandboxGroupKey` and `SandboxCatalogRegistry` with a Mermaid group and examples.
3. Create `Components/Pages/Mermaid.razor` at `/groups/mermaid`.
4. Use `CatalogPageFrame`, `Grid`, `Stack`, `SectionCard`, `SummaryTiles`, `TextArea`, `Button`, and `Alert` rather than one-off layout wrappers where possible.
5. Include a valid flowchart sample with clickable nodes.
6. Include a valid architecture-beta sample from Mermaid v11.14.0 syntax.
7. Include an invalid syntax sample or mode that visibly exercises the wrapper error panel.
8. Add tests for sandbox registry coverage if practical.
9. Run build and browser proof.

## Scope Exceptions

- The sandbox does not need to become a full Mermaid editor; it needs enough editing/selection to prove the wrapper behaviors.

## Do Not Do

- Do not copy Mermaid docs wholesale into the page.
- Do not add decorative hero/marketing layout.
- Do not bypass the wrapper by calling Mermaid directly from the sandbox page.

## Acceptance Checklist

- Sandbox navigation includes Mermaid.
- `/groups/mermaid` loads.
- Flowchart and architecture-beta render nonblank SVG.
- Clicking a rendered node updates a visible Blazor callback log.
- Pan/zoom controls change the rendered viewport.
- Invalid syntax displays message and best-effort location.
- Large and narrow layouts are readable with no clipping or overlap.

## Proof Required

- `dotnet build src/CanDoItAll.Components.Sandbox/CanDoItAll.Components.Sandbox.csproj`
- Playwright desktop pass at approximately `1600x900`: navigate, inspect rendered SVG, click node, zoom, pan, trigger invalid syntax, screenshot.
- Playwright narrow pass at approximately `390x844`: inspect layout and screenshot.
- Screenshot review answers for readability, clipping, alignment, space use, and existing sandbox fit.

## Browser Validation Logging

- Route: `/groups/mermaid`
- Viewports: `1600x900` and `390x844`
- Actions: navigate, evaluate SVG count/text, click a node, press zoom controls, drag or programmatically pan, switch to invalid syntax/error sample, capture screenshots.
- Execution report must include screenshot paths and pass/fail result.

## Progression Gate

- Downstream final closure may continue only after real browser proof shows rendered diagrams, node click callback, pan/zoom, and syntax error display all working.

## Suggested Agent Prompt

```text
Implement subbundle 02 only. Add the Mermaid sandbox group/page using the wrapper from subbundle 01, prove flowchart and architecture-beta rendering, node click callback, pan/zoom, and error display in a browser, then update browser analytics and gate results.
```
