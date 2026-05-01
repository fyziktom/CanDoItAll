# Target Solution

## Projects

- `src/CanDoItAll.Components.Mermaid`
  - Razor class library.
  - Owns Blazor component, models, service registration, JS interop module, CSS, and official Mermaid vendor asset.
  - Depends only on ASP.NET Core component packages and local Common/BaseLib only if the implementation needs shared primitives.
- `src/CanDoItAll.Components.Sandbox`
  - Adds project reference to Mermaid package.
  - Adds new Mermaid group/page in `SandboxCatalogRegistry`.
  - Uses BaseLib layout primitives for page structure and `MermaidDiagram` for demos.
- `src/CanDoItAll.Mcp.Mermaid`
  - Stdio MCP server using existing Core hosting patterns.
  - Owns a static syntax catalog based on Mermaid v11.14.0 docs/source.
- `tests/CanDoItAll.Mcp.Mermaid.Tests`
  - Unit tests for catalog and tool envelopes.
- `tests/CanDoItAll.Tests.Components`
  - Targeted component/model tests for Mermaid wrapper types and sandbox registry coverage.

## Component API Shape

- `MermaidDiagram`
  - `Source`
  - `Config`
  - `PanZoomEnabled`
  - `ShowControls`
  - `OnNodeClicked`
  - `OnRendered`
  - `OnError`
  - `Class`, `Style`, unmatched attributes
- `MermaidRenderResult`
  - `DiagramId`
  - `Succeeded`
  - `Error`
- `MermaidNodeClickEventArgs`
  - rendered SVG element id
  - Mermaid node id when detected
  - display text when detected
  - tag/class metadata
- `MermaidRenderError`
  - message
  - line
  - column
  - token/text
  - expected tokens
  - raw detail

## JS Interop Shape

- `wwwroot/js/mermaidDiagram.js` imports `./vendor/mermaid.esm.min.mjs`.
- Render flow:
  1. initialize Mermaid with merged defaults and supplied config
  2. render source into an isolated element id
  3. inject SVG
  4. apply pan/zoom controller when enabled
  5. attach click handlers to rendered SVG node candidates
  6. return structured success or error result
- Error flow:
  1. catch Mermaid parse/render exceptions
  2. normalize `hash.loc`, `hash.text`, `hash.token`, `hash.expected`, and fallback message fields
  3. return structured error for the Razor component to display

## MCP Catalog Shape

- Catalog entries include diagram type, aliases/start keywords, summary, main syntax rules, examples, forbidden symbol rules, advanced notes, source references, and validation tips.
- Tools:
  - `mermaid_diagrams_search`
  - `mermaid_diagram_get`
  - `mermaid_syntax_rules_get`
  - `mermaid_forbidden_symbols_get`
  - `mermaid_examples_get`

## Boundary Decisions

- The wrapper owns Mermaid rendering and interop only; it does not become a diagram authoring parser.
- The MCP server owns syntax guidance only; it does not render diagrams.
- Sandbox proves behavior but does not contain wrapper logic.
