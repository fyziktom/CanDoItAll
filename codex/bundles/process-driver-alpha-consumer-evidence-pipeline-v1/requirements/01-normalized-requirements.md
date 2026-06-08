# Normalized Requirements

| ID | Requirement | Owning Subbundles |
| --- | --- | --- |
| REQ-001 | Recheck latest branch state and proof before planning new work. | SB001-SB003 |
| REQ-002 | Keep Process Core deterministic, dependency-clean, and free of driver references. | SB004-SB006, SB031-SB033 |
| REQ-003 | Harden alpha verifier parser and transcript fixture coverage without command execution. | SB007-SB009 |
| REQ-004 | Add a process-module read-only consumer adapter boundary for the transcript verifier. | SB010-SB012 |
| REQ-005 | Add explicit supplied evidence/transcript payload and hash policy; do not allow arbitrary file reads. | SB013-SB015 |
| REQ-006 | Normalize verifier diagnostics into process-owned read-only evidence envelopes. | SB016-SB018 |
| REQ-007 | Enforce audit facts, redaction, no-mutation proof, and denial behavior. | SB019-SB021 |
| REQ-008 | Add test-only process workflow/evidence consumer rehearsal without runtime hooks. | SB022-SB024 |
| REQ-009 | Harden Core descriptor compatibility and consumer allow-lists. | SB025-SB027 |
| REQ-010 | Expand .NET/Rust domain transcript coverage for realistic build/test/proof transcripts. | SB028-SB030 |
| REQ-011 | Keep Office and business-analysis lanes read-only with denial tests. | SB034-SB036 |
| REQ-012 | Prepare runtime-host roadmap without implementing registry, DI, manager command, or selector. | SB037-SB039 |
| REQ-013 | Close broad smoke with build, full unit, focused integration, source scans, anti-stub, validators, and red-team. | SB052-SB054 |

## Hard Constraints

- No broad Process Core runtime extraction.
- No generic production driver registry, selector, DI registration, manager command, workflow hook, scheduler hook, or runtime host.
- No shell execution, package restore, Graph/Office calls, workspace/storage writes, process mutation, claim mutation, transition mutation, finalizer application, retry scheduling, or provider repair in this bundle.
- The transcript verifier may read only supplied transcript text and supplied evidence references/payloads.
- Driver work must remain verification-only for the `.NET/Rust` lane.
- Office and business-analysis lanes remain read-only denial/proposal lanes.
- No UI/browser/mobile/small/medium proof unless UI files unexpectedly change; if they do, fail and re-scope.
