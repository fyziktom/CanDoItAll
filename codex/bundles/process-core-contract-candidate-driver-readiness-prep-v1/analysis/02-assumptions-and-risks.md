# Assumptions And Risks

## Assumptions
- The branch builds before this bundle starts.
- The previous bundle proof artifacts are accurate but must not be trusted blindly; Codex must re-run source scans.
- Runtime behavior must remain unchanged.
- UI is out of scope.

## Critical Path Risks
- Removing source payloads too aggressively can break behavior that still needs application-local objects.
- Finalizer and direct-agent execution still need rich data; slimming must be done incrementally.
- Hydration side effects are easy to hide behind "clean" service names; this bundle must make side effects explicit.
- Subprocess projection persistence can silently lose artifact lineage if split carelessly.
- Driver-readiness docs must not accidentally introduce production API names or interfaces.

## Validation Risks
- Focused tests might miss behavior preserved only by integration flows.
- Architecture scans can pass even if semantic behavior changed.
- Full integration test project may be slow; focused integration filters are required but must cover moved paths.

## Reopen Triggers
Reopen earlier subbundles if:
- any route handler regains dispatcher nested model access,
- route source payload unwrapping appears outside the named adapter file,
- subprocess artifact projection loses lineage or expectation matching,
- finalizer paths build different contexts,
- source scans find Process Core or driver API tokens,
- execution report collapses SB rows.
