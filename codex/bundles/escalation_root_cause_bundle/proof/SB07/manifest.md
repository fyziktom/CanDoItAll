# SB07 Proof Manifest

## Implementation Scope

- Added typed deterministic .NET setup tool-plan records in `DotNetSolutionSetupToolPlanGuard.cs`.
- Added preflight validation for `create-dotnet-project`, `add-test-project`, and `repair-solution-setup` launch-variable plans.
- Guard checks now validate required receipts, resolved managed `.ps1` script refs, helper script presence, side-effect manifest mode/scope, exact execution plan references, required product paths, native ProductRoot path scope, and file-content readback checks.
- `ProcessRuntimeToolPreflightResult` now carries typed plan guard issues without breaking existing missing-tool behavior.
- `ProcessRuntimeToolPreflightService` fails before agent dispatch when deterministic .NET setup plan details are invalid.
- Adapter runtime-tool preflight diagnostics now include plan issue codes and summaries.

## Validation

- `dotnet build src/Modules/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj -p:UseArtifactsOutput=true -p:ArtifactsPath=repo://artifacts/sb07-module-build -p:WarningsNotAsErrors=NU1903`
- Result: build passed with 0 warnings and 0 errors.
- `dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~ProcessRuntimeToolPreflightServiceTests|FullyQualifiedName~ExecuteAsync_blocks_before_agent_when_dotnet_setup_plan_guard_fails|FullyQualifiedName~ExecuteAsync_filters_product_receipt_predicates_before_runtime_tool_preflight|FullyQualifiedName~Completion_gate_evaluator_reports_missing_required_script_receipt_and_failed_solution_readback" -p:UseArtifactsOutput=true -p:ArtifactsPath=repo://artifacts/sb07-unit -p:WarningsNotAsErrors=NU1903 --results-directory repo://artifacts/sb07-test-results --logger "trx;LogFileName=sb07-unit.trx" --logger "console;verbosity=minimal"`
- Result: 15 tests passed, 0 failed. Existing NU1903 `Microsoft.OpenApi` advisory warning only during restore/build graph loading.
- CodeAnalytics snapshot: `snap-20260708194440-3c6376ed`.
- CodeAnalytics dependency cycle query: `cycles: []`.
- Source check: `DotNetSolutionSetupToolPlanGuard.cs` and `ProcessRuntimeToolPreflightService.cs` contain no Workbench references.

## File Hashes

```text
0095F1ECF18DBFFE669883E73A6549F293B51ACA97FF53DD7E6AAC961E052153  repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/DotNetSolutionSetupToolPlanGuard.cs
3D950E3E9795BF92C268B27B5BDCA1B41454F762DBE88F37BF224EA847D6D565  repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRuntimeToolPreflightService.cs
8FB2446E02C7D3EA46D2079A164733CD7A9CC3794DFECEFE834DF0B17BCA9E8A  repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ResultConversion.cs
E0C7B8D345EE44EF7C382BC20138E6973E680E4242ABFA5A3D96FAB3AEE7252B  repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeToolPreflightServiceTests.cs
E31C8BD124124C533AB13CBFBEE82A90ACF5484A5C4550964B9EE429BC73C2B0  repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs
0DF8D259ECD999A2124999332BF2384314BADF43648CF4CBD2B4E3EF298079D5  repo://artifacts/sb07-test-results/sb07-unit.trx
```


## Completed Validator Metadata

- Semantic invariant contract: proof/SB07/semantic-invariants.md.
- Portable source proof: bundle://subbundles/07-sb07-tool-plan-guard-dotnet-setup/README.md.
- Portable bundle proof: bundle://proof/SB07/manifest.md.
- SHA-256 changed-file hash: 0EFA811CBDB3CADA46D9EDA3FF439C4E4E4F971E5C8D98C4EADB92F551DAE1F7.
- Passing transcript: proof/SB07/transcripts/00-validator-metadata.txt.
- Anti-stub audit transcript: proof/SB07/transcripts/00-validator-metadata.txt.
- Failing-first: N/A - process/non-production final proof uses adversarial negative tests or preserved subbundle proof rather than a historical failing transcript.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| SB07 proof metadata | proof/SB07/manifest.md | proof/SB07/transcripts/00-validator-metadata.txt | final proof closure | proof/SB07/semantic-invariants.md rejects shallow closure |

