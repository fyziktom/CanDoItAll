# SB02 Semantic Invariants

## Invariants

- Configuration DTO extraction must preserve existing JSON/configuration semantics.
- Composition state must not reference `MafAgentRuntime.*` private nested DTOs.

## Shallow-Pass Trap

- Moving type declarations without testing default/null behavior could compile but break capability loading.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative Proof |
| --- | --- | --- | --- | --- |
| Runtime configuration DTOs | Runtime configuration reader | Capability composer/builders | Created per runtime build | Invalid/missing configuration tests |
