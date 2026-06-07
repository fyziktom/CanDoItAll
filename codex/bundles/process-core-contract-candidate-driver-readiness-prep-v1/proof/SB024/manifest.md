# SB024 Critical Proof Manifest

## Gate Result
- Gate: SB024 - Gate H - projection/validation DTO parity.
- Result: Passed.
- Scope: Runtime/service refactor only; browser validation N/A because no UI/browser surface files changed.
- Failing-first proof: N/A - process refactor with no intended behavior change; negative proof is source-level and integration parity based.

## Commands
- `dotnet build .\CanDoItAll.slnx --configuration Debug --no-restore`
- `dotnet test .\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --configuration Debug --no-build --filter "<Gate H unit filter>"`
- `dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Debug --no-build --filter "<Gate H integration filter>"`
- SB024 critical source assertions and forbidden-scope scans.
- SB024 critical proof sanity check.
- `Get-FileHash` for SB024 changed source, tests, docs, and proof artifacts.

## Transcript Evidence
- Build: `bundle://proof/SB024/transcripts/critical-build.txt`
- Unit tests: `bundle://proof/SB024/transcripts/projection-validation-parity-unit-tests.txt`
- Integration tests: `bundle://proof/SB024/transcripts/projection-validation-parity-integration-tests.txt`
- Source assertions and anti-stub/no-Core/no-driver/no-UI scans: `bundle://proof/SB024/transcripts/source-assertions-and-scans.txt`
- Proof sanity check: `bundle://proof/SB024/transcripts/proof-sanity-check.txt`
- Changed-file hashes: `bundle://proof/SB024/transcripts/changed-file-hashes.txt`
- Semantic invariants: `bundle://proof/SB024/semantic-invariants.md`

## Passing Tests
- `Process_core_contract_candidate_driver_readiness_SB024_INV_001_preserves_projection_validation_dto_parity_paths`
- `Artifact_projection_source_family_order_stays_execution_mock_workspace_existing_response_browser_decision`
- `Artifact_projection_source_adapters_are_local_and_used_by_migrated_source_paths`
- `Provider_native_browser_projection_SB10_INV_001_uses_write_coordinator_for_expected_and_discovered_modes`
- `Process_core_contract_candidate_driver_readiness_SB022_INV_001_splits_projection_run_snapshot_from_execution_detail_observations`
- `Process_core_contract_candidate_driver_readiness_SB023_INV_001_converges_validation_projection_and_satisfaction_expectation_snapshots`
- `ApplyArtifactProjectionLineage_SB02_INV_001_uses_compact_key_for_long_recovery_lineage`
- `ProcessArtifactProjectionLineageBuilder_SB05_INV_001_hashes_recovery_key_and_records_lineage`
- `ProcessArtifactExpectationMatcher_SB05_INV_002_disambiguates_strong_match_by_kind`
- `ProcessArtifactProjectionPlanner_SB09_INV_001_normalizes_projection_adapter_keys`
- `ProcessArtifactProjectionSourceAdapters_SB05_SB08_preserve_key_and_lineage_parity`
- `ResolveCompletionStatus_allows_process_mock_completed_step_with_required_artifact_projection`
- `ResolveCompletionStatus_blocks_process_mock_required_artifact_when_metadata_does_not_match_expectation`
- `ResolveSuccessfulBrowserToolOutputFiles_reads_provider_native_filenames_from_execution_log`
- `ResolveMissingRequiredArtifactSummary_accepts_declared_browser_artifact_with_matching_output`
- `MatchExpectedArtifactId_matches_provider_native_browser_screenshot_to_pathless_visual_expectation`
- `MatchExpectedArtifactId_prefers_route_specific_provider_native_browser_screenshot_expectation`

