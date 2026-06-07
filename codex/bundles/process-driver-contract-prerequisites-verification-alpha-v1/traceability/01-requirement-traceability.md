# Requirement Traceability

| Requirement | Bundle destination | Owning subbundles | Proof expectation |
| --- | --- | --- | --- |
| REQ-001 | `bundle://analysis/01-current-state.md`, `bundle://reviews/01-execution-report.md` | SB001-SB003 | Branch/proof review, build/test/source scans |
| REQ-002 | `bundle://architecture/01-target-solution.md`, Core API governance docs/tests | SB004-SB006, SB022-SB024 | Core API snapshot, dependency guard, consumer allow-list |
| REQ-003 | Permission mode docs/tests | SB007-SB009 | Missing-mode and read-only denial tests |
| REQ-004 | Audit/redaction docs/tests | SB010-SB012 | Sensitive-field denial and redaction tests |
| REQ-005 | Sandbox/command policy docs/tests | SB013-SB015 | No shell, Office, storage, workspace, or mutation executor proof |
| REQ-006 | Verification-only rehearsal docs/tests | SB016-SB018 | Test-only request/result and denial contracts |
| REQ-007 | Transcript verifier alpha preparation | SB019-SB021 | Read-only transcript inspection taxonomy and denial proof |
| REQ-008 | Descriptor consumer hardening | SB022-SB024 | Explicit adapter map and side-effect boundary scans |
| REQ-009 | Office/business lane denial tests | SB025-SB027 | Read-only evidence lane denial proof |
| REQ-010 | Production driver contract decision | SB028-SB030 | Explicit defer-or-next-bundle decision and no-runtime scan |
| REQ-011 | Core docs and compatibility roadmap | SB031-SB033 | Public API docs and migration notes matching code |
| REQ-012 | Long-range domain driver roadmap | SB034-SB036 | Release gates for stable Core and domain drivers |
| REQ-013 | Final validation and bundle handoff | SB037-SB039 | Build, tests, scans, red-team review, completed validator, proof index |
