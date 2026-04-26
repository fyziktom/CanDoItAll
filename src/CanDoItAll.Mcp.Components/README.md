# CanDoItAll.Mcp.Components

## Purpose

MCP adapter for discovering and inspecting shared component-library capabilities.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/CanDoItAll.Mcp.Components/CanDoItAll.Mcp.Components.csproj
```

## References

Project references:

- `../CanDoItAll.Components.BaseLib/CanDoItAll.Components.BaseLib.csproj`
- `../CanDoItAll.Components.CanvasLib/CanDoItAll.Components.CanvasLib.csproj`
- `../CanDoItAll.Components.Common/CanDoItAll.Components.Common.csproj`
- `../CanDoItAll.Components.Sandbox/CanDoItAll.Components.Sandbox.csproj`
- `../CanDoItAll.Mcp.Core/CanDoItAll.Mcp.Core.csproj`

Framework references:

- `Microsoft.AspNetCore.App`

Direct package references:

- `ModelContextProtocol (1.1.0)`

## Architecture Notes

This project is an adapter for agent-facing tooling. It must stay thin over canonical app/module services and should not become a second implementation of product behavior.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
