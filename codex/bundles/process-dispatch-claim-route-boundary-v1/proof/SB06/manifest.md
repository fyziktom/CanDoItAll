# SB06 Proof Manifest

- Subbundle: SB06 - Execution run selection helper foundation.
- Status: Completed.
- Owned requirements: RQ-003, RQ-006, RQ-008, RQ-009.
- Owned raw notes: RN-001, RN-003, RN-004.
- Semantic invariant contract: `bundle://proof/SB06/semantic-invariants.md`.

## Changed File Manifest

| File | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessAutomationExecutionRunSelection.cs` | `NEW` | `D5A6A4900375C22B844F3B45F2915DBA518BF321C1BFA25F64B8A5CB932C2C08` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs` | `186A4F6CD31D8E4B6607B2EE121C70F7EC28DB20668BFCF87CFC6280AA4252F4` | `C1BA88766D961F050204BB3C39A5D35159A3EAFA38B0525D75D0AAE8739FBFEA` |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` | `540C52E1ED31C11EE49A4AAD4689E69FE43086A7F37FFEF5CAACDF64B3CFB599` | `A42E1206D3D4F416578E7A2A7AFB5DEAF374B1F1EEC78D27705F24EC1A0C04F4` |

## Command Transcripts

- Execution-run selection focused tests: `bundle://proof/SB06/transcripts/sb06-selection-helper-tests.txt`.
- Processes module build: `bundle://proof/SB06/transcripts/sb06-processes-build.txt`.
- Anti-stub and scope scan: `bundle://proof/SB06/transcripts/sb06-anti-stub-and-scope-scan.txt`.

## Passing Proof

- `bundle://proof/SB06/transcripts/sb06-selection-helper-tests.txt` passed with 34 tests.
- Test name: `CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.ProcessAutomationExecutionRunSelection_SB06_INV_001_selects_latest_current_attempt_competing_run`
- Test name: `CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.ProcessAutomationExecutionRunSelection_SB06_INV_002_preserves_stale_and_approval_blocking_rules`
- Test name: `CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.ProcessAutomationExecutionRunSelection_SB06_INV_003_preserves_completion_and_fresh_recovery_skip_rules`
- Existing wrapper coverage included blocking selection, recoverable selection, reusable chat session, completion skip, fresh recovery skip, and session-busy exception tests.
- `bundle://proof/SB06/transcripts/sb06-processes-build.txt` passed.

## Source Assertions

- `bundle://proof/SB06/source-assertions/execution-run-selection-helper.md`.

## Anti-Stub Audit

- `bundle://proof/SB06/transcripts/sb06-anti-stub-and-scope-scan.txt`.

## Browser Proof

- N/A. Runtime/service refactor only; no UI files changed.
