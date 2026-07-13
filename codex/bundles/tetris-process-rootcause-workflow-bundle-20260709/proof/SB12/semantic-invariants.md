# SB12 Semantic Invariants

## INV-SB12-001 Real Responsibility Extraction

- Raw note: the adapter must be split into smaller parts with better testability and no partial-class architecture.
- Expected behavior: one thin adapter delegates to cohesive top-level collaborators; process behavior is preserved.
- Disallowed shallow implementation: rename the partial monolith, keep forwarding methods on the adapter, or move all behavior into one replacement manager.
- Failing-first proof: `bundle://proof/SB12/transcripts/failing-first.txt`.
- Passing proof: `bundle://proof/SB12/transcripts/passing-tests.txt`.
- Production assertions: `bundle://proof/SB12/transcripts/source-assertions.txt`.
- Red-team negative case: architecture test rejects adapter partials and replacement-monolith growth.
- Downstream dependency: satisfied; SB13 was allowed to proceed after this invariant passed.

## Closure Result

- Result: `Passed`.
- Adapter declaration count: one class plus its narrow executor interface in one file.
- Adapter member count in refreshed CodeAnalytics: 5.
- Executor member count: 12; completion coordinator member count: 4; result converter member count: 4.
- The architecture baseline rejects partial declarations, global static imports, replacement-monolith growth, and domain-token leakage.

## Production Behavior Artifact Matrix

No new production signal, state, record, or event is introduced.
