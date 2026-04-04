# Structured Input

## Objectives

- Preserve the productive Grid/Row/Column experiment without leaving comparison-only scaffolding in Zyphonote production UI.
- Give the components sandbox a dedicated, reusable layout example page for Grid, Row, Column, and Stack composition.
- Teach the component MCP how layout composition should work and where real component usage examples already exist in CanDoItAll.
- Make the component MCP part of the standard CanDoItAll MCP reinstall flow and prove that installation works locally.
- Add repo-managed Codex guidance so future agents prefer shared components over ad-hoc layout wrappers and avoid unnecessary custom styles.

## Hard Constraints

- Keep working in the Zyphonote repo only for Zyphonote-specific cleanup; new shared guidance belongs in CanDoItAll.
- Keep custom styles to the minimum needed for visual identity; structural layout should come from shared components first.
- Use the shared CanDoItAll MCP installation path instead of a one-off local setup.
- The sandbox proof must remain browser-visible and reusable by the component MCP server.

## Assumptions

- The existing BaseLib `Grid`, `Row`, and `Column` fixes are acceptable to keep and should now be documented and demonstrated rather than rolled back.
- `CanDoItAll.Mcp.Components` is intended to be the discoverability surface for shared component usage and guidance, not only a symbol list.
- A repo-managed skill plus a local Codex plugin is an appropriate delivery for the “use this MCP when CanDoItAll components are involved” guidance.

## Risks

- The sandbox registry currently models groups as one page each, so a dedicated layout example page must either fit the existing catalog model or extend it carefully.
- The MCP install script currently does not include the components server, so adding it touches user config, VS Code config, and the install manifest.
- The component MCP may need a richer catalog model to surface real app examples, not just sandbox examples.

## Validation Expectations

- Zyphonote `/progress` still renders correctly with only the responsive Row/Column version retained.
- The sandbox exposes a dedicated layout example page and it reads clearly on desktop and narrow widths.
- The component MCP exposes the new layout guidance and real consumer examples in tool responses.
- The reinstall path publishes and configures the component MCP server locally without breaking existing installed MCPs.
