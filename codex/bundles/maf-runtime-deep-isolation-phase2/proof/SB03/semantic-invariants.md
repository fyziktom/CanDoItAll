# SB03 Semantic Invariants

## Invariants

- Capability composition ordering and access filtering must match existing behavior.
- Metrics must represent real production composition stages, not test-only counters.

## Shallow-Pass Trap

- A composer that returns a non-empty tool list without applying access policies would falsely pass simple count tests.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative Proof |
| --- | --- | --- | --- | --- |
| Runtime capability state | Runtime capability composer | Runtime build coordinator | Created per run/build | Disabled/denied capability tests |
| Composition metrics | Runtime capability composer | Metrics sink/tests | Recorded per composition stage | Missing-stage test |
