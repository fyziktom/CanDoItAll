# CanDoItAll.Space3D.Mouse.Driver

## Purpose

Driver-side contracts and runtime support for Space3D mouse integration.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/Space3D/Mouse/CanDoItAll.Space3D.Mouse.Driver/CanDoItAll.Space3D.Mouse.Driver.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Space3D.Mouse.Driver.csproj](CanDoItAll.Space3D.Mouse.Driver.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

Keep hardware and browser interaction concerns isolated from the main product modules unless the runtime gains a real product dependency on them.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
