# SB03 focused UI parity

Viewport: 1920 x 1080. Route: `/agents?tab=agents`. Playwright MCP.

- Catalog cards render at approximately 300 x 396 px with the existing `agent-selection-card` hierarchy and hidden overflow.
- Floating catalog compact rows render as a two-column grid at approximately 467 x 76 px, with independent new-chat/history actions.
- 28 Agent cards and 28 floating compact rows were exposed through the existing accessible names and selectors.
- No document-level horizontal overflow was present.
- The live Blazor connection established successfully; no error or warning was emitted after the current navigation. Historical connection-refused messages in the reused browser session predate this host start.
- Screenshot: `proof/SB03/browser/floating-list.png`.

The picker adds one source-neutral template wrapper inside its grid. The existing top-level Agent switch dialog shell, card buttons, selectors, accessible names, filtering, ordering, and visual grid remain stable; bUnit regression coverage proves those interactions.
