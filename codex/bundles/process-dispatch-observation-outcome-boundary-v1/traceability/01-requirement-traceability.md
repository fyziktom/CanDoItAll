# Requirement Traceability

| Requirement | Raw note | Owning subbundles | Required proof |
| --- | --- | --- | --- |
| RQ-001 | Preserve original functionality | SB01, SB05-SB40, SB43-SB44 | Focused tests and broad smoke matrix |
| RQ-002 | Do not create Process Core yet | SB01, SB04, SB42, SB44, SB48 | No-core source scans |
| RQ-003 | Do not introduce production driver APIs | SB01, SB04, SB41, SB42, SB44, SB48 | No-driver source scans |
| RQ-004 | Extract session-state observation | SB05-SB08 | Session observation tests and source assertions |
| RQ-005 | Extract execution-log/tool observation | SB09-SB12 | Execution-log tests and source assertions |
| RQ-006 | Extract declared outcome parsing and branch selection | SB17-SB20 | Declared outcome focused tests |
| RQ-007 | Extract completion status decisions | SB21-SB28 | Completion status focused tests |
| RQ-008 | Extract completion reason helpers | SB29-SB32 | Completion reason focused tests |
| RQ-009 | Keep side effects out of pure helpers | SB04, SB08, SB12, SB16, SB20, SB24, SB28, SB32, SB36, SB40, SB44, SB48 | Source scans for forbidden side-effect tokens |
| RQ-010 | Slim ToolValidation safely | SB37-SB40 | Line-count transcript and source boundary review |
| RQ-011 | Keep browser/UI proof N/A | All subbundles | No UI/prohibited proof-path scan |
| RQ-012 | Add documentation-only driver-readiness map | SB41-SB44, SB48 | Markdown map and no production API scan |
| RQ-013 | Use staged gates | SB01-SB48 | Execution report gate rows and critical manifests |