## Changed File Hashes
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProjectionModels.cs` - `0F793C9AB66C2FF4AE06201D02B32FB913255EFE49A7C704F68D834395A50A50`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionContext.cs` - `A14128CA9C6D28EEDD98DB4A97479B9154F27E0850D0CFF27C0380B97CB85495`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs` - `FC4C5FB9161F92C1EF961CD79CE6679CA6057AC13E0EA06A77EAC534EC3CFC3A`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessWorkspaceWrittenArtifactProjectionCoordinator.cs` - `77D7A9A14105075AD84D72FA39C4414F22710C8C7FDB13FDC944792231F5ACEC`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProviderNativeBrowserArtifactProjectionCoordinator.cs` - `7664BEE5C0A5E32AF48229ABF23736ED4C0803D912DA8B0CBC3BCCDA0EF616D1`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionFacets.cs` - `AE3C07F5D8D7B629655A12FE8C4A4B0C5F65CB1A7DBDE682C9303C36F96429CB`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionFacetImplementations.cs` - `86466C0975EA1E32FD27F467AC58116ED000C1F635B2847392124859904222D6`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactValidationSnapshot.cs` - `D9ACEAE374A0D8448ED3BFF7F2FFC7128101327C0EF4B5E75CB63FAA96AED115`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactValidationSnapshotBuilder.cs` - `C1738942733A292298B652FC72489ACEB51D584363B2610DB29B5CFBB0AD8B85`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactExpectationResolver.cs` - `A3B2F3B3BFCEEB772CF9804AD5EAD2ACC980950A32A2FB80FBC6937C0260B5A2`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactExpectationMatcher.cs` - `CA0CEB18D30779A453863E02E5FBB3F83FA7B09BE33CB4B7ECCDCAB32F445FD4`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactSatisfactionSnapshot.cs` - `EB1FFDC2124105BC5AE707A7ECA024FFAD4133DBEF8C69542A3B18E88739E299`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionSourceAdapters.cs` - `C3A8FF4BEEB6ECB22964DA90957B015C590868E8415ED178145DC738F3761B4B`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionPlanner.cs` - `B995FE0EAC214187295D1E53AF521BA16F73C1BE42FB693A1A1D9DCE041ECFD5`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionCandidateState.cs` - `AD3B8B2A2DC7804EC0CD27FF16370E0394128FD9CE689EF11B4FCB3DA3A90D07`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactPathValidationRules.cs` - `740FA2513AE5496A668EBB93D5279489447A162AC4235FA9381AF1DE0D5F5360`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProviderNativeVisualValidationRules.cs` - `2F850EBB5BCAA7BF665BA00137CB4816D02012A04F1712DB7308926628E99100`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactTextMatchRules.cs` - `B6AC244D4B98743225010653022247C12798E2A676FF52CA0F0FD748148EF505`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExecutionArtifactMetadataRules.cs` - `01C9812C058333C4D65E0400DCC332A4DE33C2884B4BBEF99D26AE90BAA25443`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExistingManagedArtifactProjectionCoordinator.cs` - `B3163BDFF409747838F392C5B03FF330CE919A7ECD9572DD16DEBC7C09EDE703`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProjectStructureArtifactPathRules.cs` - `B53239CCC3FEFC1E4D8AE06424BD5BAF7D1766E7A4F46811736DFDB1869A6869`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessResponseTextArtifactSatisfactionRules.cs` - `7CE19AE2013C582F22CCAEAE320B0E7B9514E64F3E558CAC311F81AADF67C523`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs` - `26F389A12763B900749F231D0BA59A9DAEF94B71A2DC7B31817B04FC2CDB7620`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionOrchestrator.cs` - `06739F4251DEE2682F32F195F5949C285BB8A64EF7C7CE4A0DA3BA162168BE3F`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` - `3CE1E08FCA07A847294C02EBF66DE91FD50F96845C4B6BDFF0ECCCF6E6165B57`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` - `CADBA8F577A1FC49C273DD3003B6F104B756294CB9D95E1D131031A8EA684D6E`
- `bundle://inventories/02-source-hotspots.md` - `10E4704727C65F6A9C2004D89516B47CE23BCA387F198B3EF6B2F4844C906D33`
- `bundle://reviews/01-execution-report.md` - `AC88A8D392463E256BE103A086B35D65C9116F70DA502CBB79E1740B20216C63`
- `bundle://subbundles/SB022/README.md` - `959F3F5C074AD209B9EC5798CEE22141E54BF9CCD356321A5A3F547DE50E39E7`
- `bundle://subbundles/SB023/README.md` - `2163E9A30BD20BDF4DC7340302BD4A32FD6F7FBDA8B047E847A52A80689189EB`
- `bundle://subbundles/SB024/README.md` - `EBD45C25A9519EAE71489587427FF73336CFE64C6ECF5F5C3ECF223FA3D91A12`
- `bundle://proof/SB024/semantic-invariants.md` - `F3457FDF1A9C58993C7F78D4797850418FAF2024A274867412643508EEED7F1D`
- Deleted/absent: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProjectionArtifactExpectation.cs`
- Deleted/absent: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProjectionArtifactExpectationResolver.cs`

