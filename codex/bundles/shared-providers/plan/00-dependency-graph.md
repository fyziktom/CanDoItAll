# Dependency graph

```mermaid
flowchart TD
    SB00[SB00 Baseline and decision lock]
    SB01[SB01 Protocol, identities, access context]
    SB02[SB02 Persistence and reconciliation model]
    SB03[SB03 Central catalog API]
    SB04[SB04 OpenAI-compatible relay]
    SB05[SB05 Client source sync and imports]
    SB06[SB06 Shared connector runtime projection]
    SB07[SB07 Backend checkpoint + 3 app instances]
    SB08[SB08 Desktop UI]
    SB09[SB09 Component + Playwright proof]
    SB10[SB10 Docs, Compose, operator tooling]
    SB11[SB11 OpenAPI + SharedInfo]
    SB12[SB12 Final gate + running handoff]

    SB00 --> SB01
    SB01 --> SB02
    SB02 --> SB03
    SB03 --> SB04
    SB02 --> SB05
    SB03 --> SB05
    SB04 --> SB06
    SB05 --> SB06
    SB06 --> SB07
    SB07 --> SB08
    SB08 --> SB09
    SB09 --> SB10
    SB10 --> SB11
    SB11 --> SB12
```

## Critical foundations

- SB00 project/dependency and runtime-path decision lock.
- SB01 wire/identity/access-context contracts.
- SB02 relational ownership and transaction semantics.
- SB07 backend behavior across real instances.

Downstream work may not compensate for failure in a foundation.

## Parallelism policy

The bundle is intentionally serialized for one Codex run. Small documentation/test-fixture work
may be prepared inside its owning subbundle, but no downstream production implementation starts
before its dependency gate.

## Reopen propagation

- protocol change -> reopen SB01 and recheck SB03-SB12;
- entity/invariant change -> reopen SB02 and recheck SB03, SB05-SB12;
- capability/relay change -> reopen SB04 and recheck SB06-SB12;
- runtime projection change -> reopen SB06 and recheck SB07-SB12;
- public API/OpenAPI change after SB11 -> reopen SB11 and SB12;
- UI-only copy/layout change after SB09 -> reopen SB09-SB12, not backend foundations unless
  service contract changed.
