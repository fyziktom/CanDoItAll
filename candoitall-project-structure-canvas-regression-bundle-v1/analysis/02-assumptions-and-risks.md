# Assumptions And Risks

## Working Assumptions

- The elevated session is sufficient for Playwright MCP to create its runtime directory and drive the browser.
- A local development app instance can be launched safely for MCP-driven regression work.
- Temporary project data created during testing is acceptable if it is clearly named and bounded to this bundle.

## Critical Path Risks

- The canvas interaction surface is broad enough that ad-hoc testing can miss regressions unless the flow is decomposed into explicit subbundles.
- Existing helper tests may cover some behaviors differently than the live MCP session, so manual MCP proof still needs to stand on its own.

## Validation Risks

- Right-click and canvas-menu flows can regress visually even when the underlying action still succeeds.
- Overlay or floating-window proof is weak unless the open state is captured and inspected.

## Reopen Triggers

- If Playwright MCP still fails in the elevated session, the bundle must reopen as an environment-blocked validation bundle rather than pretending coverage happened.
- If any canvas interaction fails, a repair scope must be added or executed before closure.
