# Dependency Graph

```mermaid
flowchart TD
    SB00[SB00 Re-anchor] --> SB01[SB01 Package migration]
    SB01 --> SB02[SB02 Tool/approval regressions]
    SB02 --> WAVEA[Wave A closed]
    WAVEA --> SB03[SB03 Native request/checkpoint]
    SB03 --> SB04[SB04 Persistence/recovery]
    SB04 --> SB05[SB05 API governance]
    SB05 --> FREEZE[FG-01 Freeze]
    FREEZE --> SB06[SB06 E2E and broad gate]
```

## Critical foundations

- **CF-01:** correct MAF 1.18 package graph — lent by SB01.
- **CF-02:** serial tool and approval behavior — lent by SB02.
- **CF-03:** native MAF checkpoint can rehydrate a disposed run — lent by SB03.
- **CF-04:** persistent response operation and deduplication survive replay — lent by SB04.
- **CF-05:** authorized API reaches the same service boundary — lent by SB05.

## Safe parallelism

Within SB00, independent read-only discovery may run in parallel.

After SB01:

- documentation of release changes may proceed independently;
- implementation in SB02 remains sequential with package stabilization.

Within Wave B, avoid parallel edits across:

- workflow contracts;
- runtime manager;
- MAF backend;
- persistent store;
- API DTOs.

These form one dependency chain and overlapping edits are likely to invalidate assumptions.

Test fixture work may proceed in parallel only after the owning public contracts are frozen and file ownership does not overlap.

## Revalidation propagation

- Reopen SB01 when package versions or MAF API signatures change.
- Reopen SB02 when agent option construction or custom chat-client composition changes.
- Reopen SB03 when workflow compiler topology, MAF request-port identity, or checkpoint format changes.
- Reopen SB04 when persistence schema, state transitions, or invocation key material changes.
- Reopen SB05 when auth conventions, API DTOs, or public outcomes change.
- Any reopened critical foundation invalidates SB06 proof.
