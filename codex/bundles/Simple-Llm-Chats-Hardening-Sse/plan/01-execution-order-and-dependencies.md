# Execution order and dependencies

```mermaid
flowchart TD
    SB00[SB00 Sync and proof] --> CP0{CP0 Ready?}
    CP0 -->|yes| SB01[Canonical transaction]
    SB01 --> SB02[Atomic state machine]
    SB02 --> SB03[Whole-use-case profile fence]
    SB03 --> SB04[Durable dispatcher lease]
    SB04 --> SB05[Bounded queries]
    SB05 --> SB06[Backend checkpoint]
    SB06 --> CP1{CP1 Ready?}
    CP1 -->|yes| SB07[Provider streaming]
    SB07 --> SB08[Durable event journal]
    SB08 --> SB09[202 + SSE]
    SB09 --> SB10[API security/client contract]
    SB10 --> SB11[Focused behavioral proof]
    SB11 --> CP2{CP2 Ready?}
    CP2 -->|yes| SB12[Docs and guards]
    SB12 --> SB13[Final stable gate + CI]
    SB13 --> FINAL{Ready for next bundle?}
```

## Critical foundations

- SB01–SB04 select data and execution semantics. Reopening any invalidates CP1 and all later proof.
- SB07 selects provider-streaming semantics. Reopening it invalidates event/SSE proof.
- SB08 selects durable event semantics. Reopening it invalidates reconnect and external-client proof.
- SB09 selects HTTP lifetime semantics. Reopening it invalidates API proof.

## Parallelism

No production subbundles are parallelized because each one selects the next protocol boundary.
Independent source inspection and test-fixture preparation may occur in parallel, but implementation
and closure stay sequential.
