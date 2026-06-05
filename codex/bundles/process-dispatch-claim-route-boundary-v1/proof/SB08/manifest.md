# SB08 Proof Manifest

- Subbundle: SB08 - Gate B concurrency parity.
- Status: Completed.
- Owned requirements: RQ-002, RQ-005, RQ-006, RQ-013, RQ-014.
- Owned raw notes: RN-001, RN-002, RN-003, RN-004.
- Semantic invariant contract: `bundle://proof/SB08/semantic-invariants.md`.

## Changed File Manifest

| File | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` | `9235EF2103231CE81B30A7057B34CA45763201BF27DD44DAC0D25B006D674A4F` | `0C4B74C0EBC55F2FEB4B0CB2EDFF0A4DC45A8E44D930CA3E89BCFB838125F34B` |

## Production Source Shape

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessAutomationExecutionRunSelection.cs` SHA-256 `D5A6A4900375C22B844F3B45F2915DBA518BF321C1BFA25F64B8A5CB932C2C08`.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs` SHA-256 `C1BA88766D961F050204BB3C39A5D35159A3EAFA38B0525D75D0AAE8739FBFEA`.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` SHA-256 `F460A97A134204480E265ADDF5AB334B722988859D9BC450CB425168C5EC73D0`.

## Command Transcripts

- Failing-first Gate B baseline: `bundle://proof/SB08/transcripts/sb08-failing-first-head-concurrency-gate.txt`.
- Passing architecture Gate B tests: `bundle://proof/SB08/transcripts/sb08-architecture-gate-b-tests.txt`.
- Passing integration concurrency parity tests: `bundle://proof/SB08/transcripts/sb08-concurrency-parity-integration-tests.txt`.
- Processes module build: `bundle://proof/SB08/transcripts/sb08-processes-build.txt`.
- Anti-stub and scope scan: `bundle://proof/SB08/transcripts/sb08-anti-stub-and-scope-scan.txt`.

## Failing-First Proof

- `bundle://proof/SB08/transcripts/sb08-failing-first-head-concurrency-gate.txt` records exit code `1` against `HEAD` and contains `SB08_INV_001` and `SB08_INV_002`.

## Passing Proof

- `bundle://proof/SB08/transcripts/sb08-architecture-gate-b-tests.txt` passed.
- Test name: `CanDoItAll.Tests.Unit.ProcessAgentExecutionBoundaryArchitectureTests.Process_dispatch_claim_route_gate_b_SB08_INV_001_records_concurrency_helper_parity_and_blocks_side_effect_drift`
- Test name: `CanDoItAll.Tests.Unit.ProcessAgentExecutionBoundaryArchitectureTests.Process_dispatch_claim_route_gate_b_SB08_INV_002_rejects_shallow_wrapper_migration_with_duplicate_selection_logic`
- `bundle://proof/SB08/transcripts/sb08-concurrency-parity-integration-tests.txt` passed with 36 focused concurrency tests.
- `bundle://proof/SB08/transcripts/sb08-processes-build.txt` passed.

## Source Assertions

- `bundle://proof/SB08/source-assertions/gate-b-concurrency-parity.md`.

## Anti-Stub Audit

- `bundle://proof/SB08/transcripts/sb08-anti-stub-and-scope-scan.txt`.

## Browser Proof

- N/A. Runtime/service refactor only; no UI files changed.
