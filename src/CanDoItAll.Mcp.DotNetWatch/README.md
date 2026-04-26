# CanDoItAll.Mcp.DotNetWatch

## Purpose

MCP adapter and backend for supervising dotnet watch, app readiness, browser sessions, and development capsules.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/CanDoItAll.Mcp.DotNetWatch/CanDoItAll.Mcp.DotNetWatch.csproj
```

## References

Project references:

- `..//CanDoItAll.Mcp.Core//CanDoItAll.Mcp.Core.csproj`
- `..//CanDoItAll.Mcp.LocalRuntime//CanDoItAll.Mcp.LocalRuntime.csproj`

Framework references:

- `Microsoft.AspNetCore.App`

Direct package references:

- `Microsoft.Extensions.Hosting (10.0.0)`
- `Microsoft.Extensions.Http (10.0.0)`
- `ModelContextProtocol (1.1.0)`
- `System.Management (10.0.0)`

## Architecture Notes

This project is an adapter for agent-facing tooling. It must stay thin over canonical app/module services and should not become a second implementation of product behavior.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
