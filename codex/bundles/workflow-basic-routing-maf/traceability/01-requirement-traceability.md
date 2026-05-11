# Requirement Traceability

| Requirement | Primary subbundle | Secondary subbundle | Proof type |
|---|---|---|---|
| RQ-001 typed routing contract | 01 | 04 | Model and serialization tests |
| RQ-002 legacy compatibility | 01 | 04 | Old JSON/defaulting tests |
| RQ-003 MAF predicate edges | 02 | 05 | Runtime executor invocation tests |
| RQ-004 MAF switch/default | 02 | 05 | Runtime switch/default tests |
| RQ-005 MAF fan-out | 02 | 05 | Runtime fan-out target tests |
| RQ-006 JSON payload evaluator | 02 | 01 | Unit evaluator tests |
| RQ-007 canvas route builder | 03 | 05 | Component and browser tests |
| RQ-008 route summaries | 03 | 05 | Component/browser screenshots |
| RQ-009 persistence/API | 04 | 05 | Integration tests |
| RQ-010 route scenarios | 04 | 05 | Fixtures and runtime proof |
| RQ-011 no arbitrary code | 01 | 02, 05 | Source review and tests |
| RQ-012 ARTL rejected now | 01 | 05 | Validation/compiler tests |
| RQ-013 no silent fallback | 01 | 02, 05 | Validation/compiler tests |
| RQ-014 serializable route contract | 01 | 04 | JSON round-trip tests |
| RQ-015 ARTL seam | 01 | 02, 05 | Architecture review |
| RQ-016 model tests | 01 | 05 | Unit test proof |
| RQ-017 runtime tests | 02 | 05 | Unit/runtime test proof |
| RQ-018 component tests | 03 | 05 | Component test proof |
| RQ-019 integration tests | 04 | 05 | Integration test proof |
| RQ-020 browser proof | 03 | 05 | Screenshots and browser analytics |
