# Input Coverage Matrix

| Raw input | Normalized requirement | Owning subbundles | Proof |
| --- | --- | --- | --- |
| Do not rush Process Core | REQ-002, REQ-013 | SB003, SB030, SB031-SB033 | Core/no-core scans, `bundle://architecture/07-core-extraction-readiness-scorecard.md`, and final red-team closure |
| Preserve all original functionality | REQ-001 | All | `bundle://proof/SB032/transcripts/build.txt`, `bundle://proof/SB032/transcripts/full-unit-tests.txt`, and focused integration transcripts |
| Fewer broader subbundles | REQ-013 | All | 33 individually closed subbundles across 11 phases in `bundle://reviews/01-execution-report.md` |
| More areas in one run | REQ-004 to REQ-012 | SB004-SB030 | Multi-area phase plan plus evidence sections SB004-SB030 |
| Prepare drivers safely | REQ-012 | SB028-SB030 | Documentation-only driver readiness and no-production-driver Gate J proof |
| No UI/mobile proof | REQ-014 | All | Source scans with UI/media diff checks; browser validation N/A |
