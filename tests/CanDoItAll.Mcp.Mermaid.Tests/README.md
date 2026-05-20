# CanDoItAll.Mcp.Mermaid.Tests

## Purpose

Test project for the Mermaid MCP syntax catalog and tool surface.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet test tests/CanDoItAll.Mcp.Mermaid.Tests/CanDoItAll.Mcp.Mermaid.Tests.csproj
```

## References

Project references:

- `../../src/CanDoItAll.Mcp.Mermaid/CanDoItAll.Mcp.Mermaid.csproj`

Framework references:

- None

Direct package references:

- `coverlet.collector (6.0.4)`
- `Microsoft.NET.Test.Sdk (17.14.1)`
- `xunit (2.9.3)`
- `xunit.runner.visualstudio (3.1.4)`

## Architecture Notes

Keep these tests focused on Mermaid MCP behavior: syntax catalog indexing, forbidden-symbol guidance, example retrieval, and tool calls. Product Mermaid rendering belongs in `CanDoItAll.Components.Mermaid` and should not be duplicated here.

## Related Docs

- Mermaid MCP project: `src/CanDoItAll.Mcp.Mermaid/README.md`
- Repository overview: `README.md` at the repo root
