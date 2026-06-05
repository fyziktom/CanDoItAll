# SB01 Proof Manifest

- Subbundle: SB01 - Entry audit, branch hygiene, existing boundary smoke.
- Status: Completed.
- Owned requirements: RQ-001, RQ-002, RQ-013, RQ-014.
- Owned raw notes: RN-001, RN-002, RN-004.
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`.

## Changed File Manifest

| File | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | `81EF41B768F46A9C1BFF66F2A7770CCC06C0FF6CD69DE86D593A1BB022D99054` | `81EF41B768F46A9C1BFF66F2A7770CCC06C0FF6CD69DE86D593A1BB022D99054` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs` | `186A4F6CD31D8E4B6607B2EE121C70F7EC28DB20668BFCF87CFC6280AA4252F4` | `186A4F6CD31D8E4B6607B2EE121C70F7EC28DB20668BFCF87CFC6280AA4252F4` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | `DD9668EDFCB0251590A5027B4B2612E28507FE90C0520DE2913419798D172C82` | `DD9668EDFCB0251590A5027B4B2612E28507FE90C0520DE2913419798D172C82` |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` | `FDB8FA969108D223B1C24599D1BF7E7C475B6243ED77499C308860B99C27240B` | `FDB8FA969108D223B1C24599D1BF7E7C475B6243ED77499C308860B99C27240B` |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` | `3ED5FE435ECAF2DC65E1413C8E2046E60E639E51ED56CDD350D459974FC8902D` | `3ED5FE435ECAF2DC65E1413C8E2046E60E639E51ED56CDD350D459974FC8902D` |

## Command Transcripts

- Branch and line counts: `bundle://proof/SB01/transcripts/sb01-branch-and-line-counts.txt`.
- No-core/no-driver scan: `bundle://proof/SB01/transcripts/sb01-no-core-no-driver-scan.txt`.
- No-UI/no-prohibited-viewport scan: `bundle://proof/SB01/transcripts/sb01-no-ui-or-prohibited-viewport-proof-scan.txt`.
- Anti-stub scan: `bundle://proof/SB01/transcripts/sb01-anti-stub-scan.txt`.
- Broad architecture-class baseline failure: `bundle://proof/SB01/transcripts/sb01-architecture-test.txt`.
- Focused architecture guardrail pass: `bundle://proof/SB01/transcripts/sb01-focused-architecture-tests.txt`.

## Failing-First Proof

- `bundle://proof/SB01/transcripts/sb01-architecture-test.txt` demonstrates the broad historical architecture class cannot be used as clean proof because unrelated old bundle artifact paths are absent.

## Passing Proof

- `bundle://proof/SB01/transcripts/sb01-focused-architecture-tests.txt` passed two current-scope guardrails.
- `bundle://proof/SB01/transcripts/sb01-no-core-no-driver-scan.txt` passed.
- `bundle://proof/SB01/transcripts/sb01-no-ui-or-prohibited-viewport-proof-scan.txt` passed.
- `bundle://proof/SB01/transcripts/sb01-anti-stub-scan.txt` passed.

## Source Assertions

- `bundle://proof/SB01/source-assertions/source-shape.md`.

## Anti-Stub Audit

- Command transcript: `bundle://proof/SB01/transcripts/sb01-anti-stub-scan.txt`.

## Browser Proof

- N/A. Runtime/service refactor only; no UI files changed.
