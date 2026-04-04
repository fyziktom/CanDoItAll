# Target Solution

## Target Boundary

- Use Playwright MCP as the primary validation driver.
- Use a local app instance as the live test target.
- Use scoped repair edits only if the regression sweep proves a failure.
- Keep browser proof and any repair proof tied to the exact subbundle that executed the interaction.

## Explicit Non-Goals

- No speculative canvas redesign without a reproduced failure.
- No replacement of MCP proof with test-runner-only proof unless a blocker is explicitly documented.
