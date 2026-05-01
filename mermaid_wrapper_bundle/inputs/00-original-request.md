# Original Request

Captured from the user on 2026-04-30 in `C:\repositories\CanDoItAll`.

```text
Actual available mermaid blazor libraries are not good. It will be beter to maintain own wrapper for mermaid.js
We should use mermaid.js from cdn (or better download as resource in our package, but do not build own, download official from cdn. 
You will have to add new CanDoItAll.Components.Mermaid package and examples in components sandbox as new page. 
I cloned C:\repositories\mermaid so you can see in detail how the actual version of mermaid works. We will need to have not just drawing of graphs, but also react event for click on nodes. It must have pan and zoom. 
If mermaid syntax has trouble it must display proper error info with info about where is error. 
We need also the mermaid MCP server that will capture the main syntax rules and information about how to use advanced graphs like architecture-beta that are new in mermaid. It must contains exmplanation of forbiden symbols based on graph type.

Use [$candoitall-bundle-workflow](C:\\Users\\dell\\.codex\\skills\\candoitall-bundle-workflow\\SKILL.md) to solve this.
```

## Literal Scope Notes

- "own wrapper for mermaid.js" means no dependency on existing Blazor Mermaid libraries.
- "use mermaid.js from cdn ... download official from cdn" means the shipped package must use the official Mermaid distribution file and document the source URL/version, not a local Mermaid build.
- "not just drawing of graphs" means the component must expose interactivity, not only SVG rendering.
- "react event for click on nodes" means a Blazor callback must fire when a rendered node is clicked.
- "must have pan and zoom" is a hard requirement for the rendered diagram viewport.
- "proper error info with info about where is error" requires visible syntax error details, including line/column when Mermaid exposes them.
- "MCP server" means a new agent-facing MCP project, not only static docs in the component package.
