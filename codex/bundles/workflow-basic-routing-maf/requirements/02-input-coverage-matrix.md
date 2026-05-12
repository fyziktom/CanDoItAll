# Input Coverage Matrix

| Input | Requirement coverage | Subbundle coverage | Notes |
|---|---:|---:|---|
| Use prepared MAF routing now | RQ-003, RQ-004, RQ-005 | 02, 05 | Compiler must use actual MAF predicate/switch/fan-out APIs. |
| Future replacement by ARTL | RQ-012, RQ-015 | 01, 02, 05 | Add language and compiler seam only; no ARTL parser now. |
| Add routing to workflows | RQ-001 through RQ-006 | 01, 02 | Domain and runtime foundations. |
| Add routing to workflow canvas | RQ-007, RQ-008, RQ-020 | 03, 05 | UI plus browser proof. |
| Existing workflows in uploaded code | RQ-002, RQ-009 | 01, 04 | Compatibility and persistence. |
| Execution-grade bundle | RQ-016 through RQ-020 | all | Every subbundle has proof requirements and progression gates. |
