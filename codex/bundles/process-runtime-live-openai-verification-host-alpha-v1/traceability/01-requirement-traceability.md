# Input Coverage Matrix

| Raw Note | Requirement IDs | Owning Subbundles | Planned Proof |
| --- | --- | --- | --- |
| Review real code, not only bundle report. | REQ-001 | SB001-SB003 | Source scans, focused baseline tests, current commit source inventory. |
| Look at real test outcome. | REQ-001, REQ-003 | SB001-SB012, SB049-SB051 | Build, unit, focused integration, Playwright matrix. |
| Use OpenAI credits for actual test. | REQ-002 | SB004-SB009 | Live OpenAI smoke with explicit budget/timeout and redaction. |
| Move toward generic runtime host/registry/selector/DI/manager. | REQ-004 to REQ-009 | SB013-SB039 | Verification-only host, registry, selector, DI, manager-readonly command, audit persistence. |
| Keep generic Process Core with domain drivers. | REQ-010 | SB040-SB045 | Core dependency/domain leakage scans and future execution-capable gate. |
| Prepare detailed bundle zip. | REQ-011, REQ-012 | SB052-SB060 | Validators, final red-team, zip generation. |
