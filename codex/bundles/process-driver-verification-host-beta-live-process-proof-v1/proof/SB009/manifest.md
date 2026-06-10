# SB009 Gate C Proof Manifest

## Status
Passed.

## Gate Scope
- P03 live process-run OpenAI smoke.
- Adds process-run-specific live proof instead of reusing workspace specialist-agent proof.
- Keeps production process dispatcher behavior unchanged; only integration tests and proof documents changed.

## Changed File Hashes
| Artifact | SHA256 |
| --- | --- |
| tests/CanDoItAll.Tests.Integration/LiveSpecialistAgentScenarioIntegrationTests.cs | fec545c964d7fadcf9c85919781c6e030a4ff002bd66102669c330b86a087bcf |
| tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs | badf7a70badb3b186917a7c239161d99e4e95c4bbb61afb37829f76a82f74bb1 |
| codex/bundles/process-driver-verification-host-beta-live-process-proof-v1/subbundles/SB007/README.md | 9d1d6437e6afd30a350941fabffe09728bedc9dce91be097914a37f9905d89dd |
| codex/bundles/process-driver-verification-host-beta-live-process-proof-v1/subbundles/SB008/README.md | c7d57c2b74d83d72e0957693fd15c60105d5cb8a8a78ea3023167cb48213af06 |
| codex/bundles/process-driver-verification-host-beta-live-process-proof-v1/subbundles/SB009/README.md | be0a36ce52fb42227fbd7c837addd6c5684ad6f5235dbcaab95ce7ad4bdf17e3 |
| codex/bundles/process-driver-verification-host-beta-live-process-proof-v1/reviews/01-execution-report.md | 00012f2379fd9cdfd3534730d364adca45baf128856ac927a850af00136f7135 |
| codex/bundles/process-driver-verification-host-beta-live-process-proof-v1/proof/SB007/transcripts/live-process-run-setup-source-assertions.txt | 233a106e545b6bfa67c85d8fa8eb0803f89f0bf571bc9946c01065e8b5ee7a16 |
| codex/bundles/process-driver-verification-host-beta-live-process-proof-v1/proof/SB008/transcripts/live-process-run-strict-skip-test.txt | ee2930355ac73a20411851789984847194b1fad5746af421207769b946492a56 |
| codex/bundles/process-driver-verification-host-beta-live-process-proof-v1/proof/SB008/transcripts/live-process-run-openai-smoke.txt | 1b651075903f9bb4a272ded326b3bf8e9fabcbe0b0c512f8dbc3599b8fbe4759 |
| codex/bundles/process-driver-verification-host-beta-live-process-proof-v1/proof/SB008/transcripts/live-process-run-budget-red-team.txt | 5d6ff7a1421c1247a00d70939dcbafc594c1cf46696d205f315f87f0df17efb2 |
| codex/bundles/process-driver-verification-host-beta-live-process-proof-v1/proof/SB009/transcripts/gate-c-source-diff-and-anti-stub-audit.txt | 82b51ba9daf0fd5ff4a1dbff3cc5ee3b9636df814f7fe41437d25a3f34d4c7fe |

## Production Behavior Artifact Matrix
| Artifact | Classification | Gate C conclusion |
| --- | --- | --- |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch` | Production dispatcher | No P03 source delta; dispatch behavior is exercised by the new integration test. |
| `src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs` | Production DI | No P03 source delta; `IProcessRunAutomationDispatchService` remains the registered dispatch boundary. |
| `tests/CanDoItAll.Tests.Integration/LiveSpecialistAgentScenarioIntegrationTests.cs` | Test policy | Existing specialist live smoke now requires both specialist and shared OpenAI flags. It remains classified as workspace-only proof. |
| `tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs` | New integration proof | Creates a real process run, resolves an AI-agent assignment, dispatches via `IProcessRunAutomationDispatchService`, and asserts process-bound execution/provider usage. |
| Verification/runtime drivers | No execution-capable expansion | Gate C did not add generic object payload dispatch, fallback selector behavior, or Process Core references. |

## Proof Artifacts
- SB007 setup source assertions: `bundle://proof/SB007/transcripts/live-process-run-setup-source-assertions.txt`.
- SB008 strict skip/compile proof: `bundle://proof/SB008/transcripts/live-process-run-strict-skip-test.txt`.
- SB008 live process-run OpenAI pass: `bundle://proof/SB008/transcripts/live-process-run-openai-smoke.txt`.
- SB008 budget red-team rejection: `bundle://proof/SB008/transcripts/live-process-run-budget-red-team.txt`.
- SB009 source diff and anti-stub audit: `bundle://proof/SB009/transcripts/gate-c-source-diff-and-anti-stub-audit.txt`.
- SB009 proof index: `bundle://proof/SB009/transcripts/gate-c-proof-index.txt`.

## Live Run Classification
- The live pass required `CANDOITALL_RUN_LIVE_PROCESS_RUN_VALIDATION=true`, `CANDOITALL_ENABLE_LIVE_OPENAI_SMOKE=true`, and a present `OPENAI_API_KEY`.
- The OpenAI API key value was never printed or persisted in proof.
- The test configured explicit model, timeout, and total-token ceiling controls.
- Passing assertions require `ProcessRunId`, `ProcessStepId`, `RequestedBy=process-automation-dispatch`, `SourceKind=process-step`, completed execution state, and provider usage observations for the same process run and step.

## Gate C Result
Passed. The live OpenAI proof gap is now closed for a real Process module process run, while specialist-agent live proof remains separately classified and no generic execution-capable driver surface was introduced.
