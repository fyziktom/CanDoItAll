# Shared UI Component Boundary

Reusable UI libraries are owned by the sibling
[CanDoItAll.Components repository](https://github.com/fyziktom/CanDoItAll.Components).
This repository consumes released packages from NuGet.org and owns only
application-specific composition and styling.

## Ownership

The component repository owns:

- BaseLib, CanvasLib, Common, Gantt, Charts, Mermaid, OverlayLib, QRCode,
  WebGlLib, and WebGlRunLib;
- shared Tailwind tokens and the CSS shipped by BaseLib;
- component sandbox and WebGL sandbox sample applications;
- reusable component behavior, examples, and catalog documentation.

This repository owns:

- `src/UI/CanDoItAll.AppComponents`, the app-shell/facade Razor library;
- `src/App/CanDoItAll.Web/wwwroot/css/output.css` and other product-specific styling;
- module-specific UI that does not yet have a real cross-module consumer;
- composition of the packaged libraries in the web host.

`CanDoItAll.AppComponents` currently consumes BaseLib, CanvasLib, and Common `0.1.15`,
`Microsoft.AspNetCore.Components.Web` `10.0.10`, and the FileTools component contracts.
Its adjacent
[project file](../../src/UI/CanDoItAll.AppComponents/CanDoItAll.AppComponents.csproj)
is the authoritative dependency list.

`CanDoItAll.Components.Sandbox` `0.1.4` is the sole local-package exception because the
component preview tests render its sample components and the owner intentionally does
not publish that sample application.

## Change Rules

1. Use an existing typed component before introducing markup-heavy local substitutes.
2. Keep UI local until it has a real reusable boundary.
3. Change shared behavior, shared CSS, examples, and package versions in the component
   repository; do not copy their implementation into this consumer.
4. Keep application-only orchestration in `CanDoItAll.AppComponents` or the owning
   module.
5. Validate reusable changes in the component repository's sandbox and tests, publish
   the released packages, then update the consuming package version.

Specialized libraries are not substitutes for BaseLib: use CanvasLib for graph/canvas
workflows, Gantt for schedules, OverlayLib for floating surfaces, and WebGlLib/WebGlRunLib
for typed WebGL behavior.

## Styling

The web host loads the packaged BaseLib stylesheet before this repository's generated
application stylesheet. Build only the application-specific output here:

```powershell
npm install --prefix .\Tailwind
npm run tailwind:build
```

See [Tailwind](../../Tailwind/README.md) for the input and output paths.
