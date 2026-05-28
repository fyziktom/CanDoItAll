# Shared Components Governance

## Ownership

- `CanDoItAll.Components.Common`, `CanDoItAll.Components.BaseLib`, `CanDoItAll.Components.CanvasLib`, `CanDoItAll.Components.Charts`, `CanDoItAll.Components.Mermaid`, `CanDoItAll.Components.OverlayLib`, `CanDoItAll.Components.WebGlLib`, and `CanDoItAll.Components.Sandbox` are owned from the sibling `C:\repositories\CanDoItAll.Components` repo.
- The main CanDoItAll repo consumes those libraries as private NuGet packages from `ExternalPackages`.
- The `CanDoItAll.Components` facade and `CanDoItAll.Components.WebGlSandbox` remain in the main repo because they still depend on main solution projects.
- Zyphonote or any other downstream repo must request shared-library changes through the `Requests` folders in the owning library instead of editing shared code from the consumer repo.

## Runtime Boundaries

- `CanDoItAll.Components.BaseLib` is the default home for reusable product UI primitives and service registration.
- `CanDoItAll.Components.CanvasLib`, `CanDoItAll.Components.OverlayLib`, and `CanDoItAll.Components.WebGlLib` are specialized libraries for graph/canvas, floating overlay, and WebGL workbench surfaces.
- `CanDoItAll.Components` is a facade and app-shell layer. Do not move ordinary shared components into it by default.
- `CanDoItAll.Components.Sandbox` and `CanDoItAll.Components.WebGlSandbox` are the approved homes for preview, demo, tuning, browser-proof, and fake-data component assets.
- Runtime libraries must not keep catalog-only components, preview cards, or tuning boundaries.
- App-specific styling must stay in the app-specific repo or app-specific library. Shared component styling must stay in the components repo Tailwind workspace and be shipped with BaseLib.

## Promotion Rule

Keep UI local to a module until it has a real cross-module consumer or clearly belongs to a shared surface such as BaseLib, CanvasLib, OverlayLib, WebGlLib, or the app shell. Shared components must expose typed parameters and focused child content instead of forcing consumers to rebuild behavior with raw markup and arbitrary class strings.
