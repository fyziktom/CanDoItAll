# Requirement Traceability

| Requirement | Source input | Owning subbundles | Proof expected |
| --- | --- | --- | --- |
| RQ-001 | Branch review | SB01, SB04, SB16, SB19 | build, route parity tests |
| RQ-002 | User request | SB02 | inventory and source scans |
| RQ-003 | User request | SB04 | architecture tests |
| RQ-004 | Dispatch.cs database blocker | SB05-SB06 | transition request tests |
| RQ-005 | Dispatch.cs missing inputs | SB07-SB08 | gap facts tests |
| RQ-006 | Materialization fingerprint | SB09 | exact fingerprint tests |
| RQ-007 | Block reason/directive | SB10-SB12 | text/directive parity tests |
| RQ-008 | Journal coordinator | SB11 | duplicate journal tests |
| RQ-009 | Rerun request | SB12-SB13 | request field tests |
| RQ-010 | Full migration | SB13-SB15 | integration tests |
| RQ-011 | Runtime smoke | SB16 | focused tests and build |
| RQ-012 | Driver readiness docs only | SB17 | no driver API scan |
| RQ-013 | Line count cleanup | SB18 | line count transcript |
| RQ-014 | Final red-team | SB20 | completed validator and scans |
