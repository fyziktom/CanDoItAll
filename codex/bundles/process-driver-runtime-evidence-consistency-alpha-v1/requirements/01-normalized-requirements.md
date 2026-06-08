# Normalized Requirements

| ID | Requirement | Owner |
| --- | --- | --- |
| REQ-001 | Reconcile latest branch source and proof after reported Codex crash before changing production code. | SB001-SB003 |
| REQ-002 | Decompose `TranscriptVerificationAlphaVerifier` into parser, request policy, evidence hash, redaction, diagnostics, and audit collaborators without behavior drift. | SB004-SB006 |
| REQ-003 | Preserve driver abstraction contract compatibility and versioning. | SB007-SB009 |
| REQ-004 | Decompose `ProcessTranscriptVerificationReadOnlyAdapter` into process-owned preflight/evidence/observation helpers without introducing runtime wiring. | SB010-SB012 |
| REQ-005 | Harden evidence URI/hash policy and reject untrusted or mismatched supplied evidence. | SB013-SB015 |
| REQ-006 | Preserve audit/redaction/no-mutation semantics and prove sensitive text cannot leak into diagnostics or audit summaries. | SB016-SB018 |
| REQ-007 | Implement read-only runtime evidence consistency verifier alpha over supplied Core descriptor payloads. | SB019-SB024 |
| REQ-008 | Add process-module read-only adapter for runtime evidence consistency without registry/DI/manager/scheduler/workflow hooks. | SB025-SB027 |
| REQ-009 | Keep Core descriptor consumers allow-listed and prevent Core reverse dependency on drivers. | SB028-SB030 |
| REQ-010 | Expand malicious transcript and contradictory descriptor corpus. | SB031-SB033 |
| REQ-011 | Harden Office/business-analysis lanes as denied/read-only future lanes. | SB034-SB036 |
| REQ-012 | Add shared verification test harness and reusable invariants for all driver alphas. | SB037-SB039 |
| REQ-013 | Refresh docs, package README, migration guide, and runtime host roadmap without implementing runtime host. | SB040-SB045 |
| REQ-014 | Close with broad build/unit/focused integration/source scans/bundle validators and red-team proof. | SB046-SB054 |

## Hard Constraints
- No broad Core runtime extraction.
- No runtime driver registry, selector, host, provider, DI registration, manager command, scheduler hook, workflow hook, or execution-capable driver.
- No shell execution, package restore, Office/Graph calls, external connector calls, workspace/storage writes, process mutation, claim mutation, transition mutation, finalizer application, provider repair, or retry scheduling.
- No UI/browser/mobile/small/medium proof.
- All critical subbundles require Semantic Adequacy Gate proof and artifact-backed manifests.
