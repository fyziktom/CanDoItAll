# Implementation Prompt

You are executing the Mermaid wrapper bundle in `C:\repositories\CanDoItAll`.

Follow `plan/01-phase-plan.md` and execute only the current subbundle. Before editing, read the root README, raw input, traceability rows, and the selected subbundle README. Respect the dependency gates.

Use existing repo patterns:

- Razor component package patterns from `CanDoItAll.Components.Charts`.
- Sandbox layout patterns from `CanDoItAll.Components.Sandbox`.
- MCP hosting/tool-envelope patterns from `CanDoItAll.Mcp.Components`.
- BaseLib layout components for sandbox structure.

Do not build Mermaid.js from source. Download the official Mermaid v11.14.0 distribution from CDN into the component package and record the URL/version. Keep wrapper code separate from Mermaid vendor code.

After each subbundle, update `reviews/01-execution-report.md`, the subbundle status, and raw note closure rows. If browser proof is required, record route, viewport, Playwright actions, screenshot paths, and result while the proof is fresh.
