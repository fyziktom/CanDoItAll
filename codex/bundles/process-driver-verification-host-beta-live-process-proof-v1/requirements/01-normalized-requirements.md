# Normalized Requirements

| ID | Requirement | Owning phase | Proof |
| --- | --- | --- | --- |
| REQ-001 | Verify current source and tests, not just bundle report. | P01 | source inventory, scans, focused tests |
| REQ-002 | Keep no transient bundle-path dependencies in `src` or `tests`. | P01 | rg scan + unit guard |
| REQ-003 | Classify current live OpenAI proof accurately. | P02 | live transcript review + source test review |
| REQ-004 | Add a guarded live process-run OpenAI smoke. | P03 | opt-in integration test + redacted transcript |
| REQ-005 | Keep deterministic runtime scenarios as fallback/safety net. | P04 | deterministic focused integration matrix |
| REQ-006 | Harden verification host API to async/cancellable and non-throwing for expected denials. | P05 | host API tests |
| REQ-007 | Add host options for lane enablement, payload limits, timeout, and emergency disable. | P06 | options tests + DI proof |
| REQ-008 | Harden registry and selector: exact lane, no fallback, no discovery. | P07 | selector negative tests |
| REQ-009 | Replace in-memory-only audit with durable audit boundary and query API. | P08 | migration/entity/query tests |
| REQ-010 | Add manager-readonly API/service facade without process mutation. | P09 | service/API tests |
| REQ-011 | Add manager-visible UI/API smoke for verification host diagnostics. | P10 | large desktop/API proof |
| REQ-012 | Add scheduler/workflow read-only verification readiness without driver execution hooks. | P11 | source/integration proof |
| REQ-013 | Keep Process Core generic and dependency-clean. | P12 | source scan/API snapshot |
| REQ-014 | Keep execution-capable driver host blocked behind explicit future gates. | P13 | approval matrix tests |
| REQ-015 | Produce release-candidate matrix and red-team proof. | P20-P22 | build/unit/integration/live/source scans/validators |
