# Requirement Traceability

## Input Coverage Matrix

| Raw note | Exact wording | Normalized requirements | Owning subbundle | Planned proof | Exception |
| --- | --- | --- | --- | --- | --- |
| N001 | "Actual available mermaid blazor libraries are not good. It will be beter to maintain own wrapper for mermaid.js" | R001, R003 | 01 | Component project/build/tests | None |
| N002 | "We should use mermaid.js from cdn (or better download as resource in our package, but do not build own, download official from cdn." | R002 | 01 | Vendor source metadata and static asset import | None |
| N003 | "add new CanDoItAll.Components.Mermaid package" | R001 | 01 | Solution/project build | None |
| N004 | "examples in components sandbox as new page" | R007 | 02 | Sandbox route and browser screenshot | None |
| N005 | "I cloned C:\repositories\mermaid so you can see in detail how the actual version of mermaid works." | R002, R009, R010 | 01, 03 | Version/source references in docs and MCP tests | None |
| N006 | "not just drawing of graphs, but also react event for click on nodes" | R004 | 01, 02 | Browser node click updates callback log | None |
| N007 | "It must have pan and zoom." | R005 | 01, 02 | Browser zoom/pan state changes | None |
| N008 | "If mermaid syntax has trouble it must display proper error info with info about where is error." | R006 | 01, 02 | Invalid syntax sample displays location/error detail | None |
| N009 | "We need also the mermaid MCP server" | R008 | 03 | New MCP project/settings/tests | None |
| N010 | "capture the main syntax rules and information about how to use advanced graphs like architecture-beta" | R009 | 03 | MCP catalog tests for architecture-beta and advanced diagrams | None |
| N011 | "contains exmplanation of forbiden symbols based on graph type" | R010 | 03 | MCP forbidden-symbol tests per graph type | None |
| N012 | "Use candoitall-bundle-workflow to solve this." | R011 | 04 | Bundle validators, gate rows, execution report | None |

## Requirement To Bundle Files

| Requirement | Bundle files |
| --- | --- |
| R001 | `architecture/01-target-solution.md`, `subbundles/01-01-mermaid-component-package/README.md` |
| R002 | `analysis/01-current-state.md`, `subbundles/01-01-mermaid-component-package/README.md` |
| R003 | `architecture/01-target-solution.md`, `subbundles/01-01-mermaid-component-package/README.md` |
| R004 | `architecture/01-target-solution.md`, `subbundles/01-01-mermaid-component-package/README.md`, `subbundles/02-02-sandbox-examples/README.md` |
| R005 | `architecture/01-target-solution.md`, `subbundles/01-01-mermaid-component-package/README.md`, `subbundles/02-02-sandbox-examples/README.md` |
| R006 | `architecture/01-target-solution.md`, `subbundles/01-01-mermaid-component-package/README.md`, `subbundles/02-02-sandbox-examples/README.md` |
| R007 | `subbundles/02-02-sandbox-examples/README.md` |
| R008 | `subbundles/03-03-mermaid-mcp-server/README.md` |
| R009 | `subbundles/03-03-mermaid-mcp-server/README.md` |
| R010 | `subbundles/03-03-mermaid-mcp-server/README.md` |
| R011 | `subbundles/04-04-validation-and-proof/README.md`, `reviews/01-execution-report.md` |
