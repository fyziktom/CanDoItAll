# Dependency graph

```mermaid
flowchart LR
    SB00 --> CP0
    CP0 --> SB01
    SB01 --> SB02
    SB02 --> SB03
    SB03 --> SB04
    SB04 --> SB05
    SB05 --> SB06
    SB06 --> CP1
    CP1 --> SB07
    SB07 --> SB08
    SB08 --> SB09
    SB09 --> CP2
    CP2 --> SB10
    SB10 --> SB11
    SB11 --> FINAL
```

## Critical path

`SB00 -> CP0 -> SB01 -> SB02 -> SB03 -> SB04 -> SB05 -> SB06 -> CP1 -> SB07 -> SB08 -> SB09 -> CP2 -> SB10 -> SB11 -> FINAL`

No parallel implementation is authorized before CP1 because the API depends on locked backend
semantics.
