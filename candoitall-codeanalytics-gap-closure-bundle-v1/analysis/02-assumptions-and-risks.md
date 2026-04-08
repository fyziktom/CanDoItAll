# Assumptions And Risks

## Assumptions

- Project role can be inferred from existing snapshot facts without changing the lower-level project inventory reader.
- Backward-compatible response enrichment is preferable to a breaking inventory API change.
- The alias fix should remain narrow and map only the known historical `Behavior` synonym to `TroublePath`.

## Critical Path Risks

- Subbundle 01 is the critical foundation. If project classification or filtering is wrong, the rerun proof for Scenario 1 becomes misleading and the bundle cannot close honestly.
- Subbundle 02 is smaller, but if alias handling weakens intent normalization or breaks current focused-context flows, the rerun will not be trustworthy.

## Validation Risks

- If the fix changes MCP tool input schema, native in-session validation may require a Codex restart to refresh generated bindings.
- Installed-server reruns can prove behavior even when the current session schema is stale, but that is weaker than native proof for changed inputs.
- Inventory classification heuristics need explicit tests for both product and supporting-project cases or the result will look subjectively better without being locked down.

## Reopen Triggers

- Reopen subbundle 01 if the rerun still requires caller-side name filtering to answer Scenario 1.
- Reopen subbundle 02 if a `Behavior` call still fails generically or if `TroublePath` no longer behaves the same after the alias fix.
- Reopen either implementation subbundle if reinstall or rerun proof exposes response-shape regressions in the existing five-scenario benchmark path.
