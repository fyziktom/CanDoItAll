# Current State

## Component Library

- `CanDoItAll.Components.Sandbox` is a Blazor web sandbox that already references BaseLib, CanvasLib, Charts, and Common.
- Sandbox navigation is driven by `SandboxCatalogRegistry.Groups` and `SandboxCatalogRegistry.Examples`.
- Sandbox pages use `CatalogPageFrame` and BaseLib layout primitives such as `PageScaffold`, `Grid`, `Stack`, `SectionCard`, `SummaryTiles`, `Tabs`, `TextArea`, `Button`, and `Alert`.
- `CanDoItAll.Components.Charts` is the closest wrapper precedent: it hides a third-party rendering library behind CanDoItAll models and exposes a single component API.

## MCP Server Pattern

- `CanDoItAll.Mcp.Components` is a thin stdio MCP server using `Host.CreateEmptyApplicationBuilder`, `AddCanDoItAllMcpSettings`, `ConfigureCanDoItAllMcpStdioLogging`, `AddValidatedCanDoItAllMcpOptions`, `McpToolEnvelope<T>`, and `ModelContextProtocol`.
- MCP tests directly instantiate catalog services and tool classes rather than launching the stdio server.
- Root settings files such as `CanDoItAll.Mcp.Components.settings.json` live at the workspace root.

## Mermaid Version And Syntax

- The local Mermaid clone reports package version `11.14.0` at `C:\repositories\mermaid\packages\mermaid\package.json`.
- `architecture-beta` is present and documented at `C:\repositories\mermaid\docs\syntax\architecture.md`.
- Architecture diagrams are parsed through `@mermaid-js/parser` and have Langium grammar files under `C:\repositories\mermaid\packages\parser\src\language\architecture`.
- Architecture grammar uses:
  - start keyword `architecture-beta`
  - group/service/junction declarations
  - edge syntax with `L`, `R`, `T`, `B` side ports
  - IDs matching `[\w]([-\w]*\w)?`
  - titles in `[]`, supporting unquoted words/spaces or quoted strings
  - icons in `()` matching `[\w-:]+`

## Existing Gaps

- No `CanDoItAll.Components.Mermaid` package exists.
- No sandbox page covers Mermaid rendering or syntax errors.
- No existing component exposes a rendered SVG node click callback from Mermaid.
- No Mermaid MCP server exists.
