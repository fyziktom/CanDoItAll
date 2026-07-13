# SB08 Proof Manifest

## Implementation Scope

- Added typed template execution contract documents in `ProcessTemplateExecutionContracts.cs`.
- Added central execution class constants for agent-only, guarded tool-plan, deterministic tool-plan, runtime-owned subprocess, and branch-decision steps.
- Extended `ProcessTemplatePackLoader` and source-generated JSON metadata so template definitions materialize `executionClass` and `executionContract`.
- Extended compatibility reports with strict execution contract diagnostics.
- Extended `ProcessTemplateCompatibilityScanner` with opt-in strict validation for prose-only hard gates, deterministic plan shape, required receipts, readback checks, subprocess child outputs, branch outcome identifiers, and produced artifact slots.
- Added focused compatibility-history tests for negative strict validation and positive loader materialization.

## Validation

- `dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~ProcessTemplateCompatibilityHistoryTests" -p:UseArtifactsOutput=true -p:ArtifactsPath=repo://artifacts/sb08-unit -p:WarningsNotAsErrors=NU1903 --results-directory repo://artifacts/sb08-test-results --logger "trx;LogFileName=sb08-unit.trx" --logger "console;verbosity=minimal"`
- Result: 8 tests passed, 0 failed. Existing NU1903 `Microsoft.OpenApi` advisory warning only during restore/build graph loading.
- `dotnet build src/Processes/CanDoItAll.Processes.Templates/CanDoItAll.Processes.Templates.csproj -p:UseArtifactsOutput=true -p:ArtifactsPath=repo://artifacts/sb08-templates-build -p:WarningsNotAsErrors=NU1903`
- Result: build passed with 0 warnings and 0 errors.
- CodeAnalytics snapshot: `snap-20260708195818-85ab0701`.
- CodeAnalytics dependency cycle query: `cycles: []`.

## File Hashes

```text
AF6E9611E8466665E4A93FE29BD30E4597707B592B0BE7CF9A7C8154FA4C69C6  repo://src/Processes/CanDoItAll.Processes.Templates/ProcessTemplateExecutionContracts.cs
B994D45552BC2A22B07C9254D2DD9333035F22606E90CAE34871F6313C5F931A  repo://src/Processes/CanDoItAll.Processes.Templates/ProcessTemplatePackLoader.cs
645DF1E3D615462B134B7F2B8E8A4A12C49DF2D1CFF449363384BD6A38FF42E3  repo://src/Processes/CanDoItAll.Processes.Templates/ProcessTemplateDocuments.cs
0EA82A17875F2D2C3C22C4117324E2F806D168C8350FBDB8A4E46557AB2709B3  repo://src/Processes/CanDoItAll.Processes.Templates/ProcessTemplateCompatibilityReports.cs
0060604ABB32324E31BC351A4962EC666DDAB867C7C2BA31656C91BA5FD08BD9  repo://src/Processes/CanDoItAll.Processes.Templates/ProcessTemplateCompatibilityScanner.cs
81D7D28598196F12CDE26B2252B8CCF427E714A9FE4DE341C6CAAD76E1BDEF05  repo://src/Processes/CanDoItAll.Processes.Templates/ProcessTemplateCompatibilityScanner.ExecutionContracts.cs
F0C6A0DCF1CC6B7F105733FFB39D2EF3EE7C6DC472D6493ABF99F5B795C8871F  repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessTemplateCompatibilityHistoryTests.cs
905F3BAC5B7E74B1C795BB2B556B0BF0F2A67F30C77355B8FD629E786567EE52  repo://artifacts/sb08-test-results/sb08-unit.trx
```


## Completed Validator Metadata

- Semantic invariant contract: proof/SB08/semantic-invariants.md.
- Portable source proof: bundle://subbundles/08-sb08-template-schema-execution-contracts/README.md.
- Portable bundle proof: bundle://proof/SB08/manifest.md.
- SHA-256 changed-file hash: AC654FA85E8AAE6A7EE388D040C61922BB022D1BB048EF7BAC4B0A123A5A32AE.
- Passing transcript: proof/SB08/transcripts/00-validator-metadata.txt.
- Anti-stub audit transcript: proof/SB08/transcripts/00-validator-metadata.txt.
- Failing-first: N/A - process/non-production final proof uses adversarial negative tests or preserved subbundle proof rather than a historical failing transcript.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| SB08 proof metadata | proof/SB08/manifest.md | proof/SB08/transcripts/00-validator-metadata.txt | final proof closure | proof/SB08/semantic-invariants.md rejects shallow closure |

