# SB15 Proof Manifest

- Subbundle: SB15 - Runtime smoke and proof policy.
- Status: Completed.
- Owned requirements: RQ-001, RQ-013, RQ-014.
- Owned raw notes: RN-001, RN-002, RN-003, RN-004.
- Semantic invariant contract: `bundle://proof/SB15/semantic-invariants.md`.

## Changed File Manifest

| File | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| Production source | N/A | No production source changed in SB15 |

## Gate Source Shape

| File | SHA-256 |
| --- | --- |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | `1CABCB3E22F5899CCD6511CDCA279C622F294B4429FDA368C80CA0EF50CD0982` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs` | `D72FF27A0B1375527DCFF953AA990AF728BEB641685D33A53FF429BC00F9521D` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | `DD9668EDFCB0251590A5027B4B2612E28507FE90C0520DE2913419798D172C82` |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` | `F50BF7BDBA41E30EF6E5BB57247E5FDB51C8981BCA217A535CC62C7469B4C6E8` |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` | `72DED50805ED803A3261EC94CE559ED87E9DA791B04FF1B9C6EFC8EEFE6E6365` |

## Command Transcripts

- Full solution build: `bundle://proof/SB15/transcripts/sb15-full-build.txt`.
- Focused dispatch integration tests: `bundle://proof/SB15/transcripts/sb15-focused-dispatch-integration-tests.txt`.
- Focused architecture tests: `bundle://proof/SB15/transcripts/sb15-focused-architecture-tests.txt`.
- Runtime proof-policy scan: `bundle://proof/SB15/transcripts/sb15-runtime-proof-policy-scan.txt`.
- Adversarial proof-policy trap: `bundle://proof/SB15/transcripts/sb15-failing-first-policy-trap.txt`.

## Passing Proof

- Passing transcript: `bundle://proof/SB15/transcripts/sb15-full-build.txt`.
- Passing transcript: `bundle://proof/SB15/transcripts/sb15-focused-dispatch-integration-tests.txt`.
- Passing transcript: `bundle://proof/SB15/transcripts/sb15-focused-architecture-tests.txt`.
- Passing transcript: `bundle://proof/SB15/transcripts/sb15-runtime-proof-policy-scan.txt`.
- `bundle://proof/SB15/transcripts/sb15-full-build.txt` passed.
- `bundle://proof/SB15/transcripts/sb15-focused-dispatch-integration-tests.txt` passed with 20 focused tests.
- `bundle://proof/SB15/transcripts/sb15-focused-architecture-tests.txt` passed with 11 focused tests.
- `bundle://proof/SB15/transcripts/sb15-runtime-proof-policy-scan.txt` passed.

## Source Assertions

- `bundle://proof/SB15/source-assertions/runtime-smoke-proof-policy.md`.

## Anti-Stub Audit

- Anti-stub audit transcript: `bundle://proof/SB15/transcripts/sb15-runtime-proof-policy-scan.txt`.
- `bundle://proof/SB15/transcripts/sb15-runtime-proof-policy-scan.txt`.

## Browser Proof

- N/A. Runtime/service refactor only; no UI files changed.
