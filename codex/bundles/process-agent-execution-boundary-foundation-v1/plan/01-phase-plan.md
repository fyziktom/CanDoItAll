# Phase Plan

## Subbundle Dependency Map

```mermaid
flowchart TD
    SB01[SB01 Entry audit and branch hygiene]
    SB02[SB02 Process boundary inventory]
    SB03[SB03 Execution seam design]
    GATEA[Gate A Refactor checkpoint]
    SB04[SB04 Architecture guardrails]
    SB05[SB05 Execution facade foundation]
    SB06[SB06 Move direct execution calls]
    GATEB[Gate B Refactor checkpoint]
    SB07[SB07 Coupling reduction proof]
    SB08[SB08 Minimal contracts foundation]
    SB09[SB09 Receipt and required-tool hardening]
    GATEC[Gate C Refactor checkpoint]
    SB10[SB10 Boundary consistency review]
    SB11[SB11 Runtime smoke and large-screen policy check]
    SB12[SB12 Final red-team and next cutline]

    SB01 --> SB02 --> SB03 --> GATEA --> SB04 --> SB05 --> SB06 --> GATEB --> SB07 --> SB08 --> SB09 --> GATEC --> SB10 --> SB11 --> SB12
```

## Critical Subbundles

| Subbundle | Criticality | Why |
| --- | --- | --- |
| SB01 | Critical | Establishes clean baseline and rejects previous-regression drift |
| SB03 | Critical | Defines seam before movement |
| SB04 | Critical | Adds guardrails before production changes |
| SB06 | Critical | Moves direct execution calls and can regress runtime behavior |
| SB09 | Critical | Protects receipt/artifact semantics |
| SB12 | Critical | Final closure and next-phase Process Core cutline |

## Phase Gates

### Gate A after SB03

- Inventory complete.
- Execution seam design approved.
- No production movement yet except tests/scans.
- Large-screen-only proof policy recorded.

### Gate B after SB06

- Dispatcher execution path uses facade/client for start/detail/adoption/recovery calls.
- Targeted tests pass.
- Source scans prove direct call reduction.

### Gate C after SB09

- Receipt/required-tool/artifact lineage tests pass.
- Contracts foundation has not absorbed EF/UI/Maf dependencies.
- No mobile/small/medium proof artifacts.

## Implementation Cadence

Codex should work subbundle by subbundle. After each gate, perform a source-size and dependency review before continuing.