## Source Assertions
- `bundle://proof/SB024/transcripts/source-assertions-and-scans.txt` confirms projection source-family order.
- `bundle://proof/SB024/transcripts/source-assertions-and-scans.txt` confirms validation, projection, matcher, resolver, and satisfaction surfaces use `ProcessArtifactExpectationSnapshot`.
- `bundle://proof/SB024/transcripts/source-assertions-and-scans.txt` confirms old projection/validation expectation DTO names and conversion helpers are absent from active source.
- `bundle://proof/SB024/transcripts/source-assertions-and-scans.txt` confirms source adapter external-reference and lineage builder paths remain present.
- `bundle://proof/SB024/transcripts/source-assertions-and-scans.txt` confirms provider-native browser projection uses observation snapshots and expected/discovered adapter plans.
- `bundle://proof/SB024/transcripts/source-assertions-and-scans.txt` confirms no Process Core project/directory, no production driver API token, no UI/browser file drift, and no stub markers in changed SB024 source files.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative-test citation |
| --- | --- | --- | --- | --- |
| `ProcessProjectionObservationSnapshot` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProjectionModels.cs` via `ProcessProjectionSnapshotBuilderAdapter.FromExecutionDetailObservations` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessWorkspaceWrittenArtifactProjectionCoordinator.cs` and `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProviderNativeBrowserArtifactProjectionCoordinator.cs` | Built at the dispatcher artifact-projection edge from execution detail, then carried as narrow receipt/browser facts through projection context. | `Process_core_contract_candidate_driver_readiness_SB022_INV_001_splits_projection_run_snapshot_from_execution_detail_observations` |
| `ProcessArtifactExpectationSnapshot` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactValidationSnapshotBuilder.cs` via `FromDispatchExpectation` | Validation snapshots, projection candidate snapshots, expectation matcher/resolver, projection source adapters, and satisfaction snapshots | Built from dispatcher expected artifacts and reused as the module-local read model across validation, projection, and satisfaction. | `Process_core_contract_candidate_driver_readiness_SB023_INV_001_converges_validation_projection_and_satisfaction_expectation_snapshots` and `Process_core_contract_candidate_driver_readiness_SB024_INV_001_preserves_projection_validation_dto_parity_paths` |
| `ProcessArtifactExpectationResolver` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactExpectationResolver.cs` | Projection facet matcher and artifact validation matcher paths | Resolves `ProcessArtifactExpectationSnapshot` against artifacts while preserving project-structure, provider-native visual, and narrative evidence rules. | `ProcessArtifactExpectationMatcher_SB05_INV_002_disambiguates_strong_match_by_kind`, `MatchExpectedArtifactId_matches_provider_native_browser_screenshot_to_pathless_visual_expectation`, and `MatchExpectedArtifactId_prefers_route_specific_provider_native_browser_screenshot_expectation` |

## Downstream Gate
- SB025-SB033 may proceed only while Gate H proof remains valid: projection source order, source adapter external references, recovery lineage, artifact satisfaction, provider-native browser evidence, no Core project, and no production driver API all remain guarded.
