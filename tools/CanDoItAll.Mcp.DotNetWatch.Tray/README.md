# CanDoItAll.Mcp.DotNetWatch.Tray

## Purpose

Windows tray companion for the DotNetWatch MCP backend.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0-windows`
- Validation command:

```powershell
dotnet build tools/CanDoItAll.Mcp.DotNetWatch.Tray/CanDoItAll.Mcp.DotNetWatch.Tray.csproj
```

## References

Project references:

- None

Framework references:

- None

Direct package references:

- None

## Architecture Notes

This project is an adapter for agent-facing tooling. It must stay thin over canonical app/module services and should not become a second implementation of product behavior.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
