# Codex review checklist

## Universal boundary

- Confirm the new library lives in a dedicated project such as `src/CanDoItAll.Components.WebGlLib`.
- Confirm the universal library does **not** reference `CanDoItAll.Modules.Processes`.
- Confirm typed contracts exist for scene, nodes, edges, ports, camera state, diagnostics, and semantic interaction DTOs.
- Confirm the JS runtime owns rendering, hit-testing, drag preview, and connection preview.

## Sandbox shape

- Confirm the new sandbox route or project is isolated from the production Processes route.
- Confirm representative template switching exists for simple, medium, and dense process packs.
- Confirm default camera, fit-view, reset, and selection affordances exist.

## Interaction semantics

- Confirm the sandbox supports move node, connect/disconnect, selection, reset, and scene reload.
- Confirm edits are explicitly in-memory unless a later phase changes that rule.
- Confirm process-template projection lives outside the universal library.

## Proof expectations

- Confirm screenshot proof exists for the simple, medium, and dense templates.
- Confirm semantic runtime helpers exist for scene snapshot, diagnostics, image export, and simulated interaction.
- Confirm Playwright proof uses the semantic bridge rather than raw pointer-only canvas automation.

## Gate expectations

- Gate A answers whether the universal boundary, runtime ownership, DOM mirror, and guided perspective default remain valid.
- Gate B answers whether readability and interaction semantics justify final automation hardening.
- Final closure answers whether the concept is worth a future pilot or should remain a sandbox-only spike.
