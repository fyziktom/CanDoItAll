# Assumptions And Risks

## Assumptions

- The user is reporting interactive web-app latency, not a migration failure or database corruption.
- Existing background warmup/seeding services may remain available; this bundle only removes page-init eager work.
- Page layout is not intentionally changing, so browser proof can focus on host startup and route viability unless a UI layout edit becomes necessary.

## Critical Path Risks

- If lazy loading omits data required by an initially visible command, the page may render quickly but fail on the first interaction. Tests must cover tab changes and dialog-open paths.
- If project-structure local create updates miss hierarchy links or follow-up moves, the canvas may diverge from persisted state until the next reload. The mutation test must assert visible node placement and limited DbContext usage.
- If workflows page component/provider data is deferred without a load gate, editor/template tabs may render with empty option lists. The tab-change handler must load the library before those sections render.
- EF logging filters must be precise enough to suppress EF console noise without silencing unrelated application logs.

## Validation Risks

- Component tests may validate service calls but not browser-perceived timing. The implementation must combine call-count tests with web-host startup proof.
- Some performance improvements are negative proofs: proving calls no longer happen on initial load. Tests should use existing fake/counting services where possible.
- Existing PostgreSQL configuration may require local secrets. Startup validation should use the repo's current local configuration and record failures explicitly if the environment is missing.

## Reopen Triggers

- Initial Processes navigation still invokes analytics/runtime/template-option APIs for hidden tabs.
- Workflows page initialization still calls example catalog seeding or component-library listing.
- Project-structure create still performs full surface reload before showing the created node.
- EF command logs continue to appear in console with default configuration.
- Any targeted test or web startup check fails after implementation.
