# Input Coverage Matrix

| Input / raw note | Requirement ids | Owning subbundles | Proof method |
| --- | --- | --- | --- |
| Continue smaller dispatcher isolation | RQ-001, RQ-006, RQ-008 | SB01-SB56 | Helper/coordinator extraction, line-count review, source scans |
| Do not rush Process Core | RQ-004, RQ-005 | SB04, SB08, SB14, SB20, SB26, SB32, SB38, SB44, SB48, SB52, SB56 | No-core/no-driver source scans |
| Preserve original functionality | RQ-002, RQ-007, RQ-009, RQ-010 | SB09-SB48, SB54 | Focused projection tests and regression matrix |
| Plan more phases | RQ-014, RQ-015 | SB01-SB56 | 56 subbundles with repeated critical gates |
| Prepare future drivers safely | RQ-013 | SB53, SB56 | Documentation-only driver-readiness map |
| No UI/mobile proof | RQ-011, RQ-012 | all gates | N/A browser analytics and proof-path scan |
