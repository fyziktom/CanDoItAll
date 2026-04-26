# CanDoItAll.Mcp.Core

## Purpose

Shared MCP transport, contracts, options, logging, and tool infrastructure used by CanDoItAll MCP servers.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/CanDoItAll.Mcp.Core/CanDoItAll.Mcp.Core.csproj
```

## References

Project references:

- None

Framework references:

- None

Direct package references:

- `Microsoft.Extensions.Logging.Abstractions (10.0.0)`

## Architecture Notes

This project is an adapter for agent-facing tooling. It must stay thin over canonical app/module services and should not become a second implementation of product behavior.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
