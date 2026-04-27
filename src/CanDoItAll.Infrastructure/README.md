# CanDoItAll.Infrastructure

## Purpose

Infrastructure layer for EF Core context access, control-plane database profiles, storage, search, readiness, background queues, and health checks.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/CanDoItAll.Infrastructure/CanDoItAll.Infrastructure.csproj
```

## References

Project references:

- `../CanDoItAll.SharedKernel/CanDoItAll.SharedKernel.csproj`

Framework references:

- None

Direct package references:

- `Microsoft.AspNetCore.DataProtection (10.0.6)`
- `Microsoft.EntityFrameworkCore (10.0.4)`
- `Microsoft.EntityFrameworkCore.Design (10.0.4)`
- `Microsoft.EntityFrameworkCore.InMemory (10.0.4)`
- `Microsoft.EntityFrameworkCore.Sqlite (10.0.4)`
- `Microsoft.Extensions.Diagnostics.HealthChecks (10.0.0)`
- `Microsoft.Extensions.Options.ConfigurationExtensions (10.0.0)`
- `Microsoft.Extensions.Options.DataAnnotations (10.0.0)`
- `Npgsql.EntityFrameworkCore.PostgreSQL (10.0.0)`

## Architecture Notes

Infrastructure owns persistence, storage, background runtime primitives, health, readiness, and control-plane concerns. Product rules should remain in modules and shared domain services.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
