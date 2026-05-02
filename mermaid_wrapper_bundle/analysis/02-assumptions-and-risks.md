# Assumptions And Risks

## Assumptions

- Official CDN artifact source will be `https://cdn.jsdelivr.net/npm/mermaid@11.14.0/dist/mermaid.esm.min.mjs` unless the download fails and execution records an equivalent official CDN URL.
- `securityLevel: "loose"` is acceptable for sandbox/demo click handling because Mermaid's own click/bind function path needs non-strict behavior; consumers can override config if needed.
- The wrapper can expose both a generic SVG node click callback and future Mermaid `bindFunctions` support without tying C# to Mermaid internals.
- The first implementation does not need to parse Mermaid syntax in .NET; syntax validation happens in Mermaid.js and syntax guidance lives in the MCP catalog.

## Critical Path Risks

- If the static Mermaid asset does not load through Blazor static web assets, every downstream feature fails. This makes the component package a critical foundation.
- If the JS interop lifecycle leaks modules or event handlers, sandbox validation can pass while product use degrades after navigation.
- If node click identification is too flowchart-specific, architecture-beta and newer diagrams may render but not satisfy interactivity.
- If the MCP catalog weakens "forbidden symbols" into generic advice, agents will keep generating invalid Mermaid for graph-specific grammars.

## Validation Risks

- bUnit cannot prove actual Mermaid rendering because Mermaid runs in the browser through JS.
- Playwright proof must inspect rendered SVG content, click a node, exercise zoom/pan, and capture an error state.
- Syntax error location is best-effort because Mermaid error object shapes differ by parser. The component must show any available location and raw details.

## Reopen Triggers

- Reopen subbundle 01 if sandbox or MCP work discovers the wrapper cannot load the packaged Mermaid asset.
- Reopen subbundle 01 if browser proof shows pan/zoom works only for one diagram type.
- Reopen subbundle 02 if the sandbox page does not include architecture-beta, click logging, pan/zoom, and an error example.
- Reopen subbundle 03 if MCP output lacks forbidden symbol guidance for architecture-beta plus common legacy diagram types.
- Reopen the bundle if any raw note closure is only partial without a concrete follow-up subbundle.
