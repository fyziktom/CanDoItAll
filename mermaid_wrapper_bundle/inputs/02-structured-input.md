# Structured Input

## Objectives

- Add a first-party Mermaid Blazor wrapper package named `CanDoItAll.Components.Mermaid`.
- Ship official Mermaid.js as a static package resource downloaded from the CDN, with the source version/URL documented.
- Render Mermaid diagrams through JS interop without depending on existing third-party Blazor Mermaid wrappers.
- Expose a node click callback to Blazor consumers.
- Provide pan and zoom behavior for rendered SVG diagrams.
- Display readable syntax/render errors with line, column, offending token/text, and expected tokens when Mermaid exposes them.
- Add sandbox examples as a new page/group in `CanDoItAll.Components.Sandbox`.
- Add a new `CanDoItAll.Mcp.Mermaid` server that captures syntax rules, advanced diagram guidance, and forbidden symbol guidance by graph type.

## Hard Constraints

- Do not build or modify Mermaid.js from source.
- Use Mermaid v11.14.0 from `C:\repositories\mermaid\packages\mermaid\package.json` unless execution discovers a stronger repo standard.
- The wrapper must work for `architecture-beta`, not only legacy flowcharts.
- Browser proof is required because rendering, pan/zoom, and click behavior are browser-visible.
- New MCP server must follow existing stdio host and `McpToolEnvelope<T>` patterns.

## Assumptions

- A downloaded official ESM distribution file under the component package's `wwwroot` satisfies the user's "better download as resource" preference.
- Generic SVG node click capture is acceptable when a diagram type does not support Mermaid's `click` directive syntax.
- A lightweight in-package pan/zoom controller is acceptable because the user only prohibited building Mermaid itself.

## Primary Risks

- Mermaid syntax error objects vary by parser and diagram type, so the wrapper must normalize best-effort locations without dropping raw details.
- Mermaid SVG structure varies by diagram type, so node click extraction must be tolerant and tested with at least flowchart and architecture-beta samples.
- Static web assets and JS module paths must work from both sandbox and downstream packages.
