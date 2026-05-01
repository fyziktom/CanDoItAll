# Scope Inventory

## Existing Files To Modify

| Surface | File |
| --- | --- |
| Solution membership | `C:\repositories\CanDoItAll\CanDoItAll.slnx` |
| Sandbox references | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\CanDoItAll.Components.Sandbox.csproj` |
| Sandbox services | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Program.cs` |
| Sandbox imports | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\_Imports.razor` |
| Sandbox catalog | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\SandboxCatalogRegistry.cs` |
| Component tests | `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj` |

## New Files Expected

| Surface | Path |
| --- | --- |
| Mermaid component package | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Mermaid` |
| Mermaid component tests | `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\MermaidWrapperTests.cs` |
| Sandbox Mermaid page | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\Pages\Mermaid.razor` |
| Mermaid MCP server | `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Mermaid` |
| Mermaid MCP tests | `C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Mermaid.Tests` |
| Mermaid MCP settings | `C:\repositories\CanDoItAll\CanDoItAll.Mcp.Mermaid.settings.json` |

## Mermaid Diagram Types For MCP First Pass

- architecture-beta
- flowchart/graph
- sequenceDiagram
- classDiagram
- stateDiagram/stateDiagram-v2
- erDiagram
- block
- xychart-beta
- mindmap
- gitGraph
- pie
- quadrantChart
- packet-beta
- kanban
- timeline
- sankey-beta
- radar-beta
- treemap-beta

## Validation Inventory

- `dotnet build src/CanDoItAll.Components.Mermaid/CanDoItAll.Components.Mermaid.csproj`
- `dotnet build src/CanDoItAll.Components.Sandbox/CanDoItAll.Components.Sandbox.csproj`
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter Mermaid`
- `dotnet test tests/CanDoItAll.Mcp.Mermaid.Tests/CanDoItAll.Mcp.Mermaid.Tests.csproj`
- Playwright route proof for `/groups/mermaid` at large desktop and narrow/mobile widths.
