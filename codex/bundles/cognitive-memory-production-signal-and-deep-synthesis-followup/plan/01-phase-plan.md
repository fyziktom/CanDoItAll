# Phase Plan

## Execution Order

1. Execute SB01 and install the stronger workflow/validator gates.
2. Execute SB02 and prove the current implementation fails the new behavioral regressions.
3. Execute SB03-SB08 in dependency order.
4. Execute SB09 maintainability refactors after behavior is protected by tests.
5. Execute SB10 final red-team proof and completed-stage validation.

## Subbundle Dependency Map

```mermaid
graph TD
  SB01[SB01 Proof gates] --> SB02[SB02 Failing-first corpus]
  SB02 --> SB03[SB03 Accepted-use emitter]
  SB02 --> SB04[SB04 Comparison lifecycle]
  SB02 --> SB05[SB05 Multilingual capture]
  SB02 --> SB06[SB06 Deep dream synthesis]
  SB02 --> SB07[SB07 Semantic clustering]
  SB02 --> SB08[SB08 Recall brief lineage]
  SB03 --> SB10[SB10 E2E proof]
  SB04 --> SB10
  SB05 --> SB10
  SB06 --> SB10
  SB07 --> SB10
  SB08 --> SB10
  SB03 --> SB09[SB09 Maintainability]
  SB04 --> SB09
  SB05 --> SB09
  SB06 --> SB09
  SB07 --> SB09
  SB08 --> SB09
  SB09 --> SB10
```

## Critical Subbundles

- SB01 is critical because without stronger proof gates Codex can repeat the current consumer-only implementation failure.
- SB02 is critical because production changes must be guarded by failing-first tests.
- SB03 is critical because professor assimilation currently lacks production accepted-use evidence emission.
- SB06 is critical because current dream synthesis still stores meta-evidence text instead of internalized knowledge.
- SB08 is critical because recall output must be useful and referenceable without overwhelming the requester.
- SB10 is critical because the full lifecycle must be proven end to end.

## Phase Gates

- Gate A: SB01 completed and validator fake-proof fixtures fail/pass as intended.
- Gate B: SB02 tests fail on current implementation before production fixes.
- Gate C: SB03-SB08 pass targeted production behavior tests without manual test seeding of production-only signals.
- Gate D: SB09 refactors compile and do not weaken behavior tests.
- Gate E: SB10 end-to-end proof, raw-note closure, artifact-backed proof manifests, and completed-stage validation all pass.
