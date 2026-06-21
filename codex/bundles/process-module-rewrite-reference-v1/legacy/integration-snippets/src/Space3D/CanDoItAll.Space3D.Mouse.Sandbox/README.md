# CanDoItAll.Space3D.Mouse.Sandbox

## Purpose

Sandbox host for validating Space3D mouse components and driver behavior.

## Project Type

- SDK: `Microsoft.NET.Sdk.Web`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/Space3D/CanDoItAll.Space3D.Mouse.Sandbox/CanDoItAll.Space3D.Mouse.Sandbox.csproj
```

## References

Project references:

- `../CanDoItAll.Space3D.Mouse.Components/CanDoItAll.Space3D.Mouse.Components.csproj`
- `../../CanDoItAll.Components.BaseLib/CanDoItAll.Components.BaseLib.csproj`
- `../../CanDoItAll.Components.WebGlLib/CanDoItAll.Components.WebGlLib.csproj`
- `../../CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj`

Framework references:

- None

Direct package references:

- None

## Architecture Notes

Keep hardware and browser interaction concerns isolated from the main product modules unless the runtime gains a real product dependency on them.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
