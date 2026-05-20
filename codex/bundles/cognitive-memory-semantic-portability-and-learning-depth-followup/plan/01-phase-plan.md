# Phase Plan

## Execution Order

| Phase | Subbundle | Purpose |
|---|---|---|
| 1 | SB01 | Fix proof portability and semantic invariant gates before production work continues. |
| 2 | SB02 | Add failing-first adversarial tests for all remaining gaps. |
| 3 | SB03-SB08 | Implement cognitive-memory behavior fixes. |
| 4 | SB09 | Refactor maintainability and configuration. |
| 5 | SB10 | Run end-to-end red-team closure and completed-stage proof. |

## Subbundle Dependency Map

```mermaid
flowchart LR
    SB01[SB01 Proof Portability + Invariant Gates] --> SB02[SB02 Failing-first Corpus]
    SB02 --> SB03[SB03 Cross-project + Approx Candidate Discovery]
    SB02 --> SB04[SB04 Coverage-aware Clustering]
    SB03 --> SB05[SB05 Claim-aware Dream Synthesis]
    SB04 --> SB05
    SB05 --> SB06[SB06 Deep Entailment Validation]
    SB02 --> SB07[SB07 Natural Professor Capture]
    SB07 --> SB08[SB08 Event-backed Mastery + Fading]
    SB05 --> SB08
    SB06 --> SB08
    SB08 --> SB09[SB09 Recall Brief + Lineage]
    SB03 --> SB09
    SB09 --> SB10[SB10 Maintainability + E2E Red-team Closure]
```

## Critical Subbundles

- SB01 is critical because it prevents another shallow or non-portable completion.
- SB02 is critical because production changes must be driven by failing-first semantic tests.
- SB03 is critical because `CrossProjectWeekly` is currently semantically misleading.
- SB05 is critical because dream claim grouping can merge unrelated claims.
- SB07 and SB08 are critical because professor learning must not retire human source truth based on keyword evidence.
- SB09 is critical because recall must be useful without flooding consumers while preserving exact lineage on request.
- SB10 is critical because final closure must prove the whole loop, not only isolated helpers.

## Phase Gates

- Codex must install and use the updated skills from SB01 before starting SB02.
- No production cognitive-memory code may change before SB02 failing-first tests are committed and documented.
- No subbundle may close without a `semantic-invariants` artifact and a proof manifest.
- No feature subbundle may cite only existing broad tests; it must cite at least one targeted failing-first and one targeted passing transcript.
- Final closure must run completed-stage bundle validation, targeted unit tests, broad cognitive-memory tests, fake-proof fixtures, anti-stub audit, scope guard, and red-team review.
