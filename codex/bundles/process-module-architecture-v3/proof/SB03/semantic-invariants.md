# SB03 Semantic Invariants

- Generic core projects contain no EF, Razor, UI, infrastructure, concrete driver, Git implementation, or AgentFramework runtime references.
- Capability tags, driver IDs, strategy IDs, branch family IDs, and branch outcome IDs are opaque value objects.
- Display labels are not used for route semantics.
- Backward graph routes require loop budgets.
- Forward graph routes must remain acyclic.
- Runtime event envelopes require explicit schema version, correlation, actor, sensitivity, UTC timestamp, event type, and payload hash.
- Terminal run and step states cannot transition back to active states.
- Core validation returns explicit failures instead of silent fallback behavior.
