# Normalized Requirements

| ID | Requirement | Owner Subbundles | Proof |
| --- | --- | --- | --- |
| REQ-001 | Reconcile current branch and previous runtime restoration proof from real code, not report-only claims. | SB001-SB003 | Source scans, build/unit/focused baseline. |
| REQ-002 | Run live OpenAI smoke when key is present with explicit low budget/timeout and no secret logging. | SB004-SB009 | Live smoke transcript or explicit key-absent/opt-out skip. |
| REQ-003 | Preserve deterministic process runtime regression coverage while live proof is added. | SB004-SB012 | Deterministic `.NET` and business-analysis smoke reruns. |
| REQ-004 | Add verification-only host contracts without execution-capable API. | SB013-SB015 | Public API snapshot and forbidden-token scan. |
| REQ-005 | Add explicit registry and selector with no auto-discovery or fallback selection. | SB016-SB018 | Registry/selector negative tests. |
| REQ-006 | Add DI registration only for verification host and known read-only lanes. | SB019-SB021 | DI scope tests and source scan. |
| REQ-007 | Add manager-readonly command/API/service facade returning diagnostics only. | SB022-SB027 | Manager command integration and no-mutation proof. |
| REQ-008 | Add immutable audit persistence or migration-ready audit boundary for host invocations. | SB028-SB033 | Migration/schema/readback/redaction proof. |
| REQ-009 | Add scheduler/workflow readiness docs/tests without enabling driver execution hooks. | SB034-SB039 | Scheduler/workflow negative tests and readiness matrix. |
| REQ-010 | Keep Process Core generic and dependency-clean. | SB040-SB045 | Core dependency scan and domain-leakage tests. |
| REQ-011 | Run release-candidate UI/API/runtime matrix after host alpha. | SB046-SB054 | Build, full unit, focused integration, Playwright large desktop. |
| REQ-012 | Close with future execution-capable driver approval backlog and red-team proof. | SB055-SB060 | Final validators, runbook, red-team rejection. |
