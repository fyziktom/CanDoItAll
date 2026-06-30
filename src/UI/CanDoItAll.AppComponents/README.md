# CanDoItAll.AppComponents

## Purpose

Facade and app-shell component layer for shared shell assets, tab strip behavior, and tuning boundaries.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/UI/CanDoItAll.AppComponents/CanDoItAll.AppComponents.csproj
```

## References

Project references:

- `../CanDoItAll.SharedKernel/CanDoItAll.SharedKernel.csproj`

Framework references:

- None

Direct package references:

- `Microsoft.AspNetCore.Components.Web (10.0.4)`
- `CanDoItAll.Components.BaseLib (0.1.0)`
- `CanDoItAll.Components.CanvasLib (0.1.0)`
- `CanDoItAll.Components.Common (0.1.0)`

## Architecture Notes

Keep shared UI reusable and typed. Use BaseLib for ordinary product UI, CanvasLib for graph/canvas surfaces, OverlayLib for floating windows, WebGlLib for WebGL concepts, and sandbox projects only for demos or proof.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
