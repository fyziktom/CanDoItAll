# SB05 Proof Manifest

## Implementation Scope

- Added explicit managed outcome lifecycle states inside `AgentFrameworkProcessExecutionAdapter.ManagedArtifacts.cs`: `Unchanged`, `StructuredOutcomeStaged`, and `MaterializationFailed`.
- Changed runtime managed artifact wording from premature validated/accepted language to staged/captured language before gates pass.
- `ExecuteAsync` now evaluates aggregate completion gates before managed artifact acceptance, content hash readback, and produced-slot promotion.
- Runtime appends `Runtime Accepted Completion Gates` only after all completion gates pass.
- `ParentSubprocessArtifactBridge` rejects child artifacts that carry the new captured marker without the accepted marker.

## Validation

- `dotnet build src/Modules/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj -p:UseArtifactsOutput=true -p:ArtifactsPath=repo://artifacts/sb05-module-build`
- Result: build passed with 0 warnings and 0 errors.
- `dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~ExecuteAsync_materializes_managed_artifact_from_valid_structured_outcome|FullyQualifiedName~ExecuteAsync_stages_managed_artifact_without_acceptance_when_completion_gate_fails|FullyQualifiedName~ExecuteAsync_appends_staged_and_accepted_outcome_to_existing_managed_artifact|FullyQualifiedName~ExecuteAsync_recovers_completed_primary_artifact_and_prior_product_receipts_on_retry|FullyQualifiedName~Parent_subprocess_bridge_accepts_only_typed_child_outputs|FullyQualifiedName~Parent_subprocess_bridge_rejects_staged_child_output_without_gate_acceptance|FullyQualifiedName~Completion_gate_evaluator_reports_missing_required_script_receipt_and_failed_solution_readback" -p:UseArtifactsOutput=true -p:ArtifactsPath=repo://artifacts/sb05-unit -p:WarningsNotAsErrors=NU1903 --results-directory repo://artifacts/sb05-test-results --logger "trx;LogFileName=sb05-unit.trx" --logger "console;verbosity=minimal"`
- Result: 7 tests passed, 0 failed. Existing NU1903 `Microsoft.OpenApi` advisory warning only during restore/build graph loading.
- CodeAnalytics snapshot: `snap-20260708191340-60b7e58e`.
- CodeAnalytics dependency cycle query: `cycles: []`.

## File Hashes

```text
A7E35B669C535EC6EE1F6D57367872CF37E772809C4817ADB1D46D74C59B00E0  repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.cs
2D9DB16523678A131667FB33584826D6D0C805CA633B09B9516FFEE3E153BBFB  repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ManagedArtifacts.cs
544883A6A3FCB0B5C5B06DDDB52CEF8BA82BE5E18F026B1E8BF9975942F113FD  repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.CompletionGates.cs
582F66C9F0F26C531420D1D143BC2751EE34C4E31FD5E02336C9DB0978212530  repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ParentSubprocessArtifactBridge.cs
B0CBF9DA171E93F9AEFB792AF34FF8A8121C2DB954ECD69701BF59673FAD5F25  repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs
945E53B10D54E7906055CC10112501930E454A39F08ECDC535F999E4D018221A  repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessMafHardeningRegressionTests.cs
C168630FE401420E16D0983D59CC64D1219D5A87E7FF00EC161588F5BC9F52D9  repo://artifacts/sb05-test-results/sb05-unit.trx
```


## Completed Validator Metadata

- Semantic invariant contract: proof/SB05/semantic-invariants.md.
- Portable source proof: bundle://subbundles/05-sb05-managed-artifact-acceptance-order/README.md.
- Portable bundle proof: bundle://proof/SB05/manifest.md.
- SHA-256 changed-file hash: AC2FE8C033B8784B37ABED2A75F90B66456941BFB776FCA46917A78E3DC02E98.
- Passing transcript: proof/SB05/transcripts/00-validator-metadata.txt.
- Anti-stub audit transcript: proof/SB05/transcripts/00-validator-metadata.txt.
- Failing-first: N/A - process/non-production final proof uses adversarial negative tests or preserved subbundle proof rather than a historical failing transcript.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| SB05 proof metadata | proof/SB05/manifest.md | proof/SB05/transcripts/00-validator-metadata.txt | final proof closure | proof/SB05/semantic-invariants.md rejects shallow closure |

