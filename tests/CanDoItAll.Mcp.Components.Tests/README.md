# CanDoItAll.Mcp.Components.Tests

## Purpose

Test project for the corresponding CanDoItAll runtime, module, component, MCP, or integration behavior.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet test tests/CanDoItAll.Mcp.Components.Tests/CanDoItAll.Mcp.Components.Tests.csproj
```

## References

Project references:

- `../../src/CanDoItAll.Mcp.Components/CanDoItAll.Mcp.Components.csproj`

Framework references:

- None

Direct package references:

- `coverlet.collector (6.0.4)`
- `Microsoft.NET.Test.Sdk (17.14.1)`
- `xunit (2.9.3)`
- `xunit.runner.visualstudio (3.1.4)`

## Architecture Notes

This project is an adapter for agent-facing tooling. It must stay thin over canonical app/module services and should not become a second implementation of product behavior.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
