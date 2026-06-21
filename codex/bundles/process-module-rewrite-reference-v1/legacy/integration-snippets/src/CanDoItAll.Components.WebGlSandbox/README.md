# CanDoItAll.Components.WebGlSandbox

## Purpose

Blazor sandbox for WebGL workbench prototypes and browser proof.

## Project Type

- SDK: `Microsoft.NET.Sdk.Web`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/CanDoItAll.Components.WebGlSandbox/CanDoItAll.Components.WebGlSandbox.csproj
```

## References

Project references:

- `../CanDoItAll.Components.BaseLib/CanDoItAll.Components.BaseLib.csproj`
- `../CanDoItAll.Components.OverlayLib/CanDoItAll.Components.OverlayLib.csproj`
- `../CanDoItAll.Components.WebGlLib/CanDoItAll.Components.WebGlLib.csproj`
- `../CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj`

Framework references:

- None

Direct package references:

- None

## Architecture Notes

Keep shared UI reusable and typed. Use BaseLib for ordinary product UI, CanvasLib for graph/canvas surfaces, OverlayLib for floating windows, WebGlLib for WebGL concepts, and sandbox projects only for demos or proof.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
