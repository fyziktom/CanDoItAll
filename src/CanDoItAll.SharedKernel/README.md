# CanDoItAll.SharedKernel

## Purpose

Shared low-level domain and application primitives used across modules and infrastructure.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/CanDoItAll.SharedKernel/CanDoItAll.SharedKernel.csproj
```

## References

Project references:

- None

Framework references:

- None

Direct package references:

- None

## Architecture Notes

Keep this project aligned with its solution boundary and avoid introducing dependency cycles back into higher-level runtime projects.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
