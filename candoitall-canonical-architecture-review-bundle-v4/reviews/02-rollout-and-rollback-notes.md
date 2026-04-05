# Rollout And Rollback Notes

## Rollout

- Land the failure-handling and typed-bridge changes before touching metadata cleanup so the boundary contract is stable first.
- No schema migration was required in this wave, so rollout stays at the application and test-proof level.
- Direct Playwright MCP validation is still blocked by the machine-level `EPERM` mkdir failure, so the rollout proof path currently depends on the Playwright browser-test runner and captured screenshots.

## Rollback

- If lifecycle compensation or typed node-reference changes regress the repaired path, revert the boundary changes together.
- Do not keep a half-state where metadata cleanup lands on top of a weak lifecycle seam.
- If projection-only metadata changes regress UI summaries, rollback the metadata contract and structure-page save flow together rather than restoring only one side.
- If a rollback reintroduces metadata-backed party identity or bypasses the bridge for node-scoped ownership, rerun the canonical-model review before considering the rollback safe.
