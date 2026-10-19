# CanDoItAll.SharedKernel

## Purpose

Shared low-level domain and application primitives used across modules and infrastructure.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/Foundation/CanDoItAll.SharedKernel/CanDoItAll.SharedKernel.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.SharedKernel.csproj](CanDoItAll.SharedKernel.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

Keep this project aligned with its solution boundary and avoid introducing dependency cycles back into higher-level runtime projects.

The concurrency primitives include policy resolution and pure budget allocation/settlement
arithmetic shared by the engine designs. They do not authorize execution, validate an
engine journal, coordinate generations or own persistence. Each engine keeps those duties
and must atomically persist its balance and allocation before dispatching child work.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
