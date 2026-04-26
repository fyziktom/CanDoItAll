# CanDoItAll.Mcp.CodeAnalytics

## Purpose

MCP adapter for source, solution, symbol, and code-analysis inspection over the local CanDoItAll workspace.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/CanDoItAll.Mcp.CodeAnalytics/CanDoItAll.Mcp.CodeAnalytics.csproj
```

## References

Project references:

- `../CanDoItAll.Mcp.Core/CanDoItAll.Mcp.Core.csproj`
- `../../../CanDoItAll.CodeAnalsis/src/CanDoItAll.CodeAnalytics.Abstractions/CanDoItAll.CodeAnalytics.Abstractions.csproj`
- `../../../CanDoItAll.CodeAnalsis/src/CanDoItAll.CodeAnalytics.Analysis/CanDoItAll.CodeAnalytics.Analysis.csproj`
- `../../../CanDoItAll.CodeAnalsis/src/CanDoItAll.CodeAnalytics.Application/CanDoItAll.CodeAnalytics.Application.csproj`
- `../../../CanDoItAll.CodeAnalsis/src/CanDoItAll.CodeAnalytics.Facts/CanDoItAll.CodeAnalytics.Facts.csproj`
- `../../../CanDoItAll.CodeAnalsis/src/CanDoItAll.CodeAnalytics.Rendering/CanDoItAll.CodeAnalytics.Rendering.csproj`
- `../../../CanDoItAll.CodeAnalsis/src/CanDoItAll.CodeAnalytics.Storage/CanDoItAll.CodeAnalytics.Storage.csproj`
- `../../../CanDoItAll.CodeAnalsis/src/CanDoItAll.CodeAnalytics.Workspace/CanDoItAll.CodeAnalytics.Workspace.csproj`

Framework references:

- None

Direct package references:

- `ModelContextProtocol (1.1.0)`
- `Microsoft.Extensions.Hosting (10.0.0)`
- `Microsoft.Extensions.Logging.Console (10.0.0)`
- `Microsoft.Extensions.Options.ConfigurationExtensions (10.0.4)`
- `Microsoft.Extensions.Options.DataAnnotations (10.0.0)`

## Architecture Notes

This project is an adapter for agent-facing tooling. It must stay thin over canonical app/module services and should not become a second implementation of product behavior.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
