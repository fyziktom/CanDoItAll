# CanDoItAll.Infrastructure.Abstractions

## Purpose

Infrastructure-neutral contracts for physical filesystem policy and scoped external-target path resolution.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/Foundation/CanDoItAll.Infrastructure.Abstractions/CanDoItAll.Infrastructure.Abstractions.csproj
```

## Dependencies

The authoritative project dependency list is in [CanDoItAll.Infrastructure.Abstractions.csproj](CanDoItAll.Infrastructure.Abstractions.csproj).

## Architecture Notes

This project owns narrow infrastructure ports and data contracts. Host probing, filesystem access, and physical path binding implementations remain in `CanDoItAll.Infrastructure`.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
