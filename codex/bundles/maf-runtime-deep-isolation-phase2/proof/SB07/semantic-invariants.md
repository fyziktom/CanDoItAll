# SB07 Semantic Invariants

## Invariants

- New private runtime builders must fail tests.
- Builder constructors accepting `MafAgentRuntime owner` must fail tests.
- Tests should target extracted collaborators directly.

## Shallow-Pass Trap

- A guard that only checks file count would allow private nested classes to return.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative Proof |
| --- | --- | --- | --- | --- |
| Architecture guard rules | Unit tests/source scans | Future contributors | Run in unit suite | Injected forbidden-pattern test or scan proof |
