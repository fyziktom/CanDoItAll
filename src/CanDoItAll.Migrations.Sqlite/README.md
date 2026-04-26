# CanDoItAll.Migrations.Sqlite

## Purpose

SQLite EF Core migrations for the CanDoItAll application model.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/CanDoItAll.Migrations.Sqlite/CanDoItAll.Migrations.Sqlite.csproj
```

## References

Project references:

- `../CanDoItAll.Composition/CanDoItAll.Composition.csproj`
- `../CanDoItAll.Infrastructure/CanDoItAll.Infrastructure.csproj`
- `../CanDoItAll.Modules.CrmHr/CanDoItAll.Modules.CrmHr.csproj`

Framework references:

- None

Direct package references:

- `Microsoft.EntityFrameworkCore.Design (10.0.4)`

## Architecture Notes

This project owns provider-specific EF Core migration assets only. Runtime behavior belongs in Infrastructure or the owning product module.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
