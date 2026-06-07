# SB027 Critical Proof Manifest

## Gate Result
- Gate: SB027 - Gate I - pure-rule parity and Core candidate list.
- Result: Passed.
- Scope: Runtime/service refactor only; browser validation N/A because no UI/browser surface files changed.
- Failing-first proof: N/A - process refactor with no intended behavior change; negative proof is source-level, unit architecture, and integration parity based.

## Commands
- `dotnet build .\CanDoItAll.slnx --configuration Debug --no-restore`
- `dotnet test .\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --configuration Debug --no-build --filter "<SB027 Gate I architecture filter>"`
- `dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Debug --no-build --filter "<SB027 Gate I integration filter>"`
- SB027 source assertions and forbidden-scope scans.
- SB027 critical proof sanity check.
- `Get-FileHash` for SB027 changed source, tests, docs, and proof artifacts.

## Transcript Evidence
- Build: `bundle://proof/SB027/transcripts/critical-build.txt`
- Unit architecture tests: `bundle://proof/SB027/transcripts/gate-i-architecture-tests.txt`
- Integration parity tests: `bundle://proof/SB027/transcripts/gate-i-integration-parity-tests.txt`
- Source assertions and anti-stub/no-Core/no-driver/no-UI scans: `bundle://proof/SB027/transcripts/source-assertions-and-scans.txt`
- Proof sanity check: `bundle://proof/SB027/transcripts/proof-sanity-check.txt`
- Changed-file hashes: `bundle://proof/SB027/transcripts/changed-file-hashes.txt`
- Semantic invariants: `bundle://proof/SB027/semantic-invariants.md`

## Passing Tests
- `Process_core_contract_candidate_driver_readiness_SB027_INV_001_preserves_pure_rule_parity_and_core_candidate_boundaries`
- `IsRunClosedToAutomation_keeps_reopened_step_dispatchable_inside_failed_run`
- `IsStepStatusDispatchableForRun_restricts_failed_runs_to_reopened_inprogress_steps`
- `IsRunEligibleForDispatchCandidate_allows_failed_run_recovery_without_reopening_closed_runs`
- `ProcessDispatchRouteEligibility_SB05_INV_002_preserves_run_and_step_dispatch_rules`
- `SubprocessArtifactProjectionMapping_SB09_INV_001_uses_child_expectation_id_when_same_kind_titles_conflict`
- `SubprocessArtifactProjectionMapping_SB09_INV_001_blocks_same_kind_heuristic_without_child_mapping`
- `SubprocessArtifactProjectionMapping_SB09_INV_001_warns_when_legacy_same_kind_fallback_maps`

## Changed File Hashes
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` - `2C60F71FC8AD6921ADB0A6643D4A67A66B0EC894585A8DB785EE0C5AA36D997A`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` - `35451AE150FD1C28C2C9A1EAF282FE9223FD7493D8B0A564D306333F43D84DBE`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` - `92E9CBAAFC1BAF431D0438099A8E53043B203AE1A0E1E055AD7F035D9FAB2D19`
- `bundle://architecture/04-core-readiness-decision-matrix-template.md` - `A8A4276FC636A5B23835E4364DAEB3A2CC4827EF156BB6414352984C9485D036`
- `bundle://subbundles/SB027/README.md` - `CDA4CD9C4F18B3642ED25751FBE85157068CFD1FFEE68BFF338FBE45A5374ACF`
- `bundle://reviews/01-execution-report.md` - `5810ED185F12760B11C017B46D56F20E2ABE8DCDCA458211A63E62AFC15C6AB0`
- `bundle://inventories/02-source-hotspots.md` - `B3BD697B3BF0701F2B3D3562D1BE899A47682EFEA9AB39526F66F4C11D60855B`
- `bundle://proof/SB027/semantic-invariants.md` - `82A747B99916EF6A5DB0A2C801291A146397C4E0714C25C138AF1353E6E6D511`
- Deleted/absent: `repo://src/CanDoItAll.Processes.Core`

## Source Assertions
- `bundle://proof/SB027/transcripts/source-assertions-and-scans.txt` confirms removed dispatcher facade methods and facade call sites are absent.
- `bundle://proof/SB027/transcripts/source-assertions-and-scans.txt` confirms route eligibility and subprocess artifact resolver owners remain present and directly tested.
- `bundle://proof/SB027/transcripts/source-assertions-and-scans.txt` confirms application-local side-effect helpers remain named and are not hidden behind pure-rule ownership.
- `bundle://proof/SB027/transcripts/source-assertions-and-scans.txt` confirms route services remain adapter-free.
- `bundle://proof/SB027/transcripts/source-assertions-and-scans.txt` confirms no Process Core project/directory, no production driver API token, no Process Core namespace/project token, no UI/mobile/media drift, and no stub markers in SB027 added diff lines.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative-test citation |
| --- | --- | --- | --- | --- |
| `ProcessDispatchRouteEligibility` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs` | Dispatch route services and focused route eligibility tests | Owns the module-local pure eligibility rules after dispatcher facade removal; it does not execute claims, EF queries, transitions, or workspace writes. | `Process_core_contract_candidate_driver_readiness_SB027_INV_001_preserves_pure_rule_parity_and_core_candidate_boundaries` and `ProcessDispatchRouteEligibility_SB05_INV_002_preserves_run_and_step_dispatch_rules` |
| `ProcessSubprocessArtifactSourceResolver` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessLifecycleRules.cs` | Subprocess projection planning and focused subprocess artifact mapping tests | Owns deterministic subprocess source-artifact and output-mapping selection; projection persistence, gap journals, and child-run writes remain application-local. | `Process_core_contract_candidate_driver_readiness_SB027_INV_001_preserves_pure_rule_parity_and_core_candidate_boundaries`, `SubprocessArtifactProjectionMapping_SB09_INV_001_uses_child_expectation_id_when_same_kind_titles_conflict`, and `SubprocessArtifactProjectionMapping_SB09_INV_001_blocks_same_kind_heuristic_without_child_mapping` |
| `Core Readiness Decision Matrix` | `bundle://architecture/04-core-readiness-decision-matrix-template.md` | SB028-SB033 driver-readiness documentation and final red-team gate | Records candidate-later areas separately from application-local behavior and keeps future driver work documentation-only until later gates prove the boundary. | `Process_core_contract_candidate_driver_readiness_SB027_INV_001_preserves_pure_rule_parity_and_core_candidate_boundaries` |

## Downstream Gate
- SB028-SB033 may proceed only while Gate I proof remains valid: pure rule parity remains green, side-effectful application behavior remains module-local, no Core project exists, and no production process driver API exists.
