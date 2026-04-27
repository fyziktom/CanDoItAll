# CanDoItAll.Mcp.DotNetWatch.IntegrationTests

## Purpose

MCP adapter for DotNetWatch.IntegrationTests capabilities.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet test tests/CanDoItAll.Mcp.DotNetWatch.IntegrationTests/CanDoItAll.Mcp.DotNetWatch.IntegrationTests.csproj
```

## References

Project references:

- `../../src/CanDoItAll.Mcp.DotNetWatch/CanDoItAll.Mcp.DotNetWatch.csproj`

Framework references:

- None

Direct package references:

- `coverlet.collector (6.0.4)`
- `Microsoft.NET.Test.Sdk (17.14.1)`
- `ModelContextProtocol (1.1.0)`
- `xunit (2.9.3)`
- `xunit.runner.visualstudio (3.1.4)`

## Architecture Notes

This project is an adapter for agent-facing tooling. It must stay thin over canonical app/module services and should not become a second implementation of product behavior.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
