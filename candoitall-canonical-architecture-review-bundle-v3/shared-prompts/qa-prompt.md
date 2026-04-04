# QA Prompt

Validate only the current bundle scope.

- Re-run the targeted build and test slices that cover the repaired canonical seam.
- For browser-visible behavior, attempt Playwright MCP first and capture screenshots.
- If MCP is blocked, record the exact blocker and use the narrowest honest fallback.
- Do not close the bundle while a critical canonical split-source-of-truth issue remains unresolved.
