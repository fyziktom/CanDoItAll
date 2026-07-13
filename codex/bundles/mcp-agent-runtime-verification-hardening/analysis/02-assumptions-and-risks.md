# Assumptions And Risks

## Assumptions

- Record the assumptions made during bundle preparation.

## Critical Path Risks

- Identify the subbundles that unlock later work and the regressions that would force rework if they are wrong.

## Validation Risks

- Record where proof may be weak, blocked, environment-dependent, or expensive to reproduce.

## Reopen Triggers

- List the conditions that must reopen an earlier subbundle instead of letting later work continue.
# Assumptions And Risks

## Assumptions

- The app should remain focused on large-screen UI validation for this task.
- The development workspace under the active organization scope is the database the user meant by "development db".
- Managed seed records are allowed to refresh when the seed version changes; non-managed user-edited capability records should not be overwritten.
- The Playwright MCP package can change its tool list over time, so tests should assert framing/configuration and setup success rather than a brittle exact complete tool count.

## Risks

- `@playwright/mcp@latest` may change stdio framing or startup behavior again. The runtime now supports both current newline-delimited JSON and content-length framing, but future protocol changes would need another compatibility pass.
- Existing non-managed capability records with user edits will not be rewritten by seed refresh. That is correct data ownership, but it means a manually forked Playwright MCP record could still be stale.
- The setup UI currently does not expose `messageFraming` as a visible field in the Configuration tab. The value is still present in raw configuration and used by the runtime path.
- Console noise from a stale Blazor circuit can appear if the server is stopped while a browser tab is open. Fresh navigation after restart produced only normal Blazor websocket info messages.
