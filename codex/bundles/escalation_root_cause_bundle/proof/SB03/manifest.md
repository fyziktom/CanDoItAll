# SB03 Proof Manifest

## Implementation Scope

- Added runtime-owned `ProcessRecoveryClassifier` and `IProcessRecoveryClassifier` in `CanDoItAll.Processes.Runtime`.
- Extended recovery decision receipts with diagnostic fingerprint and bounded retry counters.
- Routed safe/idempotent completion-gate diagnostics to `SafeRetry` / `CurrentStepRetry`.
- Preserved manager escalation for unsafe, non-idempotent, policy-denied, unknown, and budget-exhausted diagnostics.
- Prevented dispatch manager recovery instructions from being sent when the committed recovery result reopens the current step as `Ready`.

## Validation

- `dotnet build src/Processes/CanDoItAll.Processes.Runtime/CanDoItAll.Processes.Runtime.csproj -p:UseArtifactsOutput=true -p:ArtifactsPath=repo://artifacts/sb03-build`
- `dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~ProcessRecoveryClassifierTests|FullyQualifiedName~Safe_idempotent_completion_gate_result_routes_to_current_step_retry|FullyQualifiedName~Safe_idempotent_completion_gate_result_escalates_after_same_fingerprint_budget|FullyQualifiedName~ExecuteReady_auto_reworks_safe_adapter_contract_violation_before_manager_review|FullyQualifiedName~ExecuteReady_auto_reworks_adapter_manager_result_once_before_same_fingerprint_block|FullyQualifiedName~ExecuteReady_auto_reworks_adapter_manager_result_with_new_hash_before_same_fingerprint_block|FullyQualifiedName~ExecuteReady_blocks_transient_execution_manager_result_before_global_attempt_budget|FullyQualifiedName~ExecuteReady_blocks_identical_transient_execution_retry_after_transient_budget" -p:UseArtifactsOutput=true -p:ArtifactsPath=repo://artifacts/sb03-unit --results-directory repo://artifacts/sb03-test-results --logger "trx;LogFileName=sb03-unit.trx" --logger "console;verbosity=minimal"`
- Result: 11 tests passed, 0 failed. Existing NU1903 `Microsoft.OpenApi` advisory warning only.
- CodeAnalytics snapshot: `snap-20260708183408-4375209f`.
- CodeAnalytics dependency cycle query: `cycles: []`.

## File Hashes

```text
432E7632CE4494C52B631EC0C76E5C58175CE8E08DF88DC4C5EBC2865E739FE7  repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRecoveryClassifier.cs
C91701CBBC35F723DC58761E8CCA02933BE47FD63D2B92A07BB565081046479B  repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeState.cs
38141C4FF67010E7EA09D621F1F1E8B76BE1A5BA6347151FAC8129AD34E569D1  repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.Results.cs
2667F6C723999EE9081F1B3A55388F24311F4561F3153F61B6C82B8B5AA0387A  repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.ResultHelpers.cs
ED9F97B9C71C58C4C021CA3AC53ADADC47AADB62FCF8FD95D1D3441C006D8928  repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeDispatchApplicationService.cs
02B89E09140D93E65AB5306ACF4CDB67D658F72AFF8BE2DC5B928F321FEE621E  repo://src/Processes/CanDoItAll.Processes.Persistence/ProcessPersistenceMappers.cs
E40180E46BF2085740FB2E3EC2D064932980A683A58FCA28A7038E4ED0789E2B  repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRecoveryClassifierTests.cs
B517AEAB1F1ABFA6757844A468DB374BC0D24E5F514B7863A07EAFB523809F54  repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs
E62B239678FFCBB8C63337F2470B066C779B617D659F187FD9B22F4293DC27B9  repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeDispatchApplicationServiceTests.cs
29564FEBC453EA386A69CAF60BD0B6D58558D2CB00A3153B55D9948EF83DAA00  repo://artifacts/sb03-test-results/sb03-unit.trx
```


## Completed Validator Metadata

- Semantic invariant contract: proof/SB03/semantic-invariants.md.
- Portable source proof: bundle://subbundles/03-sb03-recovery-classifier-safe-rework/README.md.
- Portable bundle proof: bundle://proof/SB03/manifest.md.
- SHA-256 changed-file hash: AF1B0CE4B3F2669796BC6155A3AF02A37E79E8DD0CA67AA0F55C995CA8F544D8.
- Passing transcript: proof/SB03/transcripts/00-validator-metadata.txt.
- Anti-stub audit transcript: proof/SB03/transcripts/00-validator-metadata.txt.
- Failing-first: N/A - process/non-production final proof uses adversarial negative tests or preserved subbundle proof rather than a historical failing transcript.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| SB03 proof metadata | proof/SB03/manifest.md | proof/SB03/transcripts/00-validator-metadata.txt | final proof closure | proof/SB03/semantic-invariants.md rejects shallow closure |

