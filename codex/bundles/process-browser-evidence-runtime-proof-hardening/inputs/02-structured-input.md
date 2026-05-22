# Structured Input

## Objectives

- Make browser/runtime QA proof artifact-backed and process-visible.
- Prevent release or quality acceptance when required browser screenshots, console logs, snapshots, or equivalent browser evidence are only mentioned in markdown or `resultSummary.evidenceRefs`.
- Make interactive UI validation semantically meaningful enough to catch a game/canvas/custom-control surface where visible pieces or user interactions are missing.
- Classify console diagnostics by proof phase so real JavaScript errors block acceptance and intentional post-stop host disconnects are recorded separately.
- Preserve generic process runtime boundaries; project-specific acceptance facts belong outside process core.

## Hard Constraints

- Do not hardcode `TetrisGame`, Tetris rules, Blazor-specific gameplay, or canvas-specific product semantics into process core.
- Do not introduce silent fallback behavior that hides missing artifacts. Missing required evidence must become a blocker, repair outcome, or conformance observation.
- Use strongly typed contracts or existing domain types for new proof classifications, artifact kinds, and validation outcomes.
- Keep edits minimal and layered: AgentFramework provider-native MCP capture, Processes artifact projection/validation, process definitions/templates, then tests.
- Browser proof must remain bounded. Do not require full-page screenshots or unbounded snapshots unless a process step explicitly asks for them.

## Assumptions

- The provider-native browser MCP can still write default `.playwright-mcp` outputs even when a requested filename is ignored or not mirrored.
- Execution logs are durable enough to recover browser tool invocation names and requested filenames when chat history is empty, but implementation must verify this before relying on it.
- Console errors after the app host has intentionally been stopped should be classified as shutdown noise, not as the same severity as errors during active validation.
- Project structure for UI work can provide generic acceptance hints such as "representative gameplay interaction must make the active piece visible or prove canvas pixels changed"; process runtime should only enforce that such hints are tested and recorded.

## Open Questions

- Which production entity should own a typed browser proof summary: execution run detail, process artifact projection, step outcome context, or a new process evidence model? Execution should decide after reading existing persistence boundaries.
- Should `.playwright-mcp` files be copied into the scoped artifact tree at ingestion time, or should provider-native MCP wrappers force exact requested filenames? The bundle allows either if process artifact records are durable and validation can fail when they are missing.
