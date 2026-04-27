# CanDoItAll.Space3D.Mouse.Driver

## Purpose

Driver-side contracts and runtime support for Space3D mouse integration.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/Space3D/CanDoItAll.Space3D.Mouse.Driver/CanDoItAll.Space3D.Mouse.Driver.csproj
```

## References

Project references:

- None

Framework references:

- None

Direct package references:

- None

## Architecture Notes

Keep hardware and browser interaction concerns isolated from the main product modules unless the runtime gains a real product dependency on them.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
