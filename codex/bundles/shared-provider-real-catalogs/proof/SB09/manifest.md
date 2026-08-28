# SB09 proof manifest

- Status: Completed. Feature and architecture gates pass; all required broad runs
  completed with separately documented pre-existing failures.
- Inputs: bundle://inputs/07-provider-model-thinking-settings-feedback.md, N013/R13
  and N014/R14. Downstream: bundle://proof/SB10/manifest.md.
- Contract: bundle://proof/SB09/semantic-invariants.md.
- Architecture review: bundle://proof/SB09/architecture-review.md.
- Source/bundle bytes: bundle://proof/SB09/before-hashes.csv and changed-files.csv.
  Exact pre-edit workspace hashes were captured before production edits. New files
  are explicitly ABSENT. Artifact hashes are in proof-hashes.csv.
- Original results are under bundle://proof/SB09/transcripts/. No empty placeholder
  result is accepted. Collect-Closure.ps1 verifies exact discovery and original TRX.

## Final command identity

Working directory: repository root; .NET 10, Debug, xUnit/VSTest. Run-Tests.ps1 first
runs dotnet test PROJECT --no-restore --list-tests --filter FILTER -v quiet, then
dotnet test PROJECT --no-build --no-restore --filter FILTER --logger
"trx;LogFileName=LABEL.trx" --results-directory proof/SB09/transcripts -v quiet.
The original console transcript includes full absolute paths, dates and exit codes.

| Project | OR-filter classes (FullyQualifiedName~ prefix) | Final label | Passed |
| --- | --- | --- | ---: |
| tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj | ProviderModelThinkingConfigurationTests, ProviderProfileThinkingCapabilityTests, SharedThinkingEffortTests, SharedProviderPublicationAndCatalogTests, AgentProviderModelParameterPolicyTests, OllamaThinkingEffortAdapterTests | unit-final | 138 |
| tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj | ProviderModelThinkingEditorTests, SharedProviderRefreshButtonTests, AgentThinkingEffortSettingsTests, AgentDetailsDialogThinkingEffortTests, ProviderModelSelectorTests | components-layout-verified | 35 |
| tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj | SharedProviderRuntimeProjectionIntegrationTests, SharedProviderOpenAiCompatibilityIntegrationTests, SharedProviderStreamingIntegrationTests | integration-final | 56 |

Total 229 passing cases, zero failures or skips in this final affected scope.
Exact discovered/executed identity verification: discovery-verification.json and
closure-audit.txt. Earlier components-verified also passed 35; the layout-only
invalidation was rerun after the broad testhost released its assembly locks.

## Red and explicit failed attempts

failing-first.trx contains nine real failures before implementation: manual
capability precedence, source mapping, explicit unsupported and invalid-save cases.
Final tests also check per-model defaults, reset, boolean controls, unrelated JSON,
case-insensitive duplicates, unknown shared metadata and local override isolation.
Refresh tests check lazy source reads, selected/retired imports and failure behavior.

components-second.trx includes one malformed ETag fixture failure, then corrected
to the protocol's valid SHA-256 format. components-final.txt and
components-layout-final.txt are Windows DLL-lock build failures and executed no
tests; they are not passing proof. Their verified successors are named above.

## Broader gate

CodeAnalytics impacted-tests.json returned AllSuppliedSuites (TIA3001 unresolved
members and TIA3004 dynamic dispatch). Run-Broad.ps1 executes each supplied suite
once at the frozen checkpoint. Results are classified in broad-regression-results.md;
no whole-repository-green claim is made. Final CSS/grid-only changes require the
focused Components and browser rerun, not a repeated unfiltered gate.

## Review and limits

No new project reference or runtime partial split. Models owns typed configuration;
Core validates saves; existing source mapper and runtime policy consume it. Imports
remain source-owned. Browser tests use actual UI, real catalogs and real upstreams.
OpenAI reasoning with tools is verified on Responses, not asserted to work for
every Chat Completions combination. Setting controls does not create upstream support.
