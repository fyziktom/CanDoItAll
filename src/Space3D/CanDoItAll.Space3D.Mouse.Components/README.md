# CanDoItAll.Space3D.Mouse.Components

## Purpose

Razor components for the Space3D mouse integration surface.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/Space3D/CanDoItAll.Space3D.Mouse.Components/CanDoItAll.Space3D.Mouse.Components.csproj
```

## References

Project references:

- `../CanDoItAll.Space3D.Mouse.Driver/CanDoItAll.Space3D.Mouse.Driver.csproj`
- `../../CanDoItAll.Components.BaseLib/CanDoItAll.Components.BaseLib.csproj`
- `../../CanDoItAll.Components.WebGlLib/CanDoItAll.Components.WebGlLib.csproj`

Framework references:

- None

Direct package references:

- None

## Architecture Notes

Keep hardware and browser interaction concerns isolated from the main product modules unless the runtime gains a real product dependency on them.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
