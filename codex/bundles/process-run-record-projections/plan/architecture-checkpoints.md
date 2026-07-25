# Architecture Checkpoints

| Checkpoint | Timing | Gate |
| --- | --- | --- |
| A0 current-state/canonical-source review | Before SB02 | Boundaries, invariants, and performance evidence are documented. |
| A1 contract/schema review | After SB02 | No runtime-upward dependency, no ORM relationships, indexed scalar query fields, schema/completeness versioning, tests green. |
| A2 finalization review | After SB03 | Deterministic hard facts precede async narrative; idempotency, terminal-event classification, privacy, and failure semantics proven. |
| A3 consumer/read-path review | After SB04 | Normal history consumers do not hydrate canonical detail per row; APIs are bounded and typed. |
| A4 documentation parity | After SB05 | SharedInfo skill matches compiled/tested routes. |
| A5 final architecture gate | SB06 | Dependency direction, modularity, performance evidence, regression tests, migration, and build all pass. |

Any failed checkpoint reopens the owning subbundle and all downstream consumers.
