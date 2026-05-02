# 01-mermaid-component-package

## Status

- `Completed`

## Objective

Create `CanDoItAll.Components.Mermaid`, a first-party Razor component package that renders official Mermaid.js diagrams from a downloaded CDN asset and exposes Blazor callbacks, pan/zoom, and structured error display.

## Covered Inputs

- N001, N002, N003, N005, N006, N007, N008
- Requirements R001, R002, R003, R004, R005, R006

## Prerequisites

- Bundle prepared gate passed.
- Mermaid local clone version confirmed at `C:\repositories\mermaid\packages\mermaid\package.json`.

## Exact Source References

- `C:\repositories\CanDoItAll\CanDoItAll.slnx`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Charts\CanDoItAll.Components.Charts.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Charts\Components\CdaChart.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Charts\Components\ChartsHeadAssets.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Charts\Infrastructure\ServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ChartsWrapperTests.cs`
- `C:\repositories\mermaid\packages\mermaid\package.json`
- `C:\repositories\mermaid\packages\mermaid\src\mermaidAPI.ts`
- `C:\repositories\mermaid\packages\mermaid\src\errors.ts`

## Deliverables

- New Razor class library `src/CanDoItAll.Components.Mermaid`.
- Official Mermaid v11.14.0 ESM file downloaded from CDN under package `wwwroot`.
- Source metadata file documenting CDN URL, version, and download date.
- JS interop module that renders Mermaid, normalizes errors, attaches node click handlers, and supports pan/zoom.
- C# models for render options, render result, node click event args, and syntax errors.
- `MermaidDiagram` component with visible loading/error states and optional controls.
- Service registration extension if needed by local pattern.
- Targeted tests for C# models/component markup/static metadata.

## Dependency Impact

- Subbundle 02 cannot prove sandbox behavior without this wrapper.
- Subbundle 04 final browser proof depends on this package being stable.
- If the static vendor asset path is wrong, all consumers fail at runtime.

## Validation Depth

- `Critical foundation`
- Build, targeted component/model tests, static asset existence check, and dependent sandbox reference smoke.

## Implementation Steps

1. Create `src/CanDoItAll.Components.Mermaid` as `Microsoft.NET.Sdk.Razor`, targeting `net10.0`.
2. Download Mermaid v11.14.0 ESM from official CDN into `wwwroot/js/vendor/mermaid.esm.min.mjs`.
3. Add vendor metadata with URL/version and do not alter the vendor file.
4. Add `MermaidDiagram.razor`, scoped CSS or package CSS, JS module, and public models.
5. Add JS module logic for render, destroy, zoom in/out/reset, pan/drag, node click capture, and error normalization.
6. Add package `_Imports.razor` and public namespace markers as needed.
7. Add project to `CanDoItAll.slnx`.
8. Add tests under `tests/CanDoItAll.Tests.Components` and project reference to the new package.
9. Run targeted build/test commands and record results.

## Scope Exceptions

- No .NET-side Mermaid parser in this phase.
- Browser rendering proof is planned in subbundle 02/04 after the sandbox route exists.

## Do Not Do

- Do not use existing Blazor Mermaid wrapper libraries.
- Do not build Mermaid from `C:\repositories\mermaid`.
- Do not edit the downloaded vendor Mermaid file.
- Do not make the sandbox page in this subbundle except for any minimal compile reference needed by a smoke test.

## Acceptance Checklist

- `CanDoItAll.Components.Mermaid` exists and builds.
- Vendor Mermaid asset exists and metadata records official CDN source.
- `MermaidDiagram` exposes source/config, click callback, pan/zoom controls, render result, and error callback.
- Errors render visibly with message and best-effort location details.
- Tests cover model defaults, error formatting, and component markup surfaces.

## Proof Required

- `dotnet build src/CanDoItAll.Components.Mermaid/CanDoItAll.Components.Mermaid.csproj`
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter Mermaid`
- File existence proof for `wwwroot/js/vendor/mermaid.esm.min.mjs` and vendor metadata.

## Browser Validation Logging

- Route: N/A for this subbundle because the sandbox route is delivered in subbundle 02.
- Record N/A in browser analytics, but closure must state that browser proof is deferred to the dependent sandbox subbundle.

## Progression Gate

- Downstream subbundles may continue only after the component package builds, tests pass, vendor asset is present, and the sandbox can reference the package without compile errors.

## Suggested Agent Prompt

```text
Implement subbundle 01 only. Add the CanDoItAll.Components.Mermaid Razor package using official Mermaid v11.14.0 from CDN as a static asset, expose render/error/click/pan/zoom APIs, add targeted component tests, and update the execution report. Do not create the sandbox Mermaid page yet.
```
