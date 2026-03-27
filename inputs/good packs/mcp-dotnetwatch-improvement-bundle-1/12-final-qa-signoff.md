# 12. Final QA Signoff

Role:

- Senior C# and MCP QA inspector

## Final review result

Approved for implementation planning handoff.
This signoff includes the round-2 follow-up requirements around Codex workflow steering and response-efficiency control.

## Why this bundle is now sufficient

1. It is anchored to the current verified state, not stale historical failures.
2. It preserves the detached backend and wrapper strengths already present in the repo.
3. It directly addresses the current real gap:
   the bridge is weaker than the backend/runtime core.
4. It adds the missing runtime lane required for atomic Codex-safe updates.
5. It does not over-promise zero-downtime networking semantics that the current local architecture does not support.
6. It includes rollback, compatibility, validation thresholds, and risk controls.
7. It now treats Codex workflow steering as an architectural concern instead of assuming the agent will infer the correct iteration discipline by itself.
8. It constrains that steering with explicit emitter rules and a payload budget so context quality is preserved.

## Final approval conditions

Implementation is approved only if the team follows the bundle order and does not skip:

1. bridge repair and typed failures
2. workflow steering contract and response-budget controls
3. launch-model refactor
4. resource-scoped coordination
5. slot-based atomic runtime orchestration
6. validation evidence against the strict gates

## Explicit anti-patterns to reject during implementation

- adding more ad hoc booleans to `AppStartTemplate` instead of introducing a real launch model
- keeping one global workspace lock and renaming it as if it became more capable
- treating publish to one hot folder as "atomic"
- adding automatic retry without idempotency safeguards
- hiding candidate/commit/rollback state in logs instead of structured status
- breaking current watch flows to make the new atomic lane pass
- dumping verbose reminder text into every response instead of emitting compact state-derived guidance on the selected tools only
- using workflow hints as a substitute for fixing bridge reliability

## Final QA statement

The bundle is complete enough, constrained enough, and testable enough for a follow-on implementation agent.
No further planning expansion is required before implementation begins.
