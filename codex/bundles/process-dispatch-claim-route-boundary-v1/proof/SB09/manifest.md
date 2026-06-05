# SB09 Proof Manifest

- Subbundle: SB09 - Claim/heartbeat session boundary.
- Status: Completed.
- Owned requirements: RQ-007, RQ-013.
- Owned raw notes: RN-001, RN-003, RN-004.
- Semantic invariant contract: `bundle://proof/SB09/semantic-invariants.md`.

## Changed File Manifest

| File | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchGuardLease.cs` | `NEW` | `AE1E167FEABBE89C668AD9088143955D4587B7B8E532F5226FDCF73C2C64A6B6` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | `1E3DE2CF56EA4DA0637BCEF6CD8135EE096B498CBCC028819DC9F553FA78AEFD` | `09C6EACD5C0D7972BDE36E165C83E77B677AE550B8D51D45C3D1E4DA0EE1A51C` |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` | `F460A97A134204480E265ADDF5AB334B722988859D9BC450CB425168C5EC73D0` | `7EAD7852235D75B7EEFDABD42AF0BBF12F38BD31F2D504652159F14940DB3738` |

## Production Source Shape

- Existing heartbeat helper: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchLeaseHeartbeat.cs` SHA-256 `98418C0C4CD5C63F62DF0F72D883F318F2BE8EAC4FC60CA4ED83300721173D61`.

## Command Transcripts

- Guard and heartbeat focused tests: `bundle://proof/SB09/transcripts/sb09-guard-heartbeat-tests.txt`.
- Processes module build: `bundle://proof/SB09/transcripts/sb09-processes-build.txt`.
- Anti-stub and scope scan: `bundle://proof/SB09/transcripts/sb09-anti-stub-and-scope-scan.txt`.

## Passing Proof

- `bundle://proof/SB09/transcripts/sb09-guard-heartbeat-tests.txt` passed with 4 focused tests.
- Test name: `CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.ProcessDispatchGuardLease_SB09_INV_001_serializes_same_step_and_removes_released_guard`
- Test name: `CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.ProcessDispatchGuardLease_SB09_INV_002_rejects_empty_step_id`
- Test name: `CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.ProcessDispatchLeaseHeartbeat_renews_outer_and_step_claims_during_long_work`
- Test name: `CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.ProcessDispatchLeaseHeartbeat_cancels_dispatch_when_renewal_fails`
- `bundle://proof/SB09/transcripts/sb09-processes-build.txt` passed.

## Source Assertions

- `bundle://proof/SB09/source-assertions/claim-heartbeat-session-boundary.md`.

## Anti-Stub Audit

- `bundle://proof/SB09/transcripts/sb09-anti-stub-and-scope-scan.txt`.

## Browser Proof

- N/A. Runtime/service refactor only; no UI files changed.
