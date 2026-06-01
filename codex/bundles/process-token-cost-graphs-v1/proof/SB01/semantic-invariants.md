# SB01 Semantic Invariants

## SB01-I01 Provider Input Is Not Double Counted

- Invariant ID: SB01-I01
- Source raw note: "Improve used tokens usage and price and statistic calculations" and the observed mismatch between UI tokens/cost and OpenAI billing usage.
- Expected behavior: successful execution metrics use provider-reported input tokens as the persisted input token count.
- Disallowed shallow implementation: add cached-token fields but keep adding the local prompt estimate to successful provider input usage.
- Failing-first test: n/a process exemption; no pre-change test run was captured before implementation, but the shallow implementation is explicitly named and the passing integration assertion would reject it.
- Passing test: `bundle://proof/SB01/transcripts/integration-execution-tracking.txt`.
- Changed source files: `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`.
- Production assertions: `bundle://proof/SB01/transcripts/source-assertions.txt`.
- Red-team negative case: a fake provider response with input 12 and a prompt estimate would persist more than 12 before the fix; the passing test asserts exactly 12.
- Downstream dependency check: SB02 and SB03 graph totals consume persisted metrics, so graph proof depends on this invariant.

## SB01-I02 Output Tokens Are Counted

- Invariant ID: SB01-I02
- Source raw note: "Assure we are counting also outptut ... tokens for openai provider."
- Expected behavior: provider-reported output tokens flow from runtime response through continuation aggregation to persisted metrics.
- Disallowed shallow implementation: only count input/cached input and leave output at zero.
- Failing-first test: n/a process exemption; no pre-change test run was captured before implementation, but a zero-output shallow implementation would fail the passing integration assertion.
- Passing test: `bundle://proof/SB01/transcripts/integration-execution-tracking.txt`.
- Changed source files: `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Chat.cs`, `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`.
- Production assertions: `bundle://proof/SB01/transcripts/source-assertions.txt`.
- Red-team negative case: zero-output fake accounting would fail the test's expected output total.
- Downstream dependency check: live/process graphs include output usage from `ProcessLiveMetricPoint.OutputTokens`.

## SB01-I03 Cached Input Tokens Are Provider-Reported Only

- Invariant ID: SB01-I03
- Source raw note: "Assure we are counting also ... cached tokens for openai provider. For example ollama, will not have cached tokens, but if provider has it we must calc it correctly."
- Expected behavior: cached input is read from provider usage when present and remains zero when the provider does not report it.
- Disallowed shallow implementation: infer cached input tokens from total input tokens or model names.
- Failing-first test: n/a process exemption; no pre-change test run was captured before implementation, but inferred cached-token behavior is rejected by explicit provider-reported values and default zero behavior.
- Passing test: `bundle://proof/SB01/transcripts/integration-execution-tracking.txt`.
- Changed source files: `repo://src/CanDoItAll.AgentFramework.Core/Contracts/Contracts.cs`, `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`, `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Chat.cs`, `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`.
- Production assertions: `bundle://proof/SB01/transcripts/source-assertions.txt`.
- Red-team negative case: providers with no cached usage property still produce the default zero value instead of an invented cached estimate.
- Downstream dependency check: SB02 cached input statistics and SB03 cached input chart series use this persisted value.

## SB01-I04 Cached Input Pricing Contributes To Cost

- Invariant ID: SB01-I04
- Source raw note: "Improve used tokens usage and price ... calculations."
- Expected behavior: when persisted metric cost is not already stored, pricing resolution includes input, cached input, and output token prices from provider pricing metadata.
- Disallowed shallow implementation: display cached token counts while pricing only input/output tokens.
- Failing-first test: n/a process exemption; no pre-change test run was captured before implementation, but cached-token-omitting pricing is rejected by the passing unit assertion.
- Passing test: `bundle://proof/SB01/transcripts/unit-provider-pricing.txt`.
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/ProviderPricingTests.cs`.
- Production assertions: existing production pricing model and cost resolver are exercised by the test without adding fallback prices.
- Red-team negative case: a metric with cached tokens and no persisted cost must resolve to a value that includes cached token price.
- Downstream dependency check: process cost graphs use actual/persisted or resolved costs rather than silently invented prices.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `AgentRuntimeResponse.CachedInputTokens` | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs` and `bundle://proof/SB01/transcripts/source-assertions.txt` | `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Chat.cs` and `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` | runtime response is produced for agent execution and persisted in `AgentRunMetric` during successful run completion | `bundle://proof/SB01/transcripts/integration-execution-tracking.txt` proves the persisted value comes from provider usage and is not prompt-estimated |
| `AgentRunMetric.CachedInputTokens` | `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` | `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs` and provider pricing tests | persisted execution metric lifecycle is exercised by execution-run tracking integration tests | `bundle://proof/SB01/transcripts/unit-provider-pricing.txt` proves cached input affects resolved price |
