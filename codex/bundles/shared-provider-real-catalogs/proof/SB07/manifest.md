# SB07 proof manifest

- Status: Completed for N011/R11 and N012/R12 code scope; broader failures are explicit below.
- Raw input: bundle://inputs/06-thinking-effort-feedback.md.
- Invariant contract: bundle://proof/SB07/semantic-invariants.md.
- Changed files / before and after SHA-256: bundle://proof/SB07/changed-files.csv.
  BeforeHeadBlobSha256 is the exact HEAD blob, not an assertion about workspace line
  endings. CapturedPreEditSha256 retains the 14 exact captures in before-hashes.csv;
  uncaptured files are not falsely described as exact pre-edit workspace captures.
  New files have no before contents. Proof artifacts are separately hashed in proof-hashes.csv.
- Architecture/source review: bundle://proof/SB07/architecture-review.md.
- Anti-stub and exact original-result audit: bundle://proof/SB07/Collect-Closure.ps1 and
  bundle://proof/SB07/closure-audit.txt. Production assertions accompany, not replace,
  the real behavioral tests. No placeholder, fixture-agent branch or production TODO found.
- Final verifier: bundle://reviews/03-thinking-final-verifier.md.

## Final test commands and original artifacts

Working directory: repository root. Configuration: Release, net10.0, xUnit/VSTest.
Each final run used dotnet test PROJECT -c Release --no-build --no-restore --filter FILTER
--logger "trx;LogFileName=RESULT" --results-directory proof-directory -v quiet.
Discovery used the same configuration/filter with --list-tests before execution.
TRX Times fields retain actual timestamps; all final command exits were zero.

| Project | Filter (each term prefixed FullyQualifiedName~) | Result / console / discovery |
| --- | --- | --- |
| tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj | SharedThinkingEffort, SharedProviderRelayPolicyTests, SharedProviderPublicationAndCatalogTests, SharedProviderProtocolContractTests, ProviderProfileThinkingCapabilityTests, AgentExecutionPreparationCacheTests, LlmChatProviderRuntimeTests, MafModelParametersBuilderTests, AgentProviderModelParameterPolicyTests (OR) | unit-verification.trx / unit-verification.txt / unit-verification-discovery.txt |
| tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj | AgentThinkingEffortSettingsTests, ProviderModelSelectorTests, AgentDetailsDialogThinkingEffortTests, AgentProviderPresentationMapperTests, LlmChatDefinitionUiTests (OR) | components-verification.trx / components-verification.txt / components-verification-discovery.txt |
| tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj | SharedProviderStreamingIntegrationTests, SharedProviderRuntimeProjectionIntegrationTests, SharedProviderOpenAiCompatibilityIntegrationTests (OR) | relay-integration-after-discovery.trx / relay-integration-after-discovery.txt / relay-integration-verification-discovery.txt |

All paths in this table resolve beside this manifest. Final totals: 206 + 46 + 56 =
308 passed, zero failures/skips. Exact test-name equality, not counts alone, is recorded
in discovery-verification.json and checked case-sensitively by closure-audit.txt.
Earlier Run-Focused.ps1 describes the first checkpoint, not the expanded final filters.

## Failing-first and meaningful negatives

- bundle://proof/SB07/red.trx: original shared-model effort control disabled.
- bundle://proof/SB07/temperature-red.trx: two failing omission regressions; final Unit passes.
- bundle://proof/SB07/responses-terminal-red.trx: four failing terminal-event regressions;
  all pass in final Integration, including failed/incomplete/inconsistent terminal cases.
- sdk-envelope.trx: accepted exact current SDK foreground/tool envelope. Invalid stored/
  background requests, null descriptions and unsupported tool payloads still fail.
- Unsupported/unknown capabilities, invalid levels, old catalogs, independent overrides,
  revision/caching and legacy selections are in the final tests, not synthetic live proof.

## Scope and downstream gate

CodeAnalytics impact-selection.json names the public-contract/dynamic-dispatch trigger.
Run-Broad.ps1 executed the three supplied suites once at the frozen checkpoint. Those
runs are not green: Unit 1, Components 53 and Integration 18 failures plus one opt-in
skip. Related mapper, function-tool metadata and seven streaming fixture failures were
fixed and rerun in the final focused scope. Exact classification and checkpoint limits:
bundle://proof/SB07/broad-regression-results.md. No full-repository success is claimed.

Post-checkpoint temperature, SDK envelope and terminal-event changes are named bounded
invalidations covered by final tests and SB08 real requests. Docker Release publish
succeeded in bundle://proof/SB08/docker-build-final6.txt. Downstream smoke:
bundle://proof/SB08/manifest.md. The production-artifact matrix is in the invariant contract.

Provider transport limitation: Chat Completions does not accept every reasoning/tools
combination. Existing compatibility policy was not weakened. Actual reasoning proof
uses the UI-created shared Responses profile; see SB08. All three apps were upgraded.
Strict old clients must be upgraded with the new optional catalog fields; old snapshots
without those fields remain compatible and explicitly Unknown.
