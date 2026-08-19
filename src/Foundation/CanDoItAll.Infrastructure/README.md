# CanDoItAll.Infrastructure

## Purpose

Infrastructure layer for EF Core context access, control-plane database profiles, storage, search, readiness, background queues, and health checks.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/Foundation/CanDoItAll.Infrastructure/CanDoItAll.Infrastructure.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Infrastructure.csproj](CanDoItAll.Infrastructure.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

Infrastructure owns persistence, storage, background runtime primitives, health, readiness, and control-plane concerns. Product rules should remain in modules and shared domain services.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
- Storage and host-path portability: `docs/architecture/storage-and-path-portability.md`
