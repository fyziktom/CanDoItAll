# SB03 Proof Manifest

## Status

Passed.

## Changed Runtime Surface

- Added provider usage normalization in `src/CanDoItAll.AgentFramework.Core/Execution/ProviderUsageNormalization.cs`.
- Added reconciliation models and reporter in `src/CanDoItAll.AgentFramework.Models/Providers/ProviderUsageReconciliationModels.cs`.
- Routed MAF usage observations through the normalizer in `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`.
- Added raw usage fixture and reconciliation tests in `tests/CanDoItAll.Tests.Unit/ProviderUsageNormalizationTests.cs`.

## Proof Artifacts

- Failing-first transcript: `transcripts/failing-first-provider-usage-normalization.txt`.
- Passing raw usage/reconciliation tests: `transcripts/passing-provider-usage-normalization.txt`.
- Passing pricing tests: `transcripts/passing-provider-pricing-tests.txt`.
- Passing workflow usage tests: `transcripts/passing-workflow-usage-tests.txt`.
- Passing execution/finalizer/provider-failure usage tests: `transcripts/passing-agentframework-execution-usage-tests.txt`.
- Passing process cost aggregation tests: `transcripts/passing-process-costing-tests.txt`.
- Live OpenAI Responses usage smoke with redacted identifiers: `live/openai-responses-live-smoke-redacted.json`.
- Redacted imported OpenAI reconciliation report: `reconciliation/openai-reconciliation-report-redacted.json`.
- Semantic invariants: `semantic-invariants.md`.
- Source assertions: `source-assertions.txt`.
- Anti-stub audit: `anti-stub-audit.txt`.
- Changed-file hashes: `changed-file-hashes.txt`.

## Test Summary

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "SB03_INV" --no-restore`: 7 passed.
- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~ProviderPricingTests" --no-build --no-restore`: 6 passed.
- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~WorkflowExecutorTests&FullyQualifiedName~Usage" --no-build --no-restore`: 2 passed.
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~AgentFrameworkExecutionRunTrackingIntegrationTests.ExecuteRunAsync_persists_provider_usage_without_prompt_double_counting|FullyQualifiedName~AgentFrameworkExecutionRunTrackingIntegrationTests.ExecuteRunAsync_preserves_usage_when_runtime_fails_after_provider_call|FullyQualifiedName~AgentFrameworkExecutionRunTrackingIntegrationTests.ExecuteRunAsync_records_finalizer_short_circuit_usage_when_metrics_are_zero" --no-build --no-restore`: 3 passed.
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests.ResolveProcessRunActualCost_prefers_usage_ledger_over_legacy_metrics" --no-build --no-restore`: 1 passed.

## Raw Note Closure

SB03 closes the raw-note slice for token/cost mismatch by adding provider-specific raw usage normalization, preserving usage-null as unknown/unavailable, introducing a response-id reconciliation report format, running a live OpenAI Responses usage smoke with redacted identifiers, and proving existing execution/process/workflow consumers remain usage-observation-first. UI display of usage states remains assigned to SB08.

## Scope Exceptions

None. Live OpenAI Responses smoke succeeded with `OPENAI_API_KEY` present and no provider secret recorded.
