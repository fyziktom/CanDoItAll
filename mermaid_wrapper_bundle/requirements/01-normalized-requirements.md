# Normalized Requirements

| ID | Requirement | Acceptance signal | Owner |
| --- | --- | --- | --- |
| R001 | Add a first-party Razor class library package `CanDoItAll.Components.Mermaid`. | Project exists, is in `CanDoItAll.slnx`, builds, and exposes public wrapper types. | Subbundle 01 |
| R002 | Use official Mermaid.js v11.14.0 from CDN as a package static web asset, not a locally built Mermaid bundle. | Downloaded vendor file and source metadata exist under the package; JS imports that asset. | Subbundle 01 |
| R003 | Render Mermaid source through a Blazor component API. | Component accepts diagram source/config and renders nonblank SVG in browser proof. | Subbundle 01 |
| R004 | Raise a Blazor event when users click rendered nodes. | Browser proof clicks a node and sandbox logs callback details. | Subbundles 01, 02 |
| R005 | Provide pan and zoom. | Browser proof exercises zoom buttons/wheel or drag pan and observes viewBox/state changes. | Subbundles 01, 02 |
| R006 | Display proper syntax/render error details with line/column when available. | Invalid sample shows a visible error panel with message and location/excerpt metadata. | Subbundles 01, 02 |
| R007 | Add component sandbox examples as a new page/group. | Sandbox navigation includes Mermaid, with working architecture-beta, flowchart, click, zoom/pan, and error examples. | Subbundle 02 |
| R008 | Add a `CanDoItAll.Mcp.Mermaid` server. | New project/settings/test project exist and build/test. | Subbundle 03 |
| R009 | MCP syntax catalog captures main Mermaid syntax rules and advanced graph guidance, including architecture-beta. | Tools return architecture-beta syntax, examples, config notes, and forbidden symbols. | Subbundle 03 |
| R010 | MCP forbidden symbol guidance is graph-type-specific. | Tests verify at least architecture-beta, flowchart, sequence, class, state, ER, block, and xychart guidance. | Subbundle 03 |
| R011 | Validate code and browser behavior end to end. | Targeted build/test commands pass and Playwright analytics are recorded. | Subbundle 04 |

## Out Of Scope

- Building Mermaid.js from source.
- Replacing existing chart/canvas components.
- Implementing a .NET Mermaid parser.
- Covering every Mermaid diagram type with a full grammar reference in the first pass; the catalog must cover main syntax and known high-risk symbols for common and advanced diagrams.
