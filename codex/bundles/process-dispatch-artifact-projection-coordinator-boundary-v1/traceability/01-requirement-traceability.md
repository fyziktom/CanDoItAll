# Requirement Traceability

| Requirement | Source input | Owning subbundles | Planned proof |
| --- | --- | --- | --- |
| RQ-001 | Continue smaller dispatcher isolation | SB01-SB56 | Helper/coordinator extraction, line-count review, source scans |
| RQ-002 | Preserve original functionality | SB09-SB48, SB54 | Focused projection tests and regression matrix |
| RQ-003 | Keep helper code module-local | SB04, SB08, SB52, SB56 | Source scans for project/path drift |
| RQ-004 | Do not rush Process Core or driver APIs | SB04, SB08, SB14, SB20, SB26, SB32, SB38, SB44, SB48, SB52, SB56 | No-driver source scans |
| RQ-005 | Do not create Process Core | SB04, SB08, SB14, SB20, SB26, SB32, SB38, SB44, SB48, SB52, SB56 | No-core source scans |
| RQ-006 | Separate planning from coordination | SB05-SB52 | Source assertions and focused tests |
| RQ-007 | Centralize candidate state updates | SB07-SB08, SB52 | Unit tests and source assertions |
| RQ-008 | Migrate each projection source path | SB09-SB48 | Family-specific positive and negative tests |
| RQ-009 | Keep decision projection record-only | SB45-SB48 | Decision artifact tests and source assertions |
| RQ-010 | Provide focused tests | SB13, SB19, SB25, SB31, SB37, SB43, SB54 | Test transcripts |
| RQ-011 | Keep browser validation N/A | all gates | Execution-report browser analytics rows |
| RQ-012 | No mobile proof artifacts | all gates | Proof-path scans |
| RQ-013 | Documentation-only driver-readiness map | SB53, SB56 | Architecture document and no-driver scan |
| RQ-014 | Run broad final validation | SB54-SB56 | Build and regression transcripts |
| RQ-015 | Document unrelated failures | SB55-SB56 | Known-failure ledger and final report |
