# Normalized Requirements

| ID | Requirement | Owner |
| --- | --- | --- |
| RQ-001 | Preserve the completed MAF/provider/process-tool decoupling. | SB01, SB04, SB16 |
| RQ-002 | Do not introduce Process Core, process drivers, or driver-pack production APIs. | All |
| RQ-003 | Inventory the current StepCompletionFinalizer responsibilities and side effects before moving code. | SB02 |
| RQ-004 | Add architecture guardrails before production movement. | SB04 |
| RQ-005 | Extract finalizer value types to module-local files without behavior change. | SB05 |
| RQ-006 | Extract artifact content reader boundary while preserving workspace/storage fallback behavior. | SB06 |
| RQ-007 | Extract validation context/result builder helpers without moving DB persistence. | SB07 |
| RQ-008 | Prove parity after type/content-reader movement before later subbundles proceed. | SB08 |
| RQ-009 | Extract artifact-validation orchestration helper without changing required artifact satisfaction. | SB09 |
| RQ-010 | Extract runtime invariant audit builder while keeping persistence and blocking policy unchanged. | SB10 |
| RQ-011 | Extract transition request builder while preserving artifact-validation context fields. | SB11 |
| RQ-012 | Prove finalizer parity after helper extractions. | SB12 |
| RQ-013 | Extend driver-readiness documentation for finalizer/evidence semantics only. | SB13 |
| RQ-014 | Record line counts and rebalance oversized helpers. | SB14 |
| RQ-015 | Run runtime smoke and enforce no prohibited viewport proof. | SB15 |
| RQ-016 | Final red-team must choose the next safe seam and reject premature Core/driver work. | SB16 |
