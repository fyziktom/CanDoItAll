# SB06 Proof Manifest

## Implementation Scope

- Added stopped-child result kinds to `ParentSubprocessArtifactBridge`: `ChildStoppedBlocked` and `ChildStoppedFailed`.
- Added typed stopped-child diagnostic transfer records carrying child runtime status, child step key/id, diagnostic code, safe summary, evidence hash, retry/idempotency classification, and recovery decision.
- Changed parent subprocess bridge behavior so stopped non-completed child runs are returned to the parent with child root-cause diagnostics instead of being skipped or collapsed into a generic blocked result.
- Changed typed child output resolution to require accepted produced-artifact ledger evidence before bridging a child file to the parent.
- Preserved the SB05 staged-file guard: if a file exists and contains `Runtime Captured Structured Outcome` without `Runtime Accepted Completion Gates`, the bridge rejects it even when a ledger receipt exists.
- Updated subprocess adapter result conversion so blocked and failed child states produce distinct diagnostics: `process.adapter.subprocess_child_blocked` and `process.adapter.subprocess_child_failed`.
- Updated subprocess synthesis paths to append accepted managed-artifact proof only after completion gates pass before produced-slot promotion.

## Validation

- `dotnet build src/Modules/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj -p:UseArtifactsOutput=true -p:ArtifactsPath=repo://artifacts/sb06-module-build -p:WarningsNotAsErrors=NU1903`
- Result: build passed with 0 warnings and 0 errors.
- `dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~Parent_subprocess_bridge_accepts_only_typed_child_outputs|FullyQualifiedName~Parent_subprocess_bridge_rejects_physical_child_output_without_accepted_ledger|FullyQualifiedName~Parent_subprocess_bridge_rejects_staged_child_output_without_gate_acceptance|FullyQualifiedName~Parent_subprocess_bridge_rejects_typed_no_go_child_outputs|FullyQualifiedName~Parent_subprocess_bridge_returns_child_stopped_blocked_with_latest_child_diagnostics|FullyQualifiedName~ExecuteAsync_completes_subprocess_parent_from_completed_child_without_reinvoking_agent|FullyQualifiedName~ExecuteAsync_reports_blocked_subprocess_child_root_cause_without_reinvoking_parent_agent" -p:UseArtifactsOutput=true -p:ArtifactsPath=repo://artifacts/sb06-unit -p:WarningsNotAsErrors=NU1903 --results-directory repo://artifacts/sb06-test-results --logger "trx;LogFileName=sb06-unit.trx" --logger "console;verbosity=minimal"`
- Result: 7 tests passed, 0 failed. Existing NU1903 `Microsoft.OpenApi` advisory warning only during restore/build graph loading.
- CodeAnalytics snapshot: `snap-20260708193105-60b7e58e`.
- CodeAnalytics dependency cycle query: `cycles: []`.

## File Hashes

```text
1DDD6E76B656D036C893267C1C49E35D51B90AF4C7811A888CC59663E6F3239E  repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ParentSubprocessArtifactBridge.cs
F8814A6D4C2BDF386F8AD21560E0BAEEFC35FA223E7EB7080FCF3EB71656CB73  repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.Subprocess.cs
CCC1E695937B4B34C7101261016B7810B243C6DC6981EF252C62FB1978917E0E  repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ResultConversion.cs
A7E35B669C535EC6EE1F6D57367872CF37E772809C4817ADB1D46D74C59B00E0  repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.cs
2D9DB16523678A131667FB33584826D6D0C805CA633B09B9516FFEE3E153BBFB  repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ManagedArtifacts.cs
8A781FD0EFA195BD83FF831D85E2A201076BA5C635EB86E5D8D258C828BC454E  repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessMafHardeningRegressionTests.cs
8BF829B3A33EF56F3350F44F796566157773E96E5F0DDCF9914D43E8C0BABA56  repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs
47D6F052455DBF74F5B0AFB7FB3DA9D176EBA3282A69E3D9BEE1D325F1CB1BBD  repo://artifacts/sb06-test-results/sb06-unit.trx
```


## Completed Validator Metadata

- Semantic invariant contract: proof/SB06/semantic-invariants.md.
- Portable source proof: bundle://subbundles/06-sb06-subprocess-child-diagnostics-ledger-bridge/README.md.
- Portable bundle proof: bundle://proof/SB06/manifest.md.
- SHA-256 changed-file hash: EF9AA8E5897C40DF0DF6569D1F049AD89BA048CE06A5D53A090BCD9E99386E07.
- Passing transcript: proof/SB06/transcripts/00-validator-metadata.txt.
- Anti-stub audit transcript: proof/SB06/transcripts/00-validator-metadata.txt.
- Failing-first: N/A - process/non-production final proof uses adversarial negative tests or preserved subbundle proof rather than a historical failing transcript.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| SB06 proof metadata | proof/SB06/manifest.md | proof/SB06/transcripts/00-validator-metadata.txt | final proof closure | proof/SB06/semantic-invariants.md rejects shallow closure |

