# CanDoItAll.Migrations.PostgreSql

## Purpose

PostgreSQL EF Core migrations for the CanDoItAll application model.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/Foundation/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Migrations.PostgreSql.csproj](CanDoItAll.Migrations.PostgreSql.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

This project owns provider-specific EF Core migration assets only. Runtime behavior belongs in Infrastructure or the owning product module.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
