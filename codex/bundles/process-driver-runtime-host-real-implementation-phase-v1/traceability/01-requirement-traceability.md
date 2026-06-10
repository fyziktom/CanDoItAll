# Requirement Traceability

| Requirement | Subbundle | Proof style |
| --- | --- | --- |
| REQ-001 Code-first ratio | SB01, SB08 | `git diff --numstat`, grouped totals, closure block if ratio fails. |
| REQ-002 Runtime-host contracts | SB02 | Contract/API tests, Core dependency scan, abstraction package/project reference scan. |
| REQ-003 Dry-run pipeline | SB03 | Unit + integration tests over normalizer/evaluator/planner/audit mapper; no side-effect scan. |
| REQ-004 Durable audit | SB04 | EF/in-memory parity tests, cross-scope query, time-window query, retention-ready readback. |
| REQ-005 Capability catalog | SB05 | Static descriptors, no reflection discovery, no self-registration, capability consistency tests. |
| REQ-006 Scheduler/workflow jobs | SB06 | Read-only job lifecycle integration tests, no direct driver hooks. |
| REQ-007 Manager/operator readback | SB07 | API/service/UI-ready DTO tests, no-mutation flags, audit id/hash, evidence counts. |
| REQ-008 Regression + future gate | SB08 | Build, full unit, focused integration, live smoke classification, red-team scans. |
