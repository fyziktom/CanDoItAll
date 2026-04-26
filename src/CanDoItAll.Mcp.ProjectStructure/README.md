# CanDoItAll.Mcp.ProjectStructure

## Purpose

MCP adapter over the web-hosted project-structure API for safe project graph inspection and navigation.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/CanDoItAll.Mcp.ProjectStructure/CanDoItAll.Mcp.ProjectStructure.csproj
```

## References

Project references:

- `../CanDoItAll.Mcp.Core/CanDoItAll.Mcp.Core.csproj`
- `../CanDoItAll.Modules.Workbench/CanDoItAll.Modules.Workbench.csproj`

Framework references:

- None

Direct package references:

- `ModelContextProtocol (1.1.0)`
- `Microsoft.Extensions.Hosting (10.0.0)`
- `Microsoft.Extensions.Logging.Console (10.0.0)`
- `Microsoft.Extensions.Options.ConfigurationExtensions (10.0.4)`
- `Microsoft.Extensions.Options.DataAnnotations (10.0.0)`
- `Microsoft.Extensions.Http (10.0.0)`

## Architecture Notes

This project is an adapter for agent-facing tooling. It must stay thin over canonical app/module services and should not become a second implementation of product behavior.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
