# Phase Plan

## Execution Order

1. Reduce Zyphonote `Progress` to the single responsive Row/Column version and preserve the proof path.
2. Build the dedicated sandbox layout example page and catalog wiring in CanDoItAll.
3. Expand `CanDoItAll.Mcp.Components` with layout guidance and real consumer examples.
4. Add the components MCP to the reinstall path, then add the skill/plugin/docs guidance and prove installation locally.
5. Run browser proof, tool proof, install proof, and final closure audit.

## Subbundle Dependency Map

```mermaid
gantt
title Components layout MCP bundle dependency map
dateFormat  YYYY-MM-DD
section Foundations
01 Zyphonote cleanup and responsive preservation :done, s1, 2026-04-04, 1d
02 Sandbox layout example page :after s1, s2, 1d
section MCP enablement
03 Components MCP layout knowledge :after s2, s3, 1d
04 Installer, skill, plugin, install proof :after s3, s4, 1d
```

- `02` depends on the now-understood BaseLib layout semantics and becomes the proof surface for `03`.
- `03` depends on `02` because the sandbox page is one of the primary example sources for the component MCP.
- `04` depends on `03` because the install and guidance work must point to the final MCP capabilities.

## Critical Subbundles

- `02-sandbox-layout-example-page-and-registry-updates`
  - Critical UI foundation. If the sandbox example is wrong, the MCP will teach the wrong layout pattern.
- `03-candoitall-mcp-components-layout-knowledge-and-component-guidance`
  - Critical process foundation. Later install and skill guidance depends on the final tool surface.

## Phase Gates

- Gate after preparation: run `validate_bundle.py --stage prepared` and repair bundle structure issues.
- Gate after subbundle 1: Zyphonote `/progress` proves that only the responsive version remains and still renders correctly.
- Gate after subbundle 2: the sandbox page renders in browser proof on large and narrow widths and is registered in the catalog.
- Gate after subbundle 3: the component MCP can return the new layout guidance and app example data.
- Gate after subbundle 4: reinstall script publishes and wires the component MCP locally, and the new skill/plugin guidance is installed or installable from repo scripts.
- Gate before closure: run `validate_bundle.py --stage completed`, close the raw notes, and reopen any phase with weak proof.
