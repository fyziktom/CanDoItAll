# SB04 Proof Manifest

## Implementation Scope

- Added `IProcessStepRecoveryInstructionBuilder` and `ProcessStepRecoveryInstructionBuilder` in `CanDoItAll.Processes.Application`.
- The packet builder consumes strategy diagnostics, persisted runtime diagnostic receipts, recovery decisions, retry budget fields, and resolved assignment launch variables.
- Dispatch appends `Runtime diagnostic rework instruction` for safe current-step retry and includes the same diagnostic repair packet inside manager escalation instructions after blocking.
- Operator rework appends the same diagnostic packet using the pre-rework runtime receipt because the runtime clears step receipts when reopening the step.
- `CanDoItAll.Modules.Processes` registers the builder in DI.

## Validation

- `dotnet build src/Processes/CanDoItAll.Processes.Application/CanDoItAll.Processes.Application.csproj -p:UseArtifactsOutput=true -p:ArtifactsPath=repo://artifacts/sb04-build`
- `dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~ProcessStepRecoveryInstructionBuilderTests|FullyQualifiedName~ExecuteReady_auto_rework_appends_diagnostic_specific_packet|FullyQualifiedName~ExecuteReady_auto_reworks_safe_adapter_contract_violation_before_manager_review|FullyQualifiedName~Request_rework_appends_diagnostic_specific_packet_from_runtime_receipt" -p:UseArtifactsOutput=true -p:ArtifactsPath=repo://artifacts/sb04-unit --results-directory repo://artifacts/sb04-test-results --logger "trx;LogFileName=sb04-unit.trx" --logger "console;verbosity=minimal"`
- Result: 6 tests passed, 0 failed. Existing NU1903 `Microsoft.OpenApi` advisory warning only during restore/build graph loading.
- `dotnet build src/Modules/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj -p:UseArtifactsOutput=true -p:ArtifactsPath=repo://artifacts/sb04-module-build`
- Result: build passed with 0 warnings and 0 errors.
- CodeAnalytics snapshot: `snap-20260708185114-6d1a7173`.
- CodeAnalytics dependency cycle query: `cycles: []`.

## File Hashes

```text
22EEC8AE385FF2869423A550AD3B463A5EDA5911900909AFFC9467C31B27ED98  repo://src/Processes/CanDoItAll.Processes.Application/ProcessStepRecoveryInstructionBuilder.cs
328A569E90508614E8AA6B5D52DECC9FE051BFAD6C7902F5AF5327A2D60F728B  repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeDispatchApplicationService.cs
F9202C7DCBE7549B4D9FDFBED3BA1737DFBA5FDBD7AC222BD941AB384CB0E219  repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeOperatorApplicationService.cs
70F6B670A2027BB1DE88047BF87109A8732D5013752E672E4D1C1E9E8D2EBBF0  repo://src/Modules/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs
AD4A1F9A68C568CB48D673964BE8166FA3EB11D2D1B7F675EFEB6510FDEDF07D  repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessStepRecoveryInstructionBuilderTests.cs
96549A4CA8CA4A0A01D23026C52303E6A4C0E95D4039EF027C564C50CF8DB0BD  repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeDispatchApplicationServiceTests.cs
03FDC481735D7BE0E5D6E08608B6DC9BC37D9347140AC483D423EA21711939C3  repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeOperatorApplicationServiceTests.cs
83B49C8EAE7665E709105C4CC7FC01F0B83BE4F7154001B85B326E0616CF8280  repo://artifacts/sb04-test-results/sb04-unit.trx
```


## Completed Validator Metadata

- Semantic invariant contract: proof/SB04/semantic-invariants.md.
- Portable source proof: bundle://subbundles/04-sb04-diagnostic-rework-packets/README.md.
- Portable bundle proof: bundle://proof/SB04/manifest.md.
- SHA-256 changed-file hash: 73449BC5EC7B81201CAE3396CD2DD4EF24986617C495383CD9C0D9168B9F4592.
- Passing transcript: proof/SB04/transcripts/00-validator-metadata.txt.
- Anti-stub audit transcript: proof/SB04/transcripts/00-validator-metadata.txt.
- Failing-first: N/A - process/non-production final proof uses adversarial negative tests or preserved subbundle proof rather than a historical failing transcript.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| SB04 proof metadata | proof/SB04/manifest.md | proof/SB04/transcripts/00-validator-metadata.txt | final proof closure | proof/SB04/semantic-invariants.md rejects shallow closure |

