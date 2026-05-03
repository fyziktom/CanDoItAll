# CanDoItAll.Mcp.Processes

## Purpose

MCP adapter over the canonical Processes module for process templates, runs, steps, artifacts, and automation inspection.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/CanDoItAll.Mcp.Processes/CanDoItAll.Mcp.Processes.csproj
```

## References

Project references:

- `../CanDoItAll.Composition/CanDoItAll.Composition.csproj`
- `../CanDoItAll.Infrastructure/CanDoItAll.Infrastructure.csproj`
- `../CanDoItAll.Mcp.Core/CanDoItAll.Mcp.Core.csproj`
- `../CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj`
- `../CanDoItAll.Migrations.Sqlite/CanDoItAll.Migrations.Sqlite.csproj`
- `../CanDoItAll.Modules.CrmHr/CanDoItAll.Modules.CrmHr.csproj`
- `../CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj`

Framework references:

- None

Direct package references:

- `ModelContextProtocol (1.1.0)`
- `Microsoft.Extensions.Hosting (10.0.1)`
- `Microsoft.Extensions.Logging.Console (10.0.1)`
- `Microsoft.Extensions.Options.ConfigurationExtensions (10.0.5)`
- `Microsoft.Extensions.Options.DataAnnotations (10.0.0)`

## Architecture Notes

This project is an adapter for agent-facing tooling. It must stay thin over canonical app/module services and should not become a second implementation of product behavior.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
